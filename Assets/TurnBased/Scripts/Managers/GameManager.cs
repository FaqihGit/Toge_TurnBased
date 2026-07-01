using System;
using Fungus;
using UnityEngine;
using UnityEngine.InputSystem;

public enum GameState
{
    Exploration,
    DialogueCutscene,
    Cutscene,
    Combat
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

    [Header("Level References")]
    [SerializeField] private CameraTransitionController cameraTransitionController;
    [SerializeField] private CombatManager combat;
    [SerializeField] private CutsceneManager cutscene;
    [SerializeField] private CanvasManager canvas;
    [SerializeField] private PlayerManager player;
    private PlayerInputAction playerControls;


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
        playerControls.General.Escape.performed += OnInputEscape;

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
    }

    private void OnInputEscape(InputAction.CallbackContext ctx)
    {

    }

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
        canvas.worldCanvas.ShowInteractablePrompt(interactable != null, interactable == null ? null : interactable.canvasTarget);
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
            _ => null,
        };
    }
    #endregion

    private void LogMessage(string msg)
    {
        Debug.Log($"[GameManager] {msg}");
    }
}