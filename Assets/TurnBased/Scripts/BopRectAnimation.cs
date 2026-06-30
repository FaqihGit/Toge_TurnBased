using UnityEngine;
using DG.Tweening;

public class BopRectAnimation : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform target;

    [Header("Bop Settings")]
    [SerializeField] private float bopHeight = 15f;
    [SerializeField] private float duration = 0.6f;
    [SerializeField] private Ease easeUp = Ease.InOutSine;
    [SerializeField] private Ease easeDown = Ease.InOutSine;

    private Vector2 originalPosition;
    private Tween bopTween;

    private void Awake()
    {
        if (target == null)
            target = transform as RectTransform;
    }

    private void OnEnable()
    {
        StartBop();
    }

    private void OnDisable()
    {
        StopBop();
    }

    public void StartBop()
    {
        originalPosition = target.anchoredPosition;

        bopTween = target
            .DOAnchorPosY(originalPosition.y + bopHeight, duration)
            .SetEase(easeUp)
            .SetLoops(-1, LoopType.Yoyo)
            .SetId(target); // useful for targeted Kill() calls later
    }

    public void StopBop(bool resetPosition = true)
    {
        if (bopTween != null && bopTween.IsActive())
            bopTween.Kill();

        if (resetPosition)
            target.anchoredPosition = originalPosition;
    }
}