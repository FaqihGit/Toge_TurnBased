using System.Collections.Generic;
using UnityEngine;

/// Owns the Speed Bank initiative algorithm (GDD 3) — a pure, stateless-per-call
/// computation over CombatUnit.speedBank. No Unity lifecycle dependency.
public class CombatTurnOrder
{
    private float turnThreshold;

    public float TurnThreshold => turnThreshold;

    /// GDD 3.1: threshold = average Speed of all living units, fixed for the
    /// whole encounter. Call once per StartCombat, after any entry HP
    /// corrections (order doesn't matter for this calc, but keep it after
    /// the roster is final).
    public void Init(IReadOnlyList<CombatUnit> allUnits)
    {
        turnThreshold = 0f;
        foreach (var u in allUnits)
            turnThreshold += u.speed;
        turnThreshold /= Mathf.Max(1, allUnits.Count);

        foreach (var u in allUnits)
            u.speedBank = u.speed;
    }

    /// Returns the next unit to act, deducting the threshold from its bank.
    /// Applies the passive fill step (GDD 3) to all living units whenever no
    /// unit currently qualifies, looping until one does. Returns null only
    /// if no units are alive (CheckEncounterEnd should already have caught
    /// this before AdvanceTurn is called again).
    public CombatUnit GetNextActor(IReadOnlyList<CombatUnit> allUnits)
    {
        while (true)
        {
            CombatUnit best = null;

            foreach (var u in allUnits)
            {
                if (u.IsDead) continue;
                if (u.speedBank < turnThreshold) continue;

                if (best == null || u.speedBank > best.speedBank)
                {
                    best = u;
                }
                else if (Mathf.Approximately(u.speedBank, best.speedBank))
                {
                    // GDD 3.4: tie -> player side priority
                    if (u.faction == UnitFactionEnum.Player && best.faction != UnitFactionEnum.Player)
                        best = u;
                }
            }

            if (best != null)
            {
                best.speedBank -= turnThreshold;
                return best;
            }

            bool anyAlive = false;
            foreach (var u in allUnits)
            {
                if (u.IsDead) continue;
                anyAlive = true;
                u.speedBank += u.speed; // passive fill step — essential, see GDD 3
            }

            if (!anyAlive) return null;
        }
    }
}