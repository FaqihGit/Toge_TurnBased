using System;
using System.Collections.Generic;
using Fungus;
using UnityEngine;

public class CombatManager : MonoBehaviour
{

    [SerializeField] private Flowchart combatFlowchart;

    private const int MaxEnergy = 10;

    private readonly List<CombatUnit> playerUnits = new();
    private readonly List<CombatUnit> enemyUnits = new();
    private readonly List<CombatUnit> allUnits = new();
    private readonly List<CombatUnitSnapshot> preCombatSnapshots = new();

    private readonly CombatResolver combatResolver = new(MaxEnergy);
    private readonly CombatTargetSelector targetSelector = new();
    private readonly EnemyCombatAI enemyAI = new();

    [Header("Enemy AI")]
    [Tooltip("Pure pacing — lets the player read the enemy's turn before it resolves. 0 = instant.")]
    [SerializeField] private float enemyActionDelay = 0.6f;

    private float turnThreshold;
    private CombatUnit currentActor;
    private bool isCombatActive;
    private bool awaitingAction;

    // ---------------------------------------------------------------------
    // Targeting sub-phase (visual selection of a target for the chosen action)
    // is owned by targetSelector — see CombatTargetSelector.cs.
    // ---------------------------------------------------------------------
    private CombatPartyHandler playerPartyHandler;
    private CombatPartyHandler enemyPartyHandler;

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
    private CombatCanvasManager combatCanvas;

    void OnEnable()
    {
        SubscribeControls(true);
    }

    void OnDisable()
    {
        SubscribeControls(false);
    }

    public void Init(PlayerInputAction playerControls, CombatCanvasManager combatCanvas)
    {
        this.playerControls = playerControls;
        this.combatCanvas = combatCanvas;

        targetSelector.Init(playerControls);
        targetSelector.OnTargetsConfirmed += HandleTargetsConfirmed;
        targetSelector.OnTargetingCancelled += HandleTargetingCancelled;

        SubscribeControls(true);
    }

    private void SubscribeControls(bool isSubscribe)
    {
        if (playerControls == null) return;
        targetSelector.SubscribeControls(isSubscribe);
    }

    public void StartCombat(CombatPartyHandler playerParty, CombatPartyHandler enemyParty)
    {
        playerPartyHandler = playerParty;
        enemyPartyHandler = enemyParty;

        enemyPartyHandler.ShowParty(true, playerPartyHandler.WorldMemberPosition.x);

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

        combatCanvas.BindParty(playerUnits, playerPartyHandler, MaxEnergy);
        combatCanvas.BindParty(enemyUnits, enemyPartyHandler, MaxEnergy);

        targetSelector.BindParties(playerUnits, enemyUnits, playerPartyHandler, enemyPartyHandler);

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
        LogMessage($"StartTurn name {actor.source.name}");
        currentActor = actor;

        bool wasStunned = actor.IsStunned;

        if (!wasStunned)
            actor.energy = Mathf.Min(MaxEnergy, actor.energy + 1);

        combatCanvas.RefreshUnit(actor, MaxEnergy);

        combatResolver.TickStatuses(actor);

        OnTurnStarted?.Invoke(actor);

        LogMessage($"StartTurn wasStunned {wasStunned}");
        if (wasStunned)
        {
            // GDD 8: Stun skips the turn completely, no action menu.
            EndTurn();
            return;
        }

        awaitingAction = true;

        LogMessage($"StartTurn PromptAction {currentActor.source.name}");
        PromptAction();
    }

    private void PromptAction()
    {
        LogMessage($"PromptAction {currentActor.source.name} {currentActor.faction}");
        if (currentActor.faction == UnitFactionEnum.Enemies)
        {
            StartCoroutine(EnemyTurnRoutine());
            return;
        }

        // Highlight whose turn it is on their own party handler while the
        // action menu is open. Reused for every action this turn (GDD 1:
        // turn = multi-action window), not just the first.
        var actorHandler = currentActor.faction == UnitFactionEnum.Player ? playerPartyHandler : enemyPartyHandler;
        int actorIdx = targetSelector.GetPartySlotIndex(currentActor);

        HideAllIndicators();
        actorHandler.SetIndicator(actorIdx);
        actorHandler.ShowIndicator(true);

        SetActionOption(currentActor, currentActor.source.combatActionList);
        combatFlowchart.ExecuteBlock("TriggerAction");
    }

    private void EndTurn()
    {
        awaitingAction = false;
        targetSelector.ForceClose();
        HideAllIndicators();
        currentActor = null;
        AdvanceTurn();
    }

    #endregion

    #region External Calls

    // ---------------------------------------------------------------------
    // External API — called by PlayerInputRouter, an AI controller, etc.
    // ---------------------------------------------------------------------

    /// targets is ignored for party-wide actions. For single/limited-target
    /// actions, pass a subset of GetValidTargets(currentActor, action).
    public bool SubmitAction(CombatActionSO action, List<CombatUnit> targets)
    {
        if (!isCombatActive || !awaitingAction || currentActor == null) return false;
        if (action == null || action.energyCost > currentActor.energy) return false;

        var validPool = GetValidTargets(currentActor, action);
        List<CombatUnit> resolvedTargets;

        if (CombatTargetSelector.IsPartyWide(action))
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

        LogMessage($"SubmitAction {currentActor.source.name} does {action.name}");
        currentActor.energy -= action.energyCost;
        combatCanvas.RefreshUnit(currentActor, MaxEnergy); // NEW
        combatResolver.ResolveAction(currentActor, action, resolvedTargets);

        // CombatResolver has no UI reference, so the caller (here) refreshes
        // every target it just resolved against.
        foreach (var target in resolvedTargets)
            combatCanvas.RefreshUnit(target, MaxEnergy);

        if (CheckEncounterEnd()) return true;

        if (!combatResolver.HasAffordableAction(currentActor))
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
        targetSelector.ForceClose();
        HideAllIndicators();
        enemyPartyHandler.ShowParty(false);
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

    #endregion

    #region Targeting

    // ---------------------------------------------------------------------
    // Target selection is delegated to targetSelector (CombatTargetSelector.cs)
    // — input subscriptions, cursor state, and indicator visuals all live
    // there. CombatManager only starts it and reacts to its two outcomes.
    // ---------------------------------------------------------------------

    private void BeginTargeting(CombatActionSO action)
    {
        var validTargets = GetValidTargets(currentActor, action);
        targetSelector.BeginTargeting(currentActor.faction, action, validTargets);
    }

    private void HandleTargetsConfirmed(CombatActionSO action, List<CombatUnit> targets)
    {
        SubmitAction(action, targets);
    }

    private void HandleTargetingCancelled()
    {
        PromptAction();
    }

    private void HideAllIndicators()
    {
        playerPartyHandler?.ShowIndicator(false);
        enemyPartyHandler?.ShowIndicator(false);
    }

    #endregion

    #region Enemy AI

    // ---------------------------------------------------------------------
    // Enemy turns are resolved by enemyAI (plain C# class, GDD §11). It only
    // receives a snapshot of state via arguments and returns a Decision —
    // CombatManager remains the only thing that calls SubmitAction/SubmitSkip
    // and the only thing that mutates combat state.
    // ---------------------------------------------------------------------

    private System.Collections.IEnumerator EnemyTurnRoutine()
    {
        if (enemyActionDelay > 0f)
            yield return new WaitForSeconds(enemyActionDelay);

        ResolveEnemyTurn();
    }

    private void ResolveEnemyTurn()
    {
        if (!isCombatActive || !awaitingAction || currentActor == null) return;

        var actor = currentActor;
        var allies = actor.faction == UnitFactionEnum.Player ? playerUnits : enemyUnits;

        var decision = enemyAI.DecideAction(
            actor,
            actor.source.combatActionList,
            allies,
            MaxEnergy,
            action => GetValidTargets(actor, action));

        if (decision.skip)
        {
            SubmitSkip();
            return;
        }

        if (!SubmitAction(decision.action, decision.targets))
        {
            // Defensive fallback — should not happen since the AI only chose
            // from actor's own affordable/valid pool, but never leave an enemy
            // turn stuck open.
            LogMessage($"EnemyCombatAI returned an action that SubmitAction rejected ({decision.action?.name}); skipping turn.");
            SubmitSkip();
        }
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
        targetSelector.ForceClose();
        HideAllIndicators();
        enemyPartyHandler.ShowParty(false);
        combatCanvas.ClearAll();
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
    public void SubmitFlowchartAction()
    {
        LogMessage($"SubmitFlowchartAction isCombatActive {isCombatActive} awaitingAction {awaitingAction} awaitingTarget {targetSelector.IsAwaitingTarget} currentActor {currentActor}");
        if (!isCombatActive || !awaitingAction || targetSelector.IsAwaitingTarget || currentActor == null) return;

        int actionIdx = combatFlowchart.GetIntegerVariable("ActionIdx");
        var actionList = currentActor.source.combatActionList;
        LogMessage($"SubmitFlowchartAction actionIdx {actionIdx}");
        if (actionIdx < 0 || actionIdx >= actionList.Count) return;

        var action = actionList[actionIdx];
        LogMessage($"SubmitFlowchartAction action.energyCost {action.energyCost} currentActor.energy {currentActor.energy}");
        if (action.energyCost > currentActor.energy) return;

        BeginTargeting(action);
    }

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

    private void LogMessage(string msg)
    {
        Debug.Log($"[CombatManager] {msg}");
    }
}