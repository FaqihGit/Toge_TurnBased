using System;
using Fungus;
using UnityEngine;
using UnityEngine.InputSystem;

public class CanvasManager : MonoBehaviour
{
    [SerializeField] private NavigableMenuDialog menuDialog;
    [SerializeField] private SayDialog sayDialog;
    [SerializeField] private DialogInput dialogInput;

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

    public void Init(PlayerInputAction playerControls)
    {
        this.playerControls = playerControls;
        SubscribeControls(true);
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
        AdvanceDialog();
    }

    private void OnSelect(InputAction.CallbackContext context)
    {
        if (MenuDialog.ActiveMenuDialog is NavigableMenuDialog activeMenu)
        {
            activeMenu.ConfirmSelection();
            return;
        }

        AdvanceDialog();
    }

    [ContextMenu("Advance Dialog")]
    public void AdvanceDialog()
    {
        if (
            !SayDialog.ActiveSayDialog
            || MenuDialog.ActiveMenuDialog
            ) return;
        dialogInput.SetNextLineFlag();
    }
}