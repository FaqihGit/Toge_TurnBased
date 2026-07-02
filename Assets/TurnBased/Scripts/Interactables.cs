using System.Collections.Generic;
using Fungus;
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

        npcCharacter.SetStandardText(npcData.name);
        combatParty.partyUnitList = new(teamDataList);

        hasInitialized = true;
    }

    public bool Interact(UnityAction OnEndDialogCallback)
    {
        Init();
        LogMessage($"Interacted");
        if (npcFlowchart)
        {
            if (isInteracting) return false;
            isInteracting = true;

            npcFlowchart.ExecuteBlock("Interact");

            OnEndDialogAction = () => OnEndDialogCallback?.Invoke();
            return true;
        }
        return false;
    }

    [ContextMenu("Trigger Combat")]
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
