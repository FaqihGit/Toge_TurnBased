using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CombatCanvasManager : MonoBehaviour
{
    [Header("World Equvalients")]
    [SerializeField] private Transform worldCanvas;
    [SerializeField] private CombatUnitUI combatUnitUiPrefab;
    [SerializeField] private Vector3 worldOffset = new(0f, 2f, 0f);

    [Header("Action Prompt")]
    [SerializeField] private CanvasGroup actionPromptCanvasGroup;
    [SerializeField] private TMP_Text actionPromptText;
    [SerializeField] private float actionPromptFadeDuration = .5f;
    [SerializeField] private float actionPromptFadeDelay = 1f;
    private Tween actionPromptTween;

    [Header("Target Indicator")]
    [SerializeField] private RectTransform indicatorUI;
    [SerializeField] private Vector3 indicatorWorldOffset = new(0f, 2.5f, 0f);

    private readonly Dictionary<CombatUnit, Transform> unitAnchors = new();
    private Transform indicatorTarget;

    private Camera combatCamera;
    private readonly Dictionary<CombatUnit, CombatUnitUI> activeUI = new();

    void LateUpdate()
    {
        if (indicatorTarget != null && indicatorUI != null && indicatorUI.gameObject.activeSelf)
            PositionIndicator();
    }

    public void Init(Camera combatCamera)
    {
        this.combatCamera = combatCamera;

        actionPromptCanvasGroup.alpha = 0;
        ShowIndicator(false);
    }

    private void PositionIndicator()
    {
        Vector3 worldPos = indicatorTarget.position + indicatorWorldOffset;
        Vector3 screenPos = combatCamera.WorldToScreenPoint(worldPos);
        indicatorUI.position = screenPos;
    }

    public void BindParty(IReadOnlyList<CombatUnit> units, CombatPartyHandler partyHandler, int energyCap)
    {
        for (int i = 0; i < units.Count; i++)
        {
            var unit = units[i];
            var anchor = partyHandler.GetCanvasTarget(i);
            if (anchor == null) continue;

            var ui = Instantiate(combatUnitUiPrefab, worldCanvas);
            ui.Bind(combatCamera, anchor, worldOffset);

            activeUI[unit] = ui;
            unitAnchors[unit] = anchor;
            RefreshUnit(unit, energyCap);
        }
    }

    public void ShowAction(string action)
    {
        actionPromptText.text = action;
        actionPromptCanvasGroup.alpha = 1;

        actionPromptTween?.Kill();
        actionPromptTween = actionPromptCanvasGroup.DOFade(0, actionPromptFadeDuration).SetDelay(actionPromptFadeDelay);

        LayoutRebuilder.ForceRebuildLayoutImmediate(actionPromptCanvasGroup.transform as RectTransform);
    }

    public void ShowAction(CombatUnit actor, CombatActionSO action, List<CombatUnit> targets)
    {
        ShowAction(BuildActionText(actor, action, targets));
    }

    public void ShowSkip(CombatUnit actor)
    {
        ShowAction($"{actor.source.name} skip turn");
    }

    private string BuildActionText(CombatUnit actor, CombatActionSO action, List<CombatUnit> targets)
    {
        string subject = actor.source.name;
        string actionName = action.name;

        if (targets == null || targets.Count == 0)
            return $"{subject} use {actionName}";

        if (targets.Count == 1)
            return $"{subject} use {actionName} to {targets[0].source.name}";

        // GetValidTargets only ever returns a single-faction pool relative to
        // the actor, so checking the first target is enough for the whole list.
        bool isAlly = targets[0].faction == actor.faction;
        string group = isAlly ? "allies" : "foes";
        return $"{subject} use {actionName} to {targets.Count} {group}";
    }

    public void ShowIndicator(bool isShow)
    {
        if (indicatorUI == null) return;
        indicatorUI.gameObject.SetActive(isShow);
        if (!isShow) indicatorTarget = null;
    }

    public void SetIndicator(CombatUnit unit)
    {
        if (unit == null || indicatorUI == null) return;
        if (!unitAnchors.TryGetValue(unit, out var anchor)) return;

        indicatorTarget = anchor;
        indicatorUI.SetAsLastSibling();
        PositionIndicator();
    }

    public void RefreshUnit(CombatUnit unit, int energyCap)
    {
        if (!activeUI.TryGetValue(unit, out var ui)) return;

        ui.SetName(unit.source.name); // swap to whatever field your UnitDataSO actually uses for display name
        ui.SetHealth(Mathf.RoundToInt(unit.currentHealth), Mathf.RoundToInt(unit.maxHealth));
        ui.SetEnergy(unit.energy, energyCap);
    }

    public void ClearAll()
    {
        foreach (var kv in activeUI)
            if (kv.Value != null) Destroy(kv.Value.gameObject);

        activeUI.Clear();
        unitAnchors.Clear();
        indicatorTarget = null;
        ShowIndicator(false);
    }
}