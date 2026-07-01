using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime-only combat representation of a unit. Stats are copied out of the
/// source UnitDataSO when combat starts, so combat can freely mutate them
/// without touching the persistent asset. The asset is only written back via
/// an explicit CommitToSource() call (see CombatManager: on victory, or on
/// escape after the snapshot is restored).
/// </summary>
public class CombatUnit
{
    public readonly UnitDataSO source;
    public readonly UnitFactionEnum faction;

    public float maxHealth;
    public float currentHealth;
    public float attack;
    public float defense;
    public float speed;

    public float speedBank;
    public int energy;

    public readonly List<ActiveStatus> activeStatuses = new();

    public bool IsDead => currentHealth <= 0f;

    public bool IsStunned
    {
        get
        {
            foreach (var s in activeStatuses)
                if (s.type == StatusType.Stun) return true;
            return false;
        }
    }

    public bool IsGuarding
    {
        get
        {
            foreach (var s in activeStatuses)
                if (s.type == StatusType.StatMod && s.isGuard) return true;
            return false;
        }
    }

    public CombatUnit(UnitDataSO source, UnitFactionEnum faction)
    {
        this.source = source;
        this.faction = faction;

        maxHealth = source.stats.maxHealth;
        currentHealth = source.stats.currentHealth;
        attack = source.stats.combatAttack;
        defense = source.stats.combatDefense;
        speed = source.stats.combatSpeed;

        energy = 0;
        speedBank = 0f;
    }

    /// GDD 3.3: a Speed change is immediate — it affects both the live Speed
    /// stat and the unit's current bank position (delta applied to both).
    public void ChangeSpeed(float newSpeed)
    {
        float delta = newSpeed - speed;
        speed = newSpeed;
        speedBank += delta;
    }

    public CombatUnitSnapshot CaptureSnapshot()
    {
        return new CombatUnitSnapshot
        {
            unit = this,
            maxHealth = maxHealth,
            currentHealth = currentHealth,
            attack = attack,
            defense = defense,
            speed = speed,
            energy = energy
        };
    }

    public void RestoreSnapshot(CombatUnitSnapshot snapshot)
    {
        maxHealth = snapshot.maxHealth;
        currentHealth = snapshot.currentHealth;
        attack = snapshot.attack;
        defense = snapshot.defense;
        speed = snapshot.speed;
        energy = snapshot.energy;
        speedBank = 0f;
        activeStatuses.Clear();
    }

    /// Writes final runtime stats back to the source asset. Only HP is
    /// persisted — Energy/Speed/bank are combat-only and reset next encounter.
    public void CommitToSource()
    {
        source.stats.currentHealth = currentHealth;
    }
}

public struct CombatUnitSnapshot
{
    public CombatUnit unit;
    public float maxHealth;
    public float currentHealth;
    public float attack;
    public float defense;
    public float speed;
    public int energy;
}

public enum StatusType { Stun, StatMod }

public class ActiveStatus
{
    public StatusType type;
    public int remainingTurns;

    public CombatActionSO sourceAction;

    public float attackDelta;
    public float defenseDelta;
    public float speedDelta;

    public bool isGuard;
}