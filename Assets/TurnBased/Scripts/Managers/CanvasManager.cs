using System;
using Fungus;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class CanvasManager : MonoBehaviour
{
    public Action<int> OnMenuOptionSelection;
    public Action OnPauseResumeButtonClicked;

    [SerializeField] private Image escapeHoldImage;

    [Header("Component References")]
    [SerializeField] private CombatCanvasManager _combatCanvas; public CombatCanvasManager combatCanvas => _combatCanvas;
    [SerializeField] private WorldCanvasManager _worldCanvas; public WorldCanvasManager worldCanvas => _worldCanvas;
    [SerializeField] private NavigableMenuDialog menuDialog;
    [SerializeField] private SayDialog sayDialog;
    [SerializeField] private DialogInput dialogInput;
    [SerializeField] private CutsceneLetterboxUI _cutsceneLetterbox;
    [SerializeField] private CombatResultPopupUI _combatEndPopup;
    [SerializeField] private PauseMenuUI _pauseMenu;

    private PlayerInputAction playerControls;
    private Vector2 selectionInput;
    private float previousSelectionY;

    void OnEnable()
    {
        SubscribeControls(true);
    }

    void OnDisable()
    {
        SubscribeControls(false);
    }

    public void Init(PlayerInputAction playerControls, Camera mainCam, CutsceneManager cutscene)
    {
        this.playerControls = playerControls;
        SubscribeControls(true);
        ShowFungus(false);

        _combatCanvas.Init(mainCam);
        _worldCanvas.Init(mainCam);
        _cutsceneLetterbox.Init();
        menuDialog.OnOptionSelection += HandleOnMenuOptionSelection;

        _combatEndPopup.Init();

        _pauseMenu.Init();
        _pauseMenu.OnResumeButtonClicked = HandleOnPauseResumeButtonClicked;
    }

    public void HandleGameStateChanged(GameState oldState, GameState newState)
    {
        _cutsceneLetterbox.HandleGameStateChanged(oldState, newState);
    }

    private void SubscribeControls(bool isSubscribe)
    {
        if (playerControls == null) return;

        playerControls.Dialogue.Selection.performed -= HandleOnSelection;
        playerControls.Dialogue.Selection.canceled -= HandleOnSelection;
        playerControls.Dialogue.Select.performed -= HandleOnSelect;

        playerControls.Combat.Selection.performed -= HandleOnSelectionCombat;
        playerControls.Combat.Selection.canceled -= HandleOnSelectionCombat;
        playerControls.Combat.Select.performed -= HandleOnSelectCombat;

        if (isSubscribe)
        {
            playerControls.Dialogue.Selection.performed += HandleOnSelection;
            playerControls.Dialogue.Selection.canceled += HandleOnSelection;
            playerControls.Dialogue.Select.performed += HandleOnSelect;

            playerControls.Combat.Selection.performed += HandleOnSelectionCombat;
            playerControls.Combat.Selection.canceled += HandleOnSelectionCombat;
            playerControls.Combat.Select.performed += HandleOnSelectCombat;
        }
    }

    public void ShowFungus(bool isShow)
    {
        sayDialog.SetActive(isShow);
        menuDialog.SetActive(isShow);
        if (!isShow)
        {
            sayDialog.Stop();
            menuDialog.Clear();
        }
    }

    private void HandleOnSelectionCombat(InputAction.CallbackContext context) => HandleOnSelection(context, true);
    private void HandleOnSelection(InputAction.CallbackContext context) => HandleOnSelection(context, false);
    private void HandleOnSelection(InputAction.CallbackContext context, bool isCombat)
    {
        if (isCombat && combatCanvas.isAwaitingConfirmation) return;

        AdvanceDialog();

        selectionInput = context.ReadValue<Vector2>();

        if (MenuDialog.ActiveMenuDialog is NavigableMenuDialog activeMenu)
        {
            float y = selectionInput.y;
            bool wasNeutral = Mathf.Abs(previousSelectionY) < activeMenu.SelectionDeadzone;
            bool isPastDeadzone = Mathf.Abs(y) >= activeMenu.SelectionDeadzone;

            // Edge-detect the deadzone crossing so a held stick doesn't fire
            // a move every frame the action is "performed" 
            if (wasNeutral && isPastDeadzone)
            {
                if (y > 0) activeMenu.SelectPreviousOption();
                else activeMenu.SelectNextOption();
            }

            previousSelectionY = y;
            return;
        }

        previousSelectionY = 0f;
    }

    private void HandleOnSelectCombat(InputAction.CallbackContext context) => HandleOnSelect(context, true);
    private void HandleOnSelect(InputAction.CallbackContext context) => HandleOnSelect(context, false);
    private void HandleOnSelect(InputAction.CallbackContext context, bool isCombat)
    {
        if (isCombat && combatCanvas.isAwaitingConfirmation) return;

        AdvanceDialog();

        if (MenuDialog.ActiveMenuDialog is NavigableMenuDialog activeMenu)
        {
            activeMenu.ConfirmSelection();
            return;
        }
    }

    private void HandleOnMenuOptionSelection(int idx)
    {
        OnMenuOptionSelection?.Invoke(idx);
    }

    private void HandleOnPauseResumeButtonClicked()
    {
        OnPauseResumeButtonClicked?.Invoke();
    }

    public void AdvanceDialog()
    {
        bool hasSay = SayDialog.ActiveSayDialog;
        bool hasMenu = MenuDialog.ActiveMenuDialog != null &&
                       MenuDialog.ActiveMenuDialog.IsActive();
        // LogMessage($"AdvanceDialog — hasSay={hasSay}, hasMenu={hasMenu}");

        if (!hasSay || hasMenu) return;
        dialogInput.SetNextLineFlag();
    }

    public void SetEscapeHoldProgress(float progress)
    {
        escapeHoldImage.fillAmount = progress;
        combatCanvas.SetEscapeProgress(progress);
    }

    public void ShowCombatResult(bool isShow, string prompt = null, Action callback = null)
    {
        _combatEndPopup.ShowPopup(isShow);
        if (isShow)
        {
            _combatEndPopup.SetPrompt(prompt);
            _combatEndPopup.OnConfirm = () =>
            {
                callback?.Invoke();
                _combatEndPopup.OnConfirm = null;
            };
        }
    }

    public void ShowPauseMenu(bool isShow)
    {
        _pauseMenu.ShowMenu(isShow);
    }

    private void LogMessage(string msg)
    {
        Debug.Log($"[CanvasManager] {msg}");
    }
}