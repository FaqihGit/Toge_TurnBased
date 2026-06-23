using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    [SerializeField] private PlayerExploration movement;

    public void Init(PlayerInputAction controls)
    {
        movement.Init(controls);
    }
}
