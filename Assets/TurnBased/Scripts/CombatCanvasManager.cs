using System.Collections.Generic;
using UnityEngine;

public class CombatCanvasManager : MonoBehaviour
{
    [SerializeField] private CombatUnitUI combatUnitUiPrefab;
    [SerializeField] private RectTransform canvasRoot; // parent under the single shared canvas
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2f, 0f); // e.g. above the unit's head

    private Camera combatCamera;
    private readonly Dictionary<CombatUnit, CombatUnitUI> activeUI = new();

    public void Init(Camera combatCamera)
    {
        Debug.Log($"Init {combatCamera}");
        this.combatCamera = combatCamera;
    }

    /// Spawns one CombatUnitUI per unit and binds it to that unit's party slot anchor.
    /// Relies on the same index alignment CombatManager already assumes for
    /// SetIndicator/ShowSelection — units[i] <-> partyHandler's visual slot i.
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
            RefreshUnit(unit, energyCap);
        }
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
    }
}