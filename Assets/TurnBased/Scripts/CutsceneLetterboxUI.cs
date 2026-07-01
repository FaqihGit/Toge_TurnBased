using DG.Tweening;
using UnityEngine;

public class CutsceneLetterboxUI : MonoBehaviour
{
    [Header("Bars")]
    [SerializeField] private RectTransform topBar;
    [SerializeField] private RectTransform bottomBar;
    private float barHeight;
    [SerializeField] private float animDuration = 0.35f;
    [SerializeField] private AnimationCurve animCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private void Awake()
    {
        barHeight = Mathf.Max(topBar.rect.height, bottomBar.rect.height);
        SetBarsHidden();
    }

    public void Init(CutsceneManager cutscene)
    {
        cutscene.OnCutsceneStarted -= Show;
        cutscene.OnCutsceneStarted += Show;

        cutscene.OnCutsceneEnded -= Hide;
        cutscene.OnCutsceneEnded += Hide;
    }

    private void SetBarsHidden()
    {
        topBar.anchoredPosition = new Vector2(topBar.anchoredPosition.x, barHeight);
        bottomBar.anchoredPosition = new Vector2(bottomBar.anchoredPosition.x, -barHeight);
    }

    public void Show()
    {
        topBar.DOKill();
        bottomBar.DOKill();

        topBar.DOAnchorPosY(0f, animDuration).SetEase(animCurve);
        bottomBar.DOAnchorPosY(0f, animDuration).SetEase(animCurve);
    }

    public void Hide()
    {
        topBar.DOKill();
        bottomBar.DOKill();

        topBar.DOAnchorPosY(barHeight, animDuration).SetEase(animCurve);
        bottomBar.DOAnchorPosY(-barHeight, animDuration).SetEase(animCurve);
    }
}