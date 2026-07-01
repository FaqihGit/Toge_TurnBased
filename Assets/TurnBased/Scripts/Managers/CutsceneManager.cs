using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CutsceneManager : MonoBehaviour
{
    public event Action OnCutsceneStarted;
    public event Action OnCutsceneEnded;

    [Header("Obstacle Detection")]
    [SerializeField] private float obstacleCheckDistance = 0.6f;
    [SerializeField] private LayerMask obstacleLayerMask; // configure to EXCLUDE OneWayPlatform
    private Transform obstacleCheckOrigin;

    private PlayerExploration player;
    private bool isInteractionActive;
    private Coroutine runningCutscene;

    public bool IsRunning => runningCutscene != null;

    public void Init(PlayerExploration player)
    {
        this.player = player;
        obstacleCheckOrigin = player.transform;

        player.OnPlayerInteracted -= HandlePlayerInteracted;
        player.OnPlayerInteracted += HandlePlayerInteracted;
    }

    private void HandlePlayerInteracted(bool isInteracting)
    {
        isInteractionActive = isInteracting;
    }

    /// <summary>
    /// Starts running the given step sequence. Ignored if a cutscene is already running.
    /// </summary>
    public bool Play(List<CutsceneStep> steps)
    {
        if (runningCutscene != null) return false;
        if (steps == null || steps.Count == 0) return false;

        runningCutscene = StartCoroutine(RunCutscene(steps));
        return true;
    }

    private IEnumerator RunCutscene(List<CutsceneStep> steps)
    {
        OnCutsceneStarted?.Invoke();
        player.SetCutsceneControl(true);

        LogMessage("RunCutscene START");

        foreach (CutsceneStep step in steps)
        {
            LogMessage("RunCutscene step.Type");
            switch (step.Type)
            {
                case CutsceneStepType.MoveTo:
                    yield return RunMoveStep(step);
                    break;

                case CutsceneStepType.TriggerDialog:
                    yield return RunDialogStep(step);
                    break;
            }
        }

        player.SetCutsceneControl(false);
        runningCutscene = null;
        OnCutsceneEnded?.Invoke();
        LogMessage("RunCutscene ENDS");
    }

    private IEnumerator RunMoveStep(CutsceneStep step)
    {
        if (step.Destination == null) yield break;

        LogMessage("RunMoveStep STARTS");
        while (true)
        {
            Vector2 toTarget = step.Destination.position - player.transform.position;
            toTarget.y = 0f;

            LogMessage($"RunMoveStep toTarget {toTarget}");
            float distance = toTarget.magnitude;
            LogMessage($"RunMoveStep distance {distance} step.ArrivalThreshold {step.ArrivalThreshold}");
            if (distance <= step.ArrivalThreshold)
                break;

            float horizontal = Mathf.Abs(toTarget.x) < 0.05f ? 0f : Mathf.Sign(toTarget.x);
            player.DriveExternalMove(horizontal);

            if (horizontal != 0f && player.IsGrounded && IsObstacleAhead(horizontal))
                player.RequestExternalJump();

            yield return null;
        }
        LogMessage("RunMoveStep ENDS");

        player.DriveExternalMove(0f);
    }

    private bool IsObstacleAhead(float direction)
    {
        if (obstacleCheckOrigin == null) return false;

        Vector3 castDir = direction > 0f ? Vector3.right : Vector3.left;
        return Physics.Raycast(obstacleCheckOrigin.position, castDir, obstacleCheckDistance, obstacleLayerMask);
    }

    private IEnumerator RunDialogStep(CutsceneStep step)
    {
        if (step.DialogTarget == null) yield break;

        bool started = player.TryInteract(step.DialogTarget);
        if (!started) yield break;

        yield return new WaitUntil(() => !isInteractionActive);
    }

    private void OnDrawGizmosSelected()
    {
        if (obstacleCheckOrigin == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawRay(obstacleCheckOrigin.position, Vector3.right * obstacleCheckDistance);
        Gizmos.DrawRay(obstacleCheckOrigin.position, Vector3.left * obstacleCheckDistance);
    }

    private void LogMessage(string msg)
    {
        // Debug.Log($"[CutsceneManager] {msg}");
    }
}