using System.Collections.Generic;
using Fungus;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

public class Interactables : MonoBehaviour
{
    [Header("Events")]
    [SerializeField] private UnityEvent OnEndDialogEvent;
    private UnityAction OnEndDialogAction;
    public UnityAction<CombatPartyHandler> OnTriggerCombatAction;

    [Header("Data References")]
    [SerializeField] private UnitDataSO npcData;
    [SerializeField] private List<UnitDataSO> teamDataList;
    public bool isInteractable
    {
        get
        {
            if (_isOnlyInteractingOnce && hasBeenInteracted) return false;
            if (!npcFlowchart) return false;
            return true;
        }
    }
    [SerializeField] private bool _isOnlyInteractingOnce = false; public bool isOnlyInteractingOnce => _isOnlyInteractingOnce;
    private bool hasBeenInteracted;

    [Header("Component References")]
    [SerializeField] private Flowchart npcFlowchart;
    [SerializeField] private Transform _canvasTarget; public Transform canvasTarget => _canvasTarget;
    [SerializeField] private Character npcCharacter;
    [SerializeField] private CombatPartyHandler combatParty;
    private bool isInteracting;

    private bool hasInitialized = false;

    [ContextMenu("Init")]
    public void Init()
    {
        if (hasInitialized) return;

        foreach (var unit in teamDataList) unit.stats.ResetHealth();

        npcCharacter.SetStandardText(npcData.name);
        combatParty.partyUnitList = new(teamDataList);

        hasBeenInteracted = false;
        npcFlowchart.SetBooleanVariable("HasBeenInteracted", false);

        hasInitialized = true;
    }

    public bool Interact(UnityAction OnEndDialogCallback)
    {
        if (!isInteractable) return false;

        LogMessage($"Interacted");

        Init();

        if (isInteracting) return false;
        isInteracting = true;

        npcFlowchart.ExecuteBlock("Interact");

        OnEndDialogAction = () => OnEndDialogCallback?.Invoke();
        return true;
    }

    public void TriggerCombat()
    {
        OnDialog(false);
        OnTriggerCombatAction?.Invoke(combatParty);
    }

    public void OnDialog(bool isStart)
    {
        LogMessage($"OnDialog {isStart}");
        isInteracting = isStart;
        if (!isStart)
        {
            hasBeenInteracted = true;
            npcFlowchart.SetBooleanVariable("HasBeenInteracted", true);

            OnEndDialogAction?.Invoke();
            OnEndDialogAction = null;

            OnEndDialogEvent?.Invoke();
        }
    }

    protected void LogMessage(string msg)
    {
        // Debug.Log($"[Interactables] {name} {msg}");
    }
}
