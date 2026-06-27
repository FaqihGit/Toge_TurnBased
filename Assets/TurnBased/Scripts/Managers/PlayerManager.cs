using System;
using UnityEngine;
using UnityEngine.Events;

public class PlayerManager : MonoBehaviour
{
    public UnityAction<bool> OnPlayerInteracted;
    public Action<CombatPartyHandler> OnPlayerTriggerCombat;

    [SerializeField] private PlayerExploration exploration;
    [SerializeField] private CombatPartyHandler _playerParty; public CombatPartyHandler playerParty => _playerParty;
    public void Init(PlayerInputAction controls)
    {
        exploration.Init(controls);
        exploration.OnPlayerInteracted = (isInteracting) => OnPlayerInteracted?.Invoke(isInteracting);
        exploration.OnPlayerTriggerCombat = (enemy) => OnPlayerTriggerCombat?.Invoke(enemy);
    }
}
