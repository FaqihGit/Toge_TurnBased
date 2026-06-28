using System;
using Fungus;
using UnityEngine;
using UnityEngine.InputSystem;

public enum GameState
{
    Exploration,
    DialogueCutscene,
    Combat
}

public class GameManager : MonoBehaviour
{

    [Header("Current State (read-only at runtime)")]
    [SerializeField] private GameState currentState = GameState.Exploration;
    private GameState previousState;
    public bool IsExploration => currentState == GameState.Exploration;
    public bool IsDialogueOrCutscene => currentState == GameState.DialogueCutscene;
    public bool IsCombat => currentState == GameState.Combat;

    [Header("Level References")]
    [SerializeField] private CameraTransitionController cameraTransitionController;
    [SerializeField] private CombatManager combat;
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
        playerControls.General.Escape.performed += OnEscapePerformed;

        EnterState(currentState);

        combat.Init(playerControls, canvas.combatCanvas);
        cameraTransitionController.Init(currentState);
        player.Init(playerControls);
        player.OnPlayerInteracted = (isInteracting) => HandleOnPlayerInteracted(isInteracting);
        player.OnPlayerTriggerCombat = (enemyParty) => HandleOnCombatEntered(enemyParty);

        canvas.Init(playerControls, Camera.main);
    }

    private void OnEscapePerformed(InputAction.CallbackContext ctx)
    {
        // if (IsExploration)
        // {
        //     ChangeState(GameState.Combat);
        // }
        // else if (IsCombat)
        // {
        //     ChangeState(GameState.Exploration);
        // }
    }

    private void HandleOnPlayerInteracted(bool isInteracting)
    {
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

    private void HandleOnCombatEntered(CombatPartyHandler enemyParty)
    {
        ChangeState(GameState.Combat);
        combat.StartCombat(player.playerParty, enemyParty);
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
    /// Convenience for systems ending their own flow (e.g. a cutscene finishing) that just
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

            case GameState.Combat:
                break;
        }
    }

    private void ExitState(GameState state)
    {
        DisablePlayerInput(state);

        switch (state)
        {
            case GameState.Exploration:
                break;

            case GameState.DialogueCutscene:
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
            _ => null,
        };
    }
    #endregion

    private void LogMessage(string msg)
    {
        Debug.Log($"[GameManager] {msg}");
    }
}