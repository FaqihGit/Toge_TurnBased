using UnityEngine;
using UnityEngine.InputSystem;

public class CombatManager : MonoBehaviour
{
    private PlayerInputAction playerControls;

    void OnEnable()
    {
        SubscribePlayerControls(true);
    }

    void OnDisable()
    {
        SubscribePlayerControls(false);
    }

    public void Init(PlayerInputAction playerControls)
    {
        this.playerControls = playerControls;
        SubscribePlayerControls(true);
    }

    private void SubscribePlayerControls(bool isSubscribe)
    {
        if (playerControls == null) return;

        playerControls.Combat.Select.performed -= OnSelect;
        playerControls.Combat.Selection.performed -= OnSelection;

        if (isSubscribe)
        {
            playerControls.Combat.Select.performed += OnSelect;
            playerControls.Combat.Selection.performed += OnSelection;
        }
    }

    private void OnSelect(InputAction.CallbackContext ctx)
    {

    }

    private void OnSelection(InputAction.CallbackContext ctx)
    {

    }
}
