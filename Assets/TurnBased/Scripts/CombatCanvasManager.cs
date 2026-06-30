using System.Collections.Generic;
using UnityEngine;

public class CombatCanvasManager : MonoBehaviour
{
    [SerializeField] private CombatUnitUI combatUnitUiPrefab;
    [SerializeField] private RectTransform canvasRoot; // parent under the single shared canvas
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2f, 0f); // e.g. above the unit's head

    [Header("Target Indicator")]
    [SerializeField] private RectTransform indicatorUI;
    [SerializeField] private Vector3 indicatorWorldOffset = new Vector3(0f, 2.5f, 0f);

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
        ShowIndicator(false);
    }

    private void PositionIndicator()
    {
        Vector3 worldPos = indicatorTarget.position + indicatorWorldOffset;
        Vector3 screenPos = combatCamera.WorldToScreenPoint(worldPos);
        indicatorUI.position = screenPos; // match whatever space conversion CombatUnitUI.Bind uses (Overlay vs Camera-space canvas)
    }

    public void BindParty(IReadOnlyList<CombatUnit> units, CombatPartyHandler partyHandler, int energyCap)
    {
        for (int i = 0; i < units.Count; i++)
        {
            var unit = units[i];
            var anchor = partyHandler.GetCanvasTarget(i);
            if (anchor == null) continue;

            var ui = Instantiate(combatUnitUiPrefab, canvasRoot);
            ui.Bind(combatCamera, anchor, worldOffset);

            activeUI[unit] = ui;
            unitAnchors[unit] = anchor;          // NEW — indicator reuses the same anchor
            RefreshUnit(unit, energyCap);
        }
    }

    public void ShowIndicator(bool isShow)
    {
        if (indicatorUI == null) return;
        indicatorUI.gameObject.SetActive(isShow);
        if (isShow) indicatorUI.SetAsLastSibling();
        else indicatorTarget = null;
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