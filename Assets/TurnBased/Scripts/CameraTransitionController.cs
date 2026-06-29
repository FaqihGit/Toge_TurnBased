using System.Collections;
using UnityEngine;

public class CameraTransitionController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera _mainCamera; public Camera mainCamera => _mainCamera;
    [SerializeField] private Transform explorationReference;
    [SerializeField] private Transform combatReference;

    [Header("Blend Settings")]
    [SerializeField] private float blendDuration = 0.6f;
    [SerializeField] private AnimationCurve blendCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private bool isCombatFramingActive;
    private Coroutine activeBlend;

    private Transform ActiveReference => isCombatFramingActive ? combatReference : explorationReference;

    public void Init(GameState initialState)
    {
        isCombatFramingActive = initialState == GameState.Combat;
        SnapTo(ActiveReference);
    }

    public void HandleStateChanged(GameState oldState, GameState newState)
    {
        bool shouldBeCombatFraming = newState == GameState.Combat ||
            (newState == GameState.DialogueCutscene && isCombatFramingActive);

        if (shouldBeCombatFraming == isCombatFramingActive) return;

        isCombatFramingActive = shouldBeCombatFraming;
        BeginBlend(ActiveReference);
    }

    private void SnapTo(Transform reference)
    {
        _mainCamera.transform.SetPositionAndRotation(reference.position, reference.rotation);
    }

    private void BeginBlend(Transform target)
    {
        if (activeBlend != null) StopCoroutine(activeBlend);
        activeBlend = StartCoroutine(BlendTo(target));
    }

    private IEnumerator BlendTo(Transform target)
    {
        Transform cam = _mainCamera.transform;
        Vector3 startPos = cam.position;
        Quaternion startRot = cam.rotation;

        float t = 0f;
        while (t < blendDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = blendCurve.Evaluate(Mathf.Clamp01(t / blendDuration));

            // Reads target.position/rotation live each frame rather than a cached
            // snapshot, so the blend still resolves correctly if a reference is
            // nudged by another system mid-transition.
            cam.SetPositionAndRotation(
                Vector3.Lerp(startPos, target.position, k),
                Quaternion.Slerp(startRot, target.rotation, k));

            yield return null;
        }

        cam.SetPositionAndRotation(target.position, target.rotation);
        activeBlend = null;
    }

    private void OnDrawGizmosSelected()
    {
        if (explorationReference != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(explorationReference.position, 0.3f);
        }

        if (combatReference != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(combatReference.position, 0.3f);
        }

        if (explorationReference != null && combatReference != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(explorationReference.position, combatReference.position);
        }
    }
}