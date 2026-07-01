using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class PlayerManager : MonoBehaviour
{
    public UnityAction<bool> OnPlayerInteracted;
    public UnityAction<Interactables> OnPlayerInteractableUpdate;
    public Action<CombatPartyHandler> OnPlayerTriggerCombat;

    [Header("Asset References")]
    public List<UnitDataSO> partyUnitList;

    [Header("Component References")]
    [SerializeField] private PlayerExploration _exploration; public PlayerExploration exploration => _exploration;
    [SerializeField] private CombatPartyHandler _playerParty; public CombatPartyHandler playerParty => _playerParty;
    public void Init(PlayerInputAction controls)
    {
        foreach (var unit in partyUnitList)
        {
            unit.stats.ResetHealth();
        }

        _exploration.Init(controls);
        _exploration.OnPlayerInteracted = (isInteracting) => OnPlayerInteracted?.Invoke(isInteracting);
        _exploration.OnPlayerInteractableUpdate = (interactable) => OnPlayerInteractableUpdate?.Invoke(interactable);
        _exploration.OnPlayerTriggerCombat = (enemy) => OnPlayerTriggerCombat?.Invoke(enemy);

        _playerParty.partyUnitList = new(partyUnitList);
    }
}
