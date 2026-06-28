using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CombatUnitUI : MonoBehaviour
{
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI energyText;
    [SerializeField] private Slider hpSlider;
    [SerializeField] private TextMeshProUGUI hpText;

    // Optional — used to hide the UI without disabling the GameObject (disabling
    // would stop LateUpdate from running, so it could never come back on screen).
    [SerializeField] private CanvasGroup canvasGroup;

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
        energyText.text = $"E:{energy}/{energyCap}";
    }

    public void SetHealth(int currentHealth, int maxHealth)
    {
        // Was inverted (maxHealth / currentHealth) — fixed to current/max, with a
        // guard so a 0-max edge case can't divide by zero.
        hpSlider.value = maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
        hpText.text = $"{currentHealth}/{maxHealth}";
    }
}