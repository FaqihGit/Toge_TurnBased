using System.Collections.Generic;
using UnityEngine;

public class CutsceneTrigger : MonoBehaviour
{
    [SerializeField] private CutsceneManager cutsceneManager;
    [SerializeField] private List<CutsceneStep> steps = new();

    [ContextMenu("DEBUG/Play Cutscene")]
    public void Play()
    {
        if (cutsceneManager == null) return;
        cutsceneManager.Play(steps);
    }
}