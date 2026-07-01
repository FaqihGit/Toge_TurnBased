using UnityEngine;

public enum CutsceneStepType
{
    MoveTo,
    TriggerDialog
}

[System.Serializable]
public class CutsceneStep
{
    [SerializeField] private string label; // inspector readability only
    [SerializeField] private CutsceneStepType type;

    [Header("Move To")]
    [SerializeField] private Transform destination;
    [SerializeField] private float arrivalThreshold = 0.15f;

    [Header("Trigger Dialog")]
    [SerializeField] private Interactables dialogTarget;

    public CutsceneStepType Type => type;
    public Transform Destination => destination;
    public float ArrivalThreshold => arrivalThreshold;
    public Interactables DialogTarget => dialogTarget;
}