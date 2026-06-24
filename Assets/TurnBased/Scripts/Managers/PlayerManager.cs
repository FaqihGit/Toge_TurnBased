using UnityEngine;
using UnityEngine.Events;

public class PlayerManager : MonoBehaviour
{
    public UnityAction<bool> OnPlayerInteracted;
    [SerializeField] private PlayerExploration movement;

    public void Init(PlayerInputAction controls)
    {
        movement.Init(controls);
        movement.OnPlayerInteracted = (isInteracting) => OnPlayerInteracted?.Invoke(isInteracting);
    }
}
