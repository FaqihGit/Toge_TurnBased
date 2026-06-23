using Fungus;
using UnityEngine;

public class Interactables : MonoBehaviour
{
    [SerializeField] private Flowchart npcFlowchart;
    private bool isInteracting;

    public void Interact()
    {
        Debug.Log($"{name} Interacted");
        if (npcFlowchart)
        {
            if (isInteracting) return;
            isInteracting = true;

            npcFlowchart.ExecuteBlock("Interact");
        }
    }

    public void OnDialog(bool isStart)
    {
        isInteracting = isStart;
    }
}
