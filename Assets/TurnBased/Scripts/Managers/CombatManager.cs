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

    private readonly CombatResolver combatResolver = new(MaxEnergy);
    private readonly CombatTargetSelector targetSelector = new();
    private readonly EnemyCombatAI enemyAI = new();
    private readonly CombatTurnOrder turnOrder = new();
    private readonly CombatFlowchartHandler flowchartHandler = new();
    private readonly CombatLifecycleController lifecycle = new();

    [Header("Enemy AI")]
    [Tooltip("Pure pacing — lets the player read the enemy's turn before it resolves. 0 = instant.")]
    [SerializeField] private float enemyActionDelay = 0.6f;
    [Tooltip("Pause after impact so the shake/return-thrust reads before continuing.")]
    [SerializeField] private float _postActionSettle = 0.15f;

    private CombatUnit currentActor;
    private bool isCombatActive;
    private bool awaitingAction;
    private bool isResolvingAction;

    private CombatPartyHandler playerPartyHandler;
    private CombatPartyHandler enemyPartyHandler;

    public CombatUnit CurrentActor => currentActor;
    public IReadOnlyList<CombatUnit> PlayerUnits => playerUnits;
    public IReadOnlyList<CombatUnit> EnemyUnits => enemyUnits;
    public bool IsCombatActive => isCombatActive;


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

        targetSelector.Init(playerControls, combatCanvas);
        targetSelector.OnTargetsConfirmed += HandleTargetsConfirmed;
        targetSelector.OnTargetingCancelled += HandleTargetingCancelled;

        flowchartHandler.Init(combatFlowchart);
        flowchartHandler.OnActionChosen += BeginTargeting;

        lifecycle.OnCombatEnded += HandleCombatEnded;
        lifecycle.OnEscaped += HandleEscaped;

        combatResolver.OnSpeedChanged += RefreshTurnOrderDisplay;

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
        lifecycle.CaptureSnapshots(playerUnits);

        // GDD 3.1: threshold = average Speed of all living units, fixed for
        // the whole encounter.
        turnOrder.Init(allUnits);

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
    // Turn order — Speed Bank (GDD 3) computation lives in turnOrder;
    // this just starts whatever it returns.
    // ---------------------------------------------------------------------

    private void AdvanceTurn()
    {
        var next = turnOrder.GetNextActor(allUnits);
        if (next != null)
            StartTurn(next);
    }

    private void StartTurn(CombatUnit actor)
    {
        LogMessage($"StartTurn name {actor.source.name}");
        currentActor = actor;

        RefreshTurnOrderDisplay();

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

        combatCanvas.SetIndicator(currentActor);
        combatCanvas.ShowIndicator(true);

        SetActionSelection(0);
        flowchartHandler.SetActionOption(currentActor, currentActor.source.combatActionList);
        flowchartHandler.TriggerActionBlock();
    }

    private void RefreshTurnOrderDisplay()
    {
        int maxShown = combatCanvas.MaxTurnOrderShown;
        var display = new List<CombatUnit>(maxShown);

        if (currentActor != null)
            display.Add(currentActor);

        int remaining = maxShown - display.Count;
        if (remaining > 0)
            display.AddRange(turnOrder.PeekOrder(allUnits, remaining));

        combatCanvas.RefreshTurnOrder(display);
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
        if (!isCombatActive || !awaitingAction || isResolvingAction || currentActor == null) return false;
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
        combatCanvas.RefreshUnit(currentActor, MaxEnergy);
        HideAllIndicators();

        combatCanvas.ShowAction(currentActor, action, resolvedTargets);

        isResolvingAction = true;
        StartCoroutine(PlayActionAndResolve(currentActor, action, resolvedTargets));

        return true;
    }

    private System.Collections.IEnumerator PlayActionAndResolve(
    CombatUnit actor, CombatActionSO action, List<CombatUnit> resolvedTargets)
    {
        var actorParty = actor.faction == UnitFactionEnum.Player ? playerPartyHandler : enemyPartyHandler;
        var actorList = actor.faction == UnitFactionEnum.Player ? playerUnits : enemyUnits;
        bool actorThrustsBackward = actor.faction == UnitFactionEnum.Enemies;
        int actorIdx = actorList.IndexOf(actor);

        bool impactReached = false;
        actorParty.PlayAttackThrust(actorIdx, actorThrustsBackward, onPeak: () => impactReached = true);

        yield return new WaitUntil(() => impactReached);

        // Damage/heal/buff math lands exactly when the thrust hits its peak.
        combatResolver.ResolveAction(actor, action, resolvedTargets);
        combatCanvas.RefreshUnit(actor, MaxEnergy);

        foreach (var target in resolvedTargets)
        {
            combatCanvas.RefreshUnit(target, MaxEnergy);

            if (target == actor) continue; // avoid fighting the thrust tween on self-target

            var targetList = target.faction == UnitFactionEnum.Player ? playerUnits : enemyUnits;
            var targetParty = target.faction == UnitFactionEnum.Player ? playerPartyHandler : enemyPartyHandler;
            targetParty.PlayHitShake(targetList.IndexOf(target));
        }

        // Let the return-thrust and shake read before the menu reopens / turn advances.
        yield return new WaitForSeconds(Mathf.Max(0f, _postActionSettle));

        isResolvingAction = false;

        if (CheckEncounterEnd()) yield break;

        if (!combatResolver.HasAffordableAction(actor))
            EndTurn();
        else
            PromptAction();
    }

    public void SubmitSkip()
    {
        if (!isCombatActive || !awaitingAction || isResolvingAction) return;
        combatCanvas.ShowSkip(currentActor);
        EndTurn();
    }

    /// GDD 10: always available, no cost, 100% success — see
    /// CombatLifecycleController.Escape for the rollback itself.
    public void RequestEscape()
    {
        if (!isCombatActive) return;
        lifecycle.Escape(playerUnits);
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

    public void SetActionSelection(int optionIdx)
    {
        if (
            currentActor == null
            || currentActor.source == null
            || currentActor.source.combatActionList == null
            || currentActor.source.combatActionList.Count <= 0
            || currentActor.source.combatActionList.Count <= optionIdx
            )
            return;

        var currentAction = currentActor.source.combatActionList[optionIdx];
        flowchartHandler.SetActionPrompt(CombatActionDescriptionBuilder.Build(currentActor, currentAction));
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
        combatCanvas.ShowIndicator(false);
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
    // Win/loss detection lives in lifecycle; this checks it and reacts.
    // ---------------------------------------------------------------------

    private bool CheckEncounterEnd()
    {
        return lifecycle.CheckEncounterEnd(playerUnits, enemyUnits);
    }

    private void HandleCombatEnded(bool victory)
    {
        isCombatActive = false;
        awaitingAction = false;
        targetSelector.ForceClose();
        HideAllIndicators();
        enemyPartyHandler.ShowParty(false);
        combatCanvas.ClearAll();
        currentActor = null;

        OnCombatEnded?.Invoke(victory);
    }

    private void HandleEscaped()
    {
        isCombatActive = false;
        awaitingAction = false;
        targetSelector.ForceClose();
        HideAllIndicators();
        enemyPartyHandler.ShowParty(false);
        currentActor = null;

        OnEscaped?.Invoke();
    }

    #endregion

    #region Flowchart Handler
    public void SubmitFlowchartAction()
    {
        bool canAct = isCombatActive && awaitingAction && !targetSelector.IsAwaitingTarget;
        flowchartHandler.SubmitFlowchartAction(canAct, currentActor);
    }

    #endregion

    private void LogMessage(string msg)
    {
        // Debug.Log($"[CombatManager] {msg}");
    }
}