using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CombatUnitUI : MonoBehaviour
{
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI energyText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Hp sliders")]
    [SerializeField] private Slider hpSliderFront;
    [SerializeField] private Slider hpSliderBack;
    [SerializeField] private TextMeshProUGUI hpText;

    [SerializeField] private float damageDelay = 0.15f;
    [SerializeField] private float damageDuration = 0.4f;
    [SerializeField] private float healDuration = 0.35f;

    private Tween backBarTween;

    private Camera trackingCamera;
    private Transform worldTarget;
    private Vector3 worldOffset;

    void Awake()
    {
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
    }

    /// Called once by CombatCanvasManager when this instance is spawned for a unit.
    public void Bind(Camera camera, Transform target, Vector3 offset)
    {
        trackingCamera = camera;
        worldTarget = target;
        worldOffset = offset;
        UpdatePosition(); // snap immediately so it doesn't flash at the prefab's default position for one frame
    }

    void LateUpdate()
    {
        UpdatePosition();
    }

    private void UpdatePosition()
    {
        if (worldTarget == null || trackingCamera == null) return;

        Vector3 screenPoint = trackingCamera.WorldToScreenPoint(worldTarget.position + worldOffset);
        bool visible = screenPoint.z > 0f; // behind the camera

        if (canvasGroup != null)
            canvasGroup.alpha = visible ? 1f : 0f;

        if (visible)
            rectTransform.position = screenPoint;
    }

    public void SetName(string unitName)
    {
        nameText.text = unitName;
    }

    public void SetEnergy(int energy, int energyCap)
    {
        energyText.text = $"E:<color=#F7F7F7>{energy}/{energyCap}";
    }

    public void SetHealth(int currentHealth, int maxHealth)
    {
        float target = maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
        float currentFront = hpSliderFront.value;
        float currentBack = hpSliderBack.value;

        hpText.text = $"{currentHealth}/{maxHealth}";

        // Kill any ongoing tween but KEEP current value
        backBarTween?.Kill(false);

        if (target < currentFront)
        {
            // DAMAGE
            // Front bar drops instantly
            hpSliderFront.value = target;

            // Back bar lags behind
            backBarTween = hpSliderBack
                .DOValue(target, damageDuration)
                .SetDelay(backBarTween != null && backBarTween.IsActive() ? 0f : damageDelay)
                .SetEase(Ease.OutQuad);
        }
        else if (target > currentFront)
        {
            // HEAL
            // Back bar jumps immediately
            hpSliderBack.value = target;

            // Front bar catches up smoothly
            backBarTween = hpSliderFront
                .DOValue(target, healDuration)
                .SetEase(Ease.OutQuad);
        }
    }
}