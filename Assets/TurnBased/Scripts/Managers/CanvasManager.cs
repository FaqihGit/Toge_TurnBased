using System;
using System.Runtime.CompilerServices;
using Fungus;
using UnityEngine;
using UnityEngine.InputSystem;

public class CanvasManager : MonoBehaviour
{
    [SerializeField] private MenuDialog menuDialog;
    [SerializeField] private SayDialog sayDialog;
    [SerializeField] private DialogInput dialogInput;

    private PlayerInputAction playerControls;
    private Vector2 selectionInput;

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

        if (isSubscribe)
        {
            playerControls.Dialogue.Selection.performed += OnSelection;

            playerControls.Dialogue.Select.performed += OnSelect;
        }
    }

    private void OnSelection(InputAction.CallbackContext context)
    {
        selectionInput = context.ReadValue<Vector2>();
        AdvanceDialog();
    }

    private void OnSelect(InputAction.CallbackContext context)
    {
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
