using System;
using UnityEngine;
using UnityEngine.InputSystem;


public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState
    {
        Exploration,
        DialogueCutscene,
        Combat
    }

    /// <summary>Fired whenever the state changes. Subscribers receive (oldState, newState).</summary>
    public event Action<GameState, GameState> OnStateChanged;
    [Header("Current State (read-only at runtime)")]
    [SerializeField] private GameState currentState = GameState.Exploration;
    public GameState CurrentState => currentState;
    private GameState previousState;
    public bool IsExploration => currentState == GameState.Exploration;
    public bool IsDialogueOrCutscene => currentState == GameState.DialogueCutscene;
    public bool IsCombat => currentState == GameState.Combat;

    [Header("Level References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private CanvasManager canvas;
    [SerializeField] private PlayerManager player;
    private PlayerInputAction playerControls;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        // Uncomment if GameManager needs to survive scene loads:
        // DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        Init();
    }

    private void Init()
    {
        playerControls = new();

        // Run entry logic once at startup so anything that subscribed in its own Start()
        // is still correctly initialized for the starting state.
        EnterState(currentState);

        player.Init(playerControls);
        canvas.Init(playerControls);
    }

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

        OnStateChanged?.Invoke(oldState, newState);
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

    // ---- Convenience wrappers ----------------------------------------------------------
    // Handy for hooking up directly to UnityEvents in the Inspector (e.g. a trigger collider's
    // OnTriggerEnter UnityEvent, or a dialogue node's "on end" callback) without needing the
    // enum exposed in the Editor dropdown.
    public void EnterExploration() => ChangeState(GameState.Exploration);
    public void EnterDialogueCutscene() => ChangeState(GameState.DialogueCutscene);
    public void EnterCombat() => ChangeState(GameState.Combat);

    #region Player Input

    private void EnablePlayerInput(GameManager.GameState state) => GetMap(state)?.Enable();
    private void DisablePlayerInput(GameManager.GameState state) => GetMap(state)?.Disable();

    private InputActionMap GetMap(GameManager.GameState state)
    {
        return state switch
        {
            GameManager.GameState.Exploration => playerControls.Exploration.Get(),
            GameManager.GameState.DialogueCutscene => playerControls.Dialogue.Get(),
            GameManager.GameState.Combat => playerControls.Combat.Get(),
            _ => null,
        };
    }
    #endregion
}