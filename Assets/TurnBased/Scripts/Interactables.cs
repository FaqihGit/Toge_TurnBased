using Fungus;
using UnityEngine;
using UnityEngine.Events;

public class Interactables : MonoBehaviour
{
    private UnityAction OnEndDialogAction;

    [SerializeField] private Flowchart npcFlowchart;
    private bool isInteracting;

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
        Debug.Log($"[Interactables] {name} {msg}");
    }
}
