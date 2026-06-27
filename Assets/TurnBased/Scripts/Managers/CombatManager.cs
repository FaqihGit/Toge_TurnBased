using System;
using System.Collections.Generic;
using Fungus;
using UnityEngine;
using UnityEngine.InputSystem;

public class CombatManager : MonoBehaviour
{

    [SerializeField] private Flowchart combatFlowchart;

    private const int MaxEnergy = 10;

    private readonly List<CombatUnit> playerUnits = new();
    private readonly List<CombatUnit> enemyUnits = new();
    private readonly List<CombatUnit> allUnits = new();
    private readonly List<CombatUnitSnapshot> preCombatSnapshots = new();

    private float turnThreshold;
    private CombatUnit currentActor;
    private bool isCombatActive;
    private bool awaitingAction;

    // ---------------------------------------------------------------------
    // Targeting sub-phase (visual selection of a target for the chosen action)
    // ---------------------------------------------------------------------
    private CombatPartyHandler playerPartyHandler;
    private CombatPartyHandler enemyPartyHandler;

    private CombatActionSO pendingAction;
    private CombatPartyHandler targetingHandler;
    private readonly List<CombatUnit> targetCandidates = new();
    private readonly List<CombatUnit> selectedTargets = new();
    private int targetCursorIndex;
    private bool awaitingTarget;

    // Edge-detection for stick input, matching the deadzone pattern used in
    // CanvasManager/NavigableMenuDialog — stick must return to neutral before
    // another move registers, so holding a direction doesn't spam the cursor.
    private bool targetCursorAxisReleased = true;

    public CombatUnit CurrentActor => currentActor;
    public IReadOnlyList<CombatUnit> PlayerUnits => playerUnits;
    public IReadOnlyList<CombatUnit> EnemyUnits => enemyUnits;
    public bool IsCombatActive => isCombatActive;

    /// Fired when a unit's turn begins. Listeners (PlayerInputRouter, an AI
    /// controller, UI) decide what happens next and call SubmitAction /
    /// SubmitSkip back in — CombatManager never reaches out to them.
    public event Action<CombatUnit> OnTurnStarted;

    /// Fired once when the encounter ends in victory (true) or defeat (false).
    public event Action<bool> OnCombatEnded;

    /// Fired when the player escapes — no victory/defeat occurred.
    public event Action OnEscaped;

    private PlayerInputAction playerControls;

    void OnEnable()
    {
        SubscribeControls(true);
    }

    void OnDisable()
    {
        SubscribeControls(false);
    }

    public void Init(PlayerInputAction playerControls)
    {
        this.playerControls = playerControls;
        SubscribeControls(true);
    }

    private void SubscribeControls(bool isSubscribe)
    {
        if (playerControls == null) return;

        playerControls.Combat.Selection.performed -= OnSelection;
        playerControls.Combat.Select.performed -= OnSelect;
        playerControls.General.Escape.performed -= OnCancelTarget; // NOTE: requires a "Cancel" action in the Combat map

        if (isSubscribe)
        {
            playerControls.Combat.Selection.performed += OnSelection;
            playerControls.Combat.Select.performed += OnSelect;
            playerControls.General.Escape.performed += OnCancelTarget;
        }
    }

    private void OnSelection(InputAction.CallbackContext context)
    {
        if (!awaitingTarget || targetCandidates.Count == 0) return;

        var selectionInput = context.ReadValue<Vector2>();

        if (selectionInput.magnitude < 0.5f)
        {
            targetCursorAxisReleased = true;
            return;
        }

        if (!targetCursorAxisReleased) return;

        // Design intent: Up/Left -> next candidate, Down/Right -> previous,
        // wrapping around at either end.
        int direction = 0;
        if (selectionInput.y > 0.5f || selectionInput.x < -0.5f) direction = 1;
        else if (selectionInput.y < -0.5f || selectionInput.x > 0.5f) direction = -1;

        if (direction == 0) return;

        targetCursorAxisReleased = false;

        int count = targetCandidates.Count;
        targetCursorIndex = ((targetCursorIndex + direction) % count + count) % count;

        targetingHandler.SetIndicator(GetPartySlotIndex(targetCandidates[targetCursorIndex]));
    }

    private void OnSelect(InputAction.CallbackContext context)
    {
        if (!awaitingTarget || targetCandidates.Count == 0) return;

        var chosen = targetCandidates[targetCursorIndex];
        int maxTargets = Mathf.Max(1, pendingAction.targetCount);

        if (!selectedTargets.Contains(chosen))
        {
            selectedTargets.Add(chosen);
            targetingHandler.ShowSelection(true, GetPartySlotIndex(chosen));
        }

        if (selectedTargets.Count >= maxTargets)
            ConfirmTargetingResult(selectedTargets);
    }

    private void OnCancelTarget(InputAction.CallbackContext context)
    {
        if (!awaitingTarget) return;
        CancelTargeting();
    }

    public void StartCombat(CombatPartyHandler playerParty, CombatPartyHandler enemyParty)
    {
        playerPartyHandler = playerParty;
        enemyPartyHandler = enemyParty;

        playerUnits.Clear();
        enemyUnits.Clear();
        allUnits.Clear();
        preCombatSnapshots.Clear();

        BuildParty(playerParty, UnitFactionEnum.Player, playerUnits);
        BuildParty(enemyParty, UnitFactionEnum.Enemies, enemyUnits);

        allUnits.AddRange(playerUnits);
        allUnits.AddRange(enemyUnits);

        // GDD 2: a unit entering combat at <=0 HP starts at 1 HP instead.
        foreach (var u in allUnits)
        {
            if (u.currentHealth <= 0f)
                u.currentHealth = 1f;
        }

        // Snapshot is captured AFTER the 1 HP correction above, so Escape can
        // never roll a unit back into a lethal state.
        foreach (var u in playerUnits)
            preCombatSnapshots.Add(u.CaptureSnapshot());

        // GDD 3.1: threshold = average Speed of all living units, fixed for
        // the whole encounter.
        turnThreshold = 0f;
        foreach (var u in allUnits)
            turnThreshold += u.speed;
        turnThreshold /= Mathf.Max(1, allUnits.Count);

        foreach (var u in allUnits)
            u.speedBank = u.speed;

        isCombatActive = true;
        AdvanceTurn();
    }

    private void BuildParty(CombatPartyHandler partyHandler, UnitFactionEnum faction, List<CombatUnit> destination)
    {
        if (partyHandler.partyUnitList.Count > 4)
            Debug.LogWarning($"{partyHandler.name}: more than 4 units supplied, extras ignored (GDD 2: hard cap of 4 per side).");

        int count = Mathf.Min(4, partyHandler.partyUnitList.Count);
        for (int i = 0; i < count; i++)
            destination.Add(new CombatUnit(partyHandler.partyUnitList[i], faction));
    }


    #region Turn Handler
    // ---------------------------------------------------------------------
    // Turn order — Speed Bank (GDD 3)
    // ---------------------------------------------------------------------

    private void AdvanceTurn()
    {
        while (true)
        {
            CombatUnit best = null;

            foreach (var u in allUnits)
            {
                if (u.IsDead) continue;
                if (u.speedBank < turnThreshold) continue;

                if (best == null || u.speedBank > best.speedBank)
                {
                    best = u;
                }
                else if (Mathf.Approximately(u.speedBank, best.speedBank))
                {
                    // GDD 3.4: tie -> player side priority
                    if (u.faction == UnitFactionEnum.Player && best.faction != UnitFactionEnum.Player)
                        best = u;
                }
            }

            if (best != null)
            {
                best.speedBank -= turnThreshold;
                StartTurn(best);
                return;
            }

            bool anyAlive = false;
            foreach (var u in allUnits)
            {
                if (u.IsDead) continue;
                anyAlive = true;
                u.speedBank += u.speed; // passive fill step — essential, see GDD 3
            }

            if (!anyAlive) return; // CheckEncounterEnd should already have caught this
        }
    }

    private void StartTurn(CombatUnit actor)
    {
        currentActor = actor;

        bool wasStunned = actor.IsStunned;

        if (!wasStunned)
            actor.energy = Mathf.Min(MaxEnergy, actor.energy + 1);

        TickStatuses(actor);

        OnTurnStarted?.Invoke(actor);

        if (wasStunned)
        {
            // GDD 8: Stun skips the turn completely, no action menu.
            EndTurn();
            return;
        }

        awaitingAction = true;
        PromptAction();
    }

    private void PromptAction()
    {
        // Highlight whose turn it is on their own party handler while the
        // action menu is open. Reused for every action this turn (GDD 1:
        // turn = multi-action window), not just the first.
        var actorHandler = currentActor.faction == UnitFactionEnum.Player ? playerPartyHandler : enemyPartyHandler;
        int actorIdx = GetPartySlotIndex(currentActor);

        HideAllIndicators();
        actorHandler.SetIndicator(actorIdx);
        actorHandler.ShowIndicator(true);

        SetActionOption(currentActor, currentActor.source.combatActionList);
        combatFlowchart.ExecuteBlock("TriggerAction");
    }

    private void EndTurn()
    {
        awaitingAction = false;
        awaitingTarget = false;
        HideAllIndicators();
        currentActor = null;
        AdvanceTurn();
    }

    #endregion

    #region External Calls

    // ---------------------------------------------------------------------
    // External API — called by PlayerInputRouter, an AI controller, etc.
    // ---------------------------------------------------------------------

    public void SubmitAction()
    {
        if (!isCombatActive || !awaitingAction || awaitingTarget || currentActor == null) return;

        int actionIdx = combatFlowchart.GetIntegerVariable("ActionIdx");
        var actionList = currentActor.source.combatActionList;
        if (actionIdx < 0 || actionIdx >= actionList.Count) return;

        var action = actionList[actionIdx];
        if (action.energyCost > currentActor.energy) return;

        BeginTargeting(action);
    }

    /// targets is ignored for party-wide actions. For single/limited-target
    /// actions, pass a subset of GetValidTargets(currentActor, action).
    public bool SubmitAction(CombatActionSO action, List<CombatUnit> targets)
    {
        if (!isCombatActive || !awaitingAction || currentActor == null) return false;
        if (action == null || action.energyCost > currentActor.energy) return false;

        var validPool = GetValidTargets(currentActor, action);
        List<CombatUnit> resolvedTargets;

        if (IsPartyWide(action))
        {
            resolvedTargets = validPool;
        }
        else
        {
            resolvedTargets = new List<CombatUnit>();
            if (targets != null)
            {
                foreach (var t in targets)
                    if (validPool.Contains(t)) resolvedTargets.Add(t);
            }

            int maxTargets = Mathf.Max(1, action.targetCount);
            if (resolvedTargets.Count == 0 || resolvedTargets.Count > maxTargets)
                return false;
        }

        currentActor.energy -= action.energyCost;
        ResolveAction(currentActor, action, resolvedTargets);

        if (CheckEncounterEnd()) return true;

        if (!HasAffordableAction(currentActor))
            EndTurn();
        else
            PromptAction(); // GDD 1: same actor can keep acting this turn while they can afford to.

        return true;
    }

    public void SubmitSkip()
    {
        if (!isCombatActive || !awaitingAction) return;
        EndTurn();
    }

    /// GDD 10: always available, no cost, 100% success — voids the encounter
    /// entirely and restores the player party to its pre-combat snapshot.
    public void RequestEscape()
    {
        if (!isCombatActive) return;

        foreach (var snap in preCombatSnapshots)
            snap.unit.RestoreSnapshot(snap);

        foreach (var u in playerUnits)
            u.CommitToSource();

        isCombatActive = false;
        awaitingAction = false;
        awaitingTarget = false;
        HideAllIndicators();
        currentActor = null;

        OnEscaped?.Invoke();
    }

    public List<CombatUnit> GetValidTargets(CombatUnit actor, CombatActionSO action)
    {
        var pool = action.target == CombatTargetEnum.Allies
            ? (actor.faction == UnitFactionEnum.Player ? playerUnits : enemyUnits)
            : (actor.faction == UnitFactionEnum.Player ? enemyUnits : playerUnits);

        var result = new List<CombatUnit>();
        foreach (var u in pool)
            if (!u.IsDead) result.Add(u);

        return result;
    }

    private bool IsPartyWide(CombatActionSO action) => action.targetCount <= 0 || action.targetCount >= 4;

    #endregion

    #region Targeting Visuals

    // ---------------------------------------------------------------------
    // Target selection — cursor + confirmation visuals
    // ---------------------------------------------------------------------

    private void BeginTargeting(CombatActionSO action)
    {
        pendingAction = action;
        targetCandidates.Clear();
        targetCandidates.AddRange(GetValidTargets(currentActor, action));

        if (targetCandidates.Count == 0)
        {
            // No legal targets right now — bounce back to the action menu.
            pendingAction = null;
            PromptAction();
            return;
        }

        if (IsPartyWide(action))
        {
            // GDD 6: "All" scope needs no manual target selection.
            ConfirmTargetingResult(targetCandidates);
            return;
        }

        targetingHandler = GetTargetHandler(currentActor, action);
        selectedTargets.Clear();
        targetCursorIndex = 0;
        targetCursorAxisReleased = true;

        HideAllIndicators();
        targetingHandler.ShowSelectionAll(false);
        targetingHandler.SetIndicator(GetPartySlotIndex(targetCandidates[targetCursorIndex]));
        targetingHandler.ShowIndicator(true);

        awaitingTarget = true;
    }

    private void ConfirmTargetingResult(List<CombatUnit> targets)
    {
        var action = pendingAction;
        pendingAction = null;
        awaitingTarget = false;

        if (targetingHandler != null)
            targetingHandler.ShowSelectionAll(false);
        HideAllIndicators();

        SubmitAction(action, targets);
    }

    private void CancelTargeting()
    {
        awaitingTarget = false;

        if (targetingHandler != null)
            targetingHandler.ShowSelectionAll(false);

        pendingAction = null;
        selectedTargets.Clear();
        HideAllIndicators();

        PromptAction();
    }

    private CombatPartyHandler GetTargetHandler(CombatUnit actor, CombatActionSO action)
    {
        bool actorIsPlayer = actor.faction == UnitFactionEnum.Player;
        bool targetsAllies = action.target == CombatTargetEnum.Allies;

        // Mirrors the pool selection in GetValidTargets.
        bool targetsPlayerSide = targetsAllies ? actorIsPlayer : !actorIsPlayer;
        return targetsPlayerSide ? playerPartyHandler : enemyPartyHandler;
    }

    private int GetPartySlotIndex(CombatUnit unit)
    {
        int idx = playerUnits.IndexOf(unit);
        if (idx >= 0) return idx;
        return enemyUnits.IndexOf(unit);
    }

    private void HideAllIndicators()
    {
        playerPartyHandler?.ShowIndicator(false);
        enemyPartyHandler?.ShowIndicator(false);
    }

    #endregion

    #region Action Handler

    // ---------------------------------------------------------------------
    // Action resolution
    // ---------------------------------------------------------------------

    private void ResolveAction(CombatUnit actor, CombatActionSO action, List<CombatUnit> targets)
    {
        foreach (var target in targets)
        {
            if (target.IsDead) continue;

            bool dealtDamage = false;

            if (action.damageMult != 0f)
            {
                float rawAmount = actor.attack * action.damageMult;

                if (rawAmount > 0f)
                {
                    float mitigated = action.isIgnoreDef ? rawAmount : Mathf.Max(0f, rawAmount - target.defense);
                    target.currentHealth = Mathf.Clamp(target.currentHealth - mitigated, 0f, target.maxHealth);
                    dealtDamage = true;
                }
                else
                {
                    // Negative damageMult = heal; defense/isIgnoreDef don't apply.
                    target.currentHealth = Mathf.Clamp(target.currentHealth - rawAmount, 0f, target.maxHealth);
                }
            }

            if (action.isBuff)
                ApplyStatModBuff(target, action);

            if (action.appliesStun)
                ApplyStun(target, action.buffTurn);

            // GDD 4.1: hit by attack -> +1 Energy, always applies (even through Stun).
            if (dealtDamage)
                target.energy = Mathf.Min(MaxEnergy, target.energy + 1);
        }
    }

    private void ApplyStatModBuff(CombatUnit target, CombatActionSO action)
    {
        // GDD 8: no stacking — a reapplication refreshes rather than stacks.
        for (int i = target.activeStatuses.Count - 1; i >= 0; i--)
        {
            if (target.activeStatuses[i].sourceAction == action)
            {
                RevertStatus(target, target.activeStatuses[i]);
                target.activeStatuses.RemoveAt(i);
            }
        }

        // GDD 8: Speed buffs/debuffs are % based; Attack/Defense flat for now.
        float speedDelta = target.speed * action.buffValue.combatSpeed;

        var status = new ActiveStatus
        {
            type = StatusType.StatMod,
            sourceAction = action,
            remainingTurns = action.buffTurn,
            attackDelta = action.buffValue.combatAttack,
            defenseDelta = action.buffValue.combatDefense,
            speedDelta = speedDelta
        };

        target.attack += status.attackDelta;
        target.defense += status.defenseDelta;
        if (status.speedDelta != 0f)
            target.ChangeSpeed(target.speed + status.speedDelta);

        target.activeStatuses.Add(status);
    }

    private void ApplyStun(CombatUnit target, int duration)
    {
        for (int i = target.activeStatuses.Count - 1; i >= 0; i--)
        {
            if (target.activeStatuses[i].type == StatusType.Stun)
                target.activeStatuses.RemoveAt(i); // refresh, not stack
        }

        target.activeStatuses.Add(new ActiveStatus
        {
            type = StatusType.Stun,
            remainingTurns = duration
        });
    }

    private void TickStatuses(CombatUnit unit)
    {
        // GDD 8: duration ticks at the start of the unit's own turn.
        for (int i = unit.activeStatuses.Count - 1; i >= 0; i--)
        {
            var status = unit.activeStatuses[i];
            status.remainingTurns--;

            if (status.remainingTurns <= 0)
            {
                RevertStatus(unit, status);
                unit.activeStatuses.RemoveAt(i);
            }
        }
    }

    private void RevertStatus(CombatUnit unit, ActiveStatus status)
    {
        if (status.type != StatusType.StatMod) return;

        unit.attack -= status.attackDelta;
        unit.defense -= status.defenseDelta;
        if (status.speedDelta != 0f)
            unit.ChangeSpeed(unit.speed - status.speedDelta);
    }

    private bool HasAffordableAction(CombatUnit unit)
    {
        foreach (var action in unit.source.combatActionList)
            if (action.energyCost <= unit.energy)
                return true;
        return false;
    }

    #endregion

    #region Win/Loss

    // ---------------------------------------------------------------------
    // Win / loss
    // ---------------------------------------------------------------------

    private bool CheckEncounterEnd()
    {
        bool playerAlive = false;
        foreach (var u in playerUnits) if (!u.IsDead) { playerAlive = true; break; }

        bool enemyAlive = false;
        foreach (var u in enemyUnits) if (!u.IsDead) { enemyAlive = true; break; }

        if (!enemyAlive) { EndCombat(true); return true; }
        if (!playerAlive) { EndCombat(false); return true; }
        return false;
    }

    private void EndCombat(bool victory)
    {
        isCombatActive = false;
        awaitingAction = false;
        awaitingTarget = false;
        HideAllIndicators();
        currentActor = null;

        if (victory)
        {
            foreach (var u in playerUnits)
                u.CommitToSource();
        }
        // Defeat outcome (game over / retry flow) is left for GameManager.

        OnCombatEnded?.Invoke(victory);
    }
    #endregion

    #region Flowchart Handler
    private static readonly string[] OptionVariables =
    {
        "OptionA",
        "OptionB",
        "OptionC",
        "OptionD"
    };

    private static readonly string[] InteractableVariables =
    {
        "InteractableA",
        "InteractableB",
        "InteractableC",
        "InteractableD"
    };

    public void SetActionOption(CombatUnit unit, List<CombatActionSO> actionList)
    {
        for (int i = 0; i < OptionVariables.Length; i++)
        {
            bool hasAction = i < actionList.Count;

            combatFlowchart.SetStringVariable(
                OptionVariables[i],
                hasAction
                    ? ActionStringBuilder(actionList[i].name, actionList[i].energyCost)
                    : "Unavailable");

            combatFlowchart.SetBooleanVariable(
                InteractableVariables[i],
                hasAction &&
                unit.energy >= actionList[i].energyCost);
        }
    }

    private static string ActionStringBuilder(string name, int energy)
    {
        return $"{name}\n E:{energy}";
    }

    #endregion
}