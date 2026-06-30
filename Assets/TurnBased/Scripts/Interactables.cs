using System.Collections.Generic;
using Fungus;
using UnityEngine;
using UnityEngine.Events;

public class Interactables : MonoBehaviour
{
    private UnityAction OnEndDialogAction;
    public UnityAction<CombatPartyHandler> OnTriggerCombat;

    [Header("Data References")]
    [SerializeField] private UnitDataSO npcData;
    [SerializeField] private List<UnitDataSO> teamDataList;

    [Header("Component References")]
    [SerializeField] private Flowchart npcFlowchart;
    [SerializeField] private Character npcCharacter;
    [SerializeField] private CombatPartyHandler combatParty;
    private bool isInteracting;

    [ContextMenu("Init")]
    public void Init()
    {
        npcCharacter.SetStandardText(npcData.name);
    }

    public bool Interact(UnityAction OnEndDialogCallback)
    {
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
        OnTriggerCombat?.Invoke(combatParty);
    }

    public void OnDialog(bool isStart)
    {
        LogMessage($"OnDialog {isStart}");
        isInteracting = isStart;
        if (!isStart)
        {
            OnEndDialogAction?.Invoke();
            OnEndDialogAction = null;
        }
    }

    protected void LogMessage(string msg)
    {
        // Debug.Log($"[Interactables] {name} {msg}");
    }
}
