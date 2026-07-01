using System;
using System.Collections.Generic;

/// Owns pre-combat snapshotting, Escape rollback (GDD 10), and win/loss
/// detection. Fires OnCombatEnded/OnEscaped when the encounter resolves —
/// CombatManager subscribes to run its own UI/state cleanup and then
/// re-fires its public events of the same names.
public class CombatLifecycleController
{
    private readonly List<CombatUnitSnapshot> preCombatSnapshots = new();

    public event Action<bool> OnCombatEnded;
    public event Action OnEscaped;

    /// Caller must snapshot AFTER any entry corrections (e.g. "enter at 1 HP"
    /// safety floor) — see GDD 10.
    public void CaptureSnapshots(IReadOnlyList<CombatUnit> playerUnits)
    {
        preCombatSnapshots.Clear();
        foreach (var u in playerUnits)
            preCombatSnapshots.Add(u.CaptureSnapshot());
    }

    /// GDD 10: always available, no cost, 100% success — voids the encounter
    /// entirely and restores the player party to its pre-combat snapshot.
    public void Escape(List<CombatUnit> playerUnits)
    {
        foreach (var snap in preCombatSnapshots)
            snap.unit.RestoreSnapshot(snap);

        foreach (var u in playerUnits)
            u.CommitToSource();

        OnEscaped?.Invoke();
    }

    /// Returns true (and fires OnCombatEnded) if the encounter is over.
    public bool CheckEncounterEnd(List<CombatUnit> playerUnits, List<CombatUnit> enemyUnits)
    {
        bool playerAlive = false;
        foreach (var u in playerUnits) if (!u.IsDead) { playerAlive = true; break; }

        bool enemyAlive = false;
        foreach (var u in enemyUnits) if (!u.IsDead) { enemyAlive = true; break; }

        if (!enemyAlive) { EndCombat(true, playerUnits); return true; }
        if (!playerAlive) { EndCombat(false, playerUnits); return true; }
        return false;
    }

    private void EndCombat(bool victory, List<CombatUnit> playerUnits)
    {
        if (victory)
        {
            foreach (var u in playerUnits)
                u.CommitToSource();
        }
        // Defeat outcome (game over / retry flow) is left for GameManager.

        OnCombatEnded?.Invoke(victory);
    }
}