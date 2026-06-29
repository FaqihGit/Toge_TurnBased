using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure combat-math layer. Resolves a CombatActionSO against a set of targets
/// (damage/heal, buff, stun, GDD 4.1 +1 Energy-on-hit), and ticks status
/// durations at turn start. Mutates CombatUnit stat/health/energy/status
/// fields only — no Unity input, no UI references.
///
/// CombatManager owns sequencing (whose turn, which action, which targets)
/// and is responsible for refreshing UI after calling ResolveAction; this
/// class has no canvas/UI dependency so it can be unit-tested in isolation
/// once the damage formula (GDD 5) is finalized.
/// </summary>
public class CombatResolver
{
    private readonly int maxEnergy;

    public CombatResolver(int maxEnergy)
    {
        this.maxEnergy = maxEnergy;
    }

    /// Applies action to each target. Caller refreshes UI for affected
    /// targets afterward (it already has the target list).
    public void ResolveAction(CombatUnit actor, CombatActionSO action, List<CombatUnit> targets)
    {
        foreach (var target in targets)
        {
            if (target.IsDead) continue;

            bool dealtDamage = false;

            if (action.damageMult != 0f)
            {
                float rawAmount = actor.attack * action.damageMult;

                if (rawAmount > 0f)
                {
                    float mitigated = action.isIgnoreDef ? rawAmount : Mathf.Max(0f, rawAmount - target.defense);
                    target.currentHealth = Mathf.Clamp(target.currentHealth - mitigated, 0f, target.maxHealth);
                    dealtDamage = true;
                }
                else
                {
                    // Negative damageMult = heal; defense/isIgnoreDef don't apply.
                    target.currentHealth = Mathf.Clamp(target.currentHealth - rawAmount, 0f, target.maxHealth);
                }
            }

            if (action.isBuff)
                ApplyStatModBuff(target, action);

            if (action.appliesStun)
                ApplyStun(target, action.buffTurn);

            // GDD 4.1: hit by attack -> +1 Energy, always applies (even through Stun).
            if (dealtDamage)
                target.energy = Mathf.Min(maxEnergy, target.energy + 1);
        }
    }

    /// GDD 8: duration ticks at the start of the unit's own turn.
    public void TickStatuses(CombatUnit unit)
    {
        for (int i = unit.activeStatuses.Count - 1; i >= 0; i--)
        {
            var status = unit.activeStatuses[i];
            status.remainingTurns--;

            if (status.remainingTurns <= 0)
            {
                RevertStatus(unit, status);
                unit.activeStatuses.RemoveAt(i);
            }
        }
    }

    public bool HasAffordableAction(CombatUnit unit)
    {
        foreach (var action in unit.source.combatActionList)
            if (action.energyCost <= unit.energy)
                return true;
        return false;
    }

    private void ApplyStatModBuff(CombatUnit target, CombatActionSO action)
    {
        // GDD 8: no stacking — a reapplication refreshes rather than stacks.
        for (int i = target.activeStatuses.Count - 1; i >= 0; i--)
        {
            if (target.activeStatuses[i].sourceAction == action)
            {
                RevertStatus(target, target.activeStatuses[i]);
                target.activeStatuses.RemoveAt(i);
            }
        }

        // GDD 8: Speed buffs/debuffs are % based; Attack/Defense flat for now.
        float speedDelta = target.speed * action.buffValue.combatSpeed;

        var status = new ActiveStatus
        {
            type = StatusType.StatMod,
            sourceAction = action,
            remainingTurns = action.buffTurn,
            attackDelta = action.buffValue.combatAttack,
            defenseDelta = action.buffValue.combatDefense,
            speedDelta = speedDelta
        };

        target.attack += status.attackDelta;
        target.defense += status.defenseDelta;
        if (status.speedDelta != 0f)
            target.ChangeSpeed(target.speed + status.speedDelta);

        target.activeStatuses.Add(status);
    }

    private void ApplyStun(CombatUnit target, int duration)
    {
        for (int i = target.activeStatuses.Count - 1; i >= 0; i--)
        {
            if (target.activeStatuses[i].type == StatusType.Stun)
                target.activeStatuses.RemoveAt(i); // refresh, not stack
        }

        target.activeStatuses.Add(new ActiveStatus
        {
            type = StatusType.Stun,
            remainingTurns = duration
        });
    }

    private void RevertStatus(CombatUnit unit, ActiveStatus status)
    {
        if (status.type != StatusType.StatMod) return;

        unit.attack -= status.attackDelta;
        unit.defense -= status.defenseDelta;
        if (status.speedDelta != 0f)
            unit.ChangeSpeed(unit.speed - status.speedDelta);
    }
}