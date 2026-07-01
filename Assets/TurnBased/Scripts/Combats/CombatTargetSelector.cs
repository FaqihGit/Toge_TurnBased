using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owns the targeting sub-phase: cursor navigation over a candidate list,
/// multi-target confirmation, and cancel — plus the input subscriptions and
/// indicator visuals that drive it. No knowledge of turn order, energy, or
/// damage; it only knows "here are some candidates, tell me what got picked."
///
/// CombatManager pushes the input reference (Init) and the current party
/// data (BindParties) into this class, then drives it through BeginTargeting
/// and reacts to OnTargetsConfirmed / OnTargetingCancelled — the same
/// push-in / event-out pattern used for PlayerInputRouter and CombatResolver.
/// </summary>
public class CombatTargetSelector
{
    private PlayerInputAction playerControls;
    private CombatCanvasManager combatCanvas;

    private CombatPartyHandler playerPartyHandler;
    private CombatPartyHandler enemyPartyHandler;
    private IReadOnlyList<CombatUnit> playerUnits;
    private IReadOnlyList<CombatUnit> enemyUnits;

    private CombatActionSO pendingAction;
    private UnitFactionEnum actingFaction;
    private CombatPartyHandler targetingHandler;
    private readonly List<CombatUnit> targetCandidates = new();
    private readonly List<CombatUnit> selectedTargets = new();
    private int targetCursorIndex;
    private bool awaitingTarget;

    // Edge-detection for stick input, matching the deadzone pattern used in
    // CanvasManager/NavigableMenuDialog — stick must return to neutral before
    // another move registers, so holding a direction doesn't spam the cursor.
    private bool targetCursorAxisReleased = true;
    private int targetingOpenedFrame = -1;

    public bool IsAwaitingTarget => awaitingTarget;

    /// Fired once a target set is finalized — manual confirm, or immediately
    /// for party-wide actions which need no cursor at all.
    public event Action<CombatActionSO, List<CombatUnit>> OnTargetsConfirmed;

    /// Fired when targeting can't proceed or is explicitly cancelled — either
    /// way the listener's job is the same: go back to the action menu.
    public event Action OnTargetingCancelled;

    public void Init(PlayerInputAction playerControls, CombatCanvasManager combatCanvas) // signature change
    {
        this.playerControls = playerControls;
        this.combatCanvas = combatCanvas;
    }

    public void BindParties(IReadOnlyList<CombatUnit> playerUnits, IReadOnlyList<CombatUnit> enemyUnits,
        CombatPartyHandler playerPartyHandler, CombatPartyHandler enemyPartyHandler)
    {
        this.playerUnits = playerUnits;
        this.enemyUnits = enemyUnits;
        this.playerPartyHandler = playerPartyHandler;
        this.enemyPartyHandler = enemyPartyHandler;
    }

    public void SubscribeControls(bool isSubscribe)
    {
        if (playerControls == null) return;

        playerControls.Combat.Selection.performed -= OnSelection;
        playerControls.Combat.Selection.canceled -= OnSelectionReleased;
        playerControls.Combat.Select.performed -= OnSelect;
        playerControls.General.Escape.performed -= OnCancelTarget;

        if (isSubscribe)
        {
            playerControls.Combat.Selection.performed += OnSelection;
            playerControls.Combat.Selection.canceled += OnSelectionReleased;
            playerControls.Combat.Select.performed += OnSelect;
            playerControls.General.Escape.performed += OnCancelTarget;
        }
    }

    /// targetCount <= 0 or >= 4 means "whole side" (GDD 2: 4-unit party cap),
    /// which skips manual cursor selection entirely.
    public static bool IsPartyWide(CombatActionSO action) => action.targetCount <= 0 || action.targetCount >= 4;

    /// validTargets is the actor's legal target pool (already computed by the
    /// caller, since it needs that same pool for SubmitAction regardless).
    public void BeginTargeting(UnitFactionEnum actorFaction, CombatActionSO action, List<CombatUnit> validTargets)
    {
        pendingAction = action;
        actingFaction = actorFaction;
        targetCandidates.Clear();
        targetCandidates.AddRange(validTargets);

        if (targetCandidates.Count == 0)
        {
            // No legal targets right now — bounce back to the action menu.
            pendingAction = null;
            OnTargetingCancelled?.Invoke();
            return;
        }

        if (IsPartyWide(action))
        {
            // GDD 6: "All" scope needs no manual target selection.
            ConfirmTargetingResult(targetCandidates);
            return;
        }

        targetingHandler = GetTargetHandler(actorFaction, action);
        selectedTargets.Clear();
        targetCursorIndex = 0;
        targetCursorAxisReleased = true;

        targetingHandler.ShowSelectionAll(false);
        combatCanvas.SetIndicator(targetCandidates[targetCursorIndex]);
        combatCanvas.ShowIndicator(true);

        awaitingTarget = true;
        targetingOpenedFrame = Time.frameCount;
    }

    /// Resets targeting state without firing OnTargetingCancelled — for
    /// CombatManager to call when combat itself is ending/escaping and a
    /// trip back to the action menu would be wrong.
    public void ForceClose()
    {
        awaitingTarget = false;
        pendingAction = null;
        selectedTargets.Clear();

        if (targetingHandler != null)
            targetingHandler.ShowSelectionAll(false);

        HideAllIndicators();
    }

    /// "Where does this unit live on its party's canvas/handler" — used both
    /// for the targeting cursor here and by CombatManager for the turn
    /// indicator, so it lives in one place rather than two copies.
    public int GetPartySlotIndex(CombatUnit unit)
    {
        int idx = IndexOf(playerUnits, unit);
        if (idx >= 0) return idx;
        return IndexOf(enemyUnits, unit);
    }

    private static int IndexOf(IReadOnlyList<CombatUnit> list, CombatUnit unit)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] == unit) return i;
        return -1;
    }

    private void ConfirmTargetingResult(List<CombatUnit> targets)
    {
        var action = pendingAction;
        pendingAction = null;
        awaitingTarget = false;

        if (targetingHandler != null)
            targetingHandler.ShowSelectionAll(false);
        HideAllIndicators();

        OnTargetsConfirmed?.Invoke(action, targets);
    }

    private void CancelTargeting()
    {
        awaitingTarget = false;

        if (targetingHandler != null)
            targetingHandler.ShowSelectionAll(false);

        pendingAction = null;
        selectedTargets.Clear();
        HideAllIndicators();

        OnTargetingCancelled?.Invoke();
    }

    private CombatPartyHandler GetTargetHandler(UnitFactionEnum actorFaction, CombatActionSO action)
    {
        bool actorIsPlayer = actorFaction == UnitFactionEnum.Player;
        bool targetsAllies = action.target == CombatTargetEnum.Allies;

        // Mirrors the pool selection in CombatManager.GetValidTargets.
        bool targetsPlayerSide = targetsAllies ? actorIsPlayer : !actorIsPlayer;
        return targetsPlayerSide ? playerPartyHandler : enemyPartyHandler;
    }

    private void HideAllIndicators()
    {
        combatCanvas.ShowIndicator(false);
    }

    private void OnSelectionReleased(InputAction.CallbackContext context)
    {
        targetCursorAxisReleased = true;
    }

    private void OnSelection(InputAction.CallbackContext context)
    {
        if (!awaitingTarget || targetCandidates.Count == 0) return;
        if (!targetCursorAxisReleased) return;

        var selectionInput = context.ReadValue<Vector2>();

        int direction = 0;
        if (selectionInput.y > 0.5f || selectionInput.x < -0.5f) direction = 1;
        else if (selectionInput.y < -0.5f || selectionInput.x > 0.5f) direction = -1;

        if (direction == 0) return;

        targetCursorAxisReleased = false;

        int count = targetCandidates.Count;
        targetCursorIndex = ((targetCursorIndex + direction) % count + count) % count;

        combatCanvas.SetIndicator(targetCandidates[targetCursorIndex]);
    }

    private void OnSelect(InputAction.CallbackContext context)
    {
        if (!awaitingTarget || targetCandidates.Count == 0) return;
        if (Time.frameCount == targetingOpenedFrame) return; // ignore the confirm that just opened targeting

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
}