using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class CutsceneTrigger : MonoBehaviour
{
    [SerializeField] private CutsceneManager cutsceneManager;
    [SerializeField] private List<CutsceneStep> steps = new();

    [Header("Gizmos")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private Color moveColor = Color.cyan;
    [SerializeField] private Color dialogColor = Color.yellow;
    [SerializeField] private Color pathColor = new(1f, 1f, 1f, 0.5f);

    [ContextMenu("DEBUG/Play Cutscene")]
    public void Play()
    {
        if (cutsceneManager == null) return;
        cutsceneManager.Play(steps);
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos || steps == null || steps.Count == 0) return;

        Vector3 pathCursor = transform.position;

        for (int i = 0; i < steps.Count; i++)
        {
            CutsceneStep step = steps[i];
            if (step == null) continue;

            switch (step.Type)
            {
                case CutsceneStepType.MoveTo:
                    DrawMoveStep(step, i, ref pathCursor);
                    break;

                case CutsceneStepType.TriggerDialog:
                    DrawDialogStep(step, i, pathCursor);
                    break;
            }
        }
    }

    private void DrawMoveStep(CutsceneStep step, int index, ref Vector3 pathCursor)
    {
        if (step.Destination == null) return;

        Vector3 dest = step.Destination.position;

        // Path line from wherever we last stood to this destination
        Gizmos.color = pathColor;
        Gizmos.DrawLine(pathCursor, dest);

        // Arrival radius
        Gizmos.color = moveColor;
        Gizmos.DrawWireSphere(dest, step.ArrivalThreshold);

        // Solid dot at the exact target point
        Gizmos.DrawSphere(dest, 0.05f);

        DrawLabel(dest, $"{index}: Move");

        pathCursor = dest;
    }

    private void DrawDialogStep(CutsceneStep step, int index, Vector3 pathCursor)
    {
        if (step.DialogTarget == null) return;

        Vector3 dialogPos = step.DialogTarget.transform.position;

        // Faint connector so you can see which point in the sequence triggers this dialog
        Gizmos.color = pathColor;
        Gizmos.DrawLine(pathCursor, dialogPos);

        Gizmos.color = dialogColor;
        Gizmos.DrawWireCube(dialogPos + Vector3.up * 0.1f, Vector3.one * 0.25f);

        DrawLabel(dialogPos, $"{index}: Dialog");
    }

    private static void DrawLabel(Vector3 position, string text)
    {
#if UNITY_EDITOR
        Handles.Label(position + Vector3.up * 0.3f, text);
#endif
    }
}