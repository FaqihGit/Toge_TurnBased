using System;
using Fungus;
using UnityEngine;
using UnityEngine.InputSystem;

public class CanvasManager : MonoBehaviour
{
    [SerializeField] private CombatCanvasManager _combatCanvas; public CombatCanvasManager combatCanvas => _combatCanvas;
    [SerializeField] private WorldCanvasManager _worldCanvas; public WorldCanvasManager worldCanvas => _worldCanvas;
    [SerializeField] private NavigableMenuDialog menuDialog;
    [SerializeField] private SayDialog sayDialog;
    [SerializeField] private DialogInput dialogInput;
    [SerializeField] private CutsceneLetterboxUI _cutsceneLetterbox;

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

        _combatCanvas.Init(mainCam);
        _worldCanvas.Init(mainCam);
        _cutsceneLetterbox.Init(cutscene);
    }

    private void SubscribeControls(bool isSubscribe)
    {
        if (playerControls == null) return;

        playerControls.Dialogue.Selection.performed -= OnSelection;
        playerControls.Dialogue.Select.performed -= OnSelect;

        playerControls.Combat.Selection.performed -= OnSelection;
        playerControls.Combat.Select.performed -= OnSelect;

        if (isSubscribe)
        {
            playerControls.Dialogue.Selection.performed += OnSelection;
            playerControls.Dialogue.Select.performed += OnSelect;

            playerControls.Combat.Selection.performed += OnSelection;
            playerControls.Combat.Select.performed += OnSelect;
        }
    }

    private void OnSelection(InputAction.CallbackContext context)
    {
        AdvanceDialog();

        selectionInput = context.ReadValue<Vector2>();

        if (MenuDialog.ActiveMenuDialog is NavigableMenuDialog activeMenu)
        {
            float y = selectionInput.y;
            bool wasNeutral = Mathf.Abs(previousSelectionY) < activeMenu.SelectionDeadzone;
            bool isPastDeadzone = Mathf.Abs(y) >= activeMenu.SelectionDeadzone;

            // Edge-detect the deadzone crossing so a held stick doesn't fire
            // a move every frame the action is "performed" - same flag-style
            // guard used for jump/interact elsewhere in the project.
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

    private void OnSelect(InputAction.CallbackContext context)
    {
        AdvanceDialog();

        if (MenuDialog.ActiveMenuDialog is NavigableMenuDialog activeMenu)
        {
            activeMenu.ConfirmSelection();
            return;
        }
    }

    [ContextMenu("Advance Dialog")]
    public void AdvanceDialog()
    {
        bool hasSay = SayDialog.ActiveSayDialog;
        bool hasMenu = MenuDialog.ActiveMenuDialog != null &&
                       MenuDialog.ActiveMenuDialog.IsActive();
        // LogMessage($"AdvanceDialog — hasSay={hasSay}, hasMenu={hasMenu}");

        if (!hasSay || hasMenu) return;
        dialogInput.SetNextLineFlag();
    }

    private void LogMessage(string msg)
    {
        Debug.Log($"[CanvasManager] {msg}");
    }
}