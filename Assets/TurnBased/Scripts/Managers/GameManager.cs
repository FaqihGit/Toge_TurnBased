using System;
using Fungus;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;

public enum GameState
{
    Exploration,
    DialogueCutscene,
    Cutscene,
    Combat,
    Pause
}

public class GameManager : MonoBehaviour
{

    [Header("Asset References")]
    [SerializeField] private Material platformMat;
    private static readonly int ColorId = Shader.PropertyToID("_BaseColor");

    [Header("Current State (read-only at runtime)")]
    [SerializeField] private GameState currentState = GameState.Exploration;
    private GameState previousState;
    public bool IsExploration => currentState == GameState.Exploration;
    public bool IsDialogueOrCutscene => currentState == GameState.DialogueCutscene || currentState == GameState.Cutscene;
    public bool IsCombat => currentState == GameState.Combat;
    public bool IsPause => currentState == GameState.Pause;

    [Header("Level References")]
    [SerializeField] private CameraTransitionController cameraTransitionController;
    [SerializeField] private CombatManager combat;
    [SerializeField] private CutsceneManager cutscene;
    [SerializeField] private CanvasManager canvas;
    [SerializeField] private PlayerManager player;
    private PlayerInputAction playerControls;

    [Header("Escape Hold Tracking")]
    [SerializeField] private float EscapeMaxPressTime = .2f;
    [SerializeField] private float EscapeHoldTime = .7f;
    private bool isTrackingEscapeHold;
    private bool isExpectingEscapeInput;
    private float escapeHoldStartTime;
    private float trackedHoldEscapeTime;

    [Header("DEBUGS")]
    [SerializeField] private GameState targetGameStateDebug;

    private void Start()
    {
        Init();
    }

    private void Init()
    {
        playerControls = new();
        playerControls.General.Enable();
        playerControls.General.Escape.started += OnInputEscapeStarted;
        // playerControls.General.Escape.performed += OnInputEscape;
        playerControls.General.Escape.canceled += OnInputEscapeCanceled;

        SetPlatformMatsAlpha(1);
        EnterState(currentState);

        combat.Init(playerControls, canvas.combatCanvas);
        combat.OnCombatEnded += HandleOnCombatEnd;
        combat.OnEscaped += HandleOnCombatEscaped;

        cameraTransitionController.Init(currentState);

        player.Init(playerControls);
        player.OnPlayerInteracted = (isInteracting) => HandleOnPlayerInteracted(isInteracting);
        player.OnPlayerInteractableUpdate = (interactable) => HandleOnPlayerInteractableUpdate(interactable);
        player.OnPlayerTriggerCombat = (enemyParty) => HandleOnPlayerEnterCombat(enemyParty);

        cutscene.Init(player.exploration);
        cutscene.OnCutsceneStarted += HandleOnCutsceneStarted;
        cutscene.OnCutsceneEnded += HandleOnCutsceneEnded;

        canvas.Init(playerControls, cameraTransitionController.mainCamera, cutscene);
        canvas.OnMenuOptionSelection += HandleOnCanvasMenuOptionSelection;
        canvas.OnPauseResumeButtonClicked += HandleOnCanvasResumeButtonClicked;
    }

    private void Update()
    {
        if (isExpectingEscapeInput)
        {
            if (isTrackingEscapeHold)
            {
                trackedHoldEscapeTime = Time.time - escapeHoldStartTime;

                HoldingEscape(Mathf.Clamp01(trackedHoldEscapeTime / EscapeHoldTime));

                // LogMessage($"elapsed {trackedHoldEscapeTime} escapeHoldDurationSeconds {EscapeHoldTime}");
                if (trackedHoldEscapeTime >= EscapeHoldTime)
                {
                    isTrackingEscapeHold = false;
                    isExpectingEscapeInput = false;
                    HandleEscapeHold();
                }
            }
            else
            {
                if (trackedHoldEscapeTime <= EscapeMaxPressTime)
                {
                    isTrackingEscapeHold = false;
                    HandleEscapeTap();
                }

                isExpectingEscapeInput = false;
            }

        }
    }

    #region Escape Input (tap = skip/pause, hold = escape request)
    private void OnInputEscapeStarted(InputAction.CallbackContext ctx)
    {
        escapeHoldStartTime = Time.time;
        isTrackingEscapeHold = true;
        isExpectingEscapeInput = true;
    }

    // private void OnInputEscape(InputAction.CallbackContext ctx)
    // {
    //     HoldingEscape(0f);

    //     if (ctx.interaction is HoldInteraction)
    //     {
    //         HandleEscapeHold();
    //     }
    //     else
    //         HandleEscapeTap();
    // }

    private void OnInputEscapeCanceled(InputAction.CallbackContext ctx)
    {
        isTrackingEscapeHold = false;
        HoldingEscape(0f);
    }

    private void HoldingEscape(float progress)
    {
        // LogMessage($"HoldingEscape {progress}");
        canvas.SetEscapeHoldProgress(progress);
    }

    private void HandleEscapeTap()
    {
        switch (currentState)
        {
            case GameState.Combat:
                if (combat.isAwaitingConfirmation) canvas.combatCanvas.onConfirmNoAction?.Invoke();
                else combat.RequestSkipConfirm();
                break;

            case GameState.Pause:
                ExitPause();
                break;

            default:
                EnterPause();
                break;
        }
    }

    private void HandleEscapeHold()
    {
        switch (currentState)
        {
            case GameState.Combat:
                combat.RequestEscapeConfirm();
                break;

            default:
                // No hold behavior defined outside combat yet.
                break;
        }
    }

    private void EnterPause()
    {
        ChangeState(GameState.Pause);
        canvas.ShowPauseMenu(true);
    }

    private void ExitPause()
    {
        if (currentState != GameState.Pause) return;

        canvas.ShowPauseMenu(false);
        RevertToPreviousState();
    }

    #endregion

    #region Event Handlers
    private void HandleOnPlayerInteracted(bool isInteracting)
    {
        canvas.worldCanvas.ShowInteractablePrompt(false);
        // LogMessage($"HandleOnPlayerInteracted {isInteracting}");
        if (isInteracting)
        {
            ChangeState(GameState.DialogueCutscene);
        }
        else
        {
            if (currentState == GameState.DialogueCutscene)
            {
                RevertToPreviousState();
            }
        }
    }

    private void HandleOnPlayerInteractableUpdate(Interactables interactable)
    {
        if (interactable != null)
        {
            canvas.worldCanvas.ShowInteractablePrompt(true, interactable.canvasTarget);
        }
        else
        {
            canvas.worldCanvas.ShowInteractablePrompt(false);
        }
    }

    private void HandleOnPlayerEnterCombat(CombatPartyHandler enemyParty)
    {
        ChangeState(GameState.Combat);
        combat.StartCombat(player.playerParty, enemyParty);
    }

    private void HandleOnCombatEnd(bool isVictory)
    {

        ChangeState(GameState.Exploration);
    }

    private void HandleOnCombatEscaped()
    {

        ChangeState(GameState.Exploration);
    }

    private void HandleOnCutsceneStarted()
    {
        ChangeState(GameState.Cutscene);
    }

    private void HandleOnCutsceneEnded()
    {
        ChangeState(GameState.Exploration);
    }

    private void HandleOnCanvasMenuOptionSelection(int optionIdx)
    {
        combat.SetActionSelection(optionIdx);
    }

    private void HandleOnCanvasResumeButtonClicked()
    {
        ExitPause();
    }
    #endregion

    private void SetPlatformMatsAlpha(float alpha)
    {
        if (platformMat == null) return;

        var color = platformMat.GetColor(ColorId);
        color.a = alpha;
        platformMat.SetColor(ColorId, color);
    }

    #region State Handlers
    /// <summary>Primary entry point for switching game state.</summary>
    public void ChangeState(GameState newState)
    {
        if (newState == currentState)
            return;

        GameState oldState = currentState;

        ExitState(oldState);

        previousState = oldState;
        currentState = newState;

        EnterState(newState);

        LogMessage($"ChangeState {previousState} => {currentState}");
        cameraTransitionController.HandleStateChanged(oldState, newState);
    }

    [ContextMenu("DEBUG/ChangeStateDebug")]
    private void ChangeState_Debug()
    {
        ChangeState(targetGameStateDebug);
    }

    /// <summary>
    /// Convenience for systems ending their own flow (e.g. a dialogue finishing) that just
    /// want to hand control back to whatever was happening before they took over.
    /// </summary>
    public void RevertToPreviousState()
    {
        ChangeState(previousState);
    }

    private void EnterState(GameState state)
    {
        EnablePlayerInput(state);

        switch (state)
        {
            case GameState.Exploration:
                break;

            case GameState.DialogueCutscene:
                break;

            case GameState.Cutscene:
                break;

            case GameState.Combat:
                SetPlatformMatsAlpha(0);
                break;

            case GameState.Pause:
                break;
        }
    }

    private void ExitState(GameState state)
    {
        SetPlatformMatsAlpha(1);
        DisablePlayerInput(state);

        switch (state)
        {
            case GameState.Exploration:
                break;

            case GameState.DialogueCutscene:
                break;

            case GameState.Cutscene:
                break;

            case GameState.Combat:
                canvas.ShowFungus(false);
                break;

            case GameState.Pause:
                break;
        }
    }
    #endregion

    #region Player Input

    private void EnablePlayerInput(GameState state) => GetMap(state)?.Enable();
    private void DisablePlayerInput(GameState state) => GetMap(state)?.Disable();

    private InputActionMap GetMap(GameState state)
    {
        return state switch
        {
            GameState.Exploration => playerControls.Exploration.Get(),
            GameState.DialogueCutscene => playerControls.Dialogue.Get(),
            GameState.Combat => playerControls.Combat.Get(),
            GameState.Cutscene => null, // intentional: no map enabled, player has zero input during playback
            GameState.Pause => null,    // intentional: all gameplay input disabled while paused
            _ => null,
        };
    }
    #endregion

    private void LogMessage(string msg)
    {
        Debug.Log($"[GameManager] {msg}");
    }
}