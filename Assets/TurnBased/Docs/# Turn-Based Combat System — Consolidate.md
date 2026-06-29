# Turn-Based Combat System — Consolidated Design Document

---

## 0. Overview

This document defines a turn-based combat system built around dynamic initiative and a persistent energy economy. It emphasizes flexibility, player expression, and predictable but non-static turn flow.

---

## 1. Core Design Pillars

* **Dynamic initiative:** Turn order is driven by Speed and updates continuously during combat.
* **Banked Energy:** Energy persists across turns instead of resetting.
* **Turn = action window:** Units can act multiple times per turn as long as they can afford it.
* **Two-axis targeting:** Actions are defined by:

  * Target faction: Ally or Enemy
  * Target scope: Single or All

---

## 2. Party & Encounter Structure

* Maximum **4 active units per side**
* **No reserve/bench system**
* Units cannot be swapped mid-combat
* Dead units:

  * Removed from turn queue
  * Remain visible (downed state)

### Entering Combat at 0 HP

* Units entering at ≤0 HP start at **1 HP instead**
* They act normally but will fall immediately if hit

---

## 3. Turn Order System — Speed Bank

### 3.1 Core Mechanic

Each unit has a **bank value** determining turn readiness.

#### Setup (once per encounter)

```
threshold = average Speed of all living units
for each unit i:
  bank[i] = speed[i]
```

#### Turn Selection Loop

```
loop:
  ready = units where bank >= threshold

  if ready not empty:
    actor = unit with highest bank
    (tie → player side priority)

    bank[actor] -= threshold
    return actor

  else:
    for each unit i:
      bank[i] += speed[i]
```

### 3.2 Key Rules

* Faster units act more frequently (proportional scaling)
* `threshold` is **fixed for entire encounter**
* Overflow is preserved (no reset to 0)

### 3.3 Speed Changes

When Speed changes:

```
delta = newSpeed - oldSpeed
speed[i] = newSpeed
bank[i] += delta
```

* Effect is **immediate** (affects turn order instantly)

### 3.4 Tie-Breaking

* Player-controlled units act first

---

## 4. Energy System

### 4.1 Energy Generation

| Trigger       | Gain                        |
| ------------- | --------------------------- |
| Turn start    | +1 (suppressed if Stunned)  |
| Hit by attack | +1 per hit (always applies) |

### 4.2 Core Rules

* Energy is **persistent (banked)**
* **Max Energy = 10**
* Overflow is **wasted**

### 4.3 Spending Energy

* Each action has a cost
* Multiple actions allowed per turn
* No 0-cost baseline action

**Guarantee:**

* Turn start grants +1 Energy → ensures at least one action if cheapest cost ≤1

### 4.4 Ending a Turn

A turn ends when:

1. Player manually selects **Skip**
2. No actions are affordable

---

## 5. Action Types

| Action | Target     | Cost        | Notes                                                 |
| ------ | ---------- | ----------- | ----------------------------------------------------- |
| Attack | Enemy      | ≤1          | Always usable                                         |
| Skill  | Ally/Enemy | Medium–High | Damage, buffs, debuffs, heals, Stun                   |
| Guard  | Self       | 0           | Reduces damage; synergizes with hit-based Energy gain |
| Item   | Varies     | Low/0       | Utility                                               |
| Skip   | —          | 0           | Ends turn                                             |

### Design Decisions

* No Taunt mechanic
* Stun is a **status effect**, not an action

---

## 6. Targeting System

Strict rules:

* **Faction:** Ally or Enemy only
* **Scope:** Single or All only

|       | Single    | All                |
| ----- | --------- | ------------------ |
| Enemy | Attack    | AoE damage         |
| Ally  | Heal/Buff | Party-wide effects |

* No mixed targeting
* No random/row targeting

---

## 7. Stats

| Stat    | Function                 |
| ------- | ------------------------ |
| HP      | Survival                 |
| Energy  | Action resource (cap 10) |
| Speed   | Turn order               |
| Attack  | Damage output            |
| Defense | Damage reduction         |

* No separate magic stats

---

## 8. Status Effects

### General Rules

* Duration ticks at **start of unit’s turn**
* No stacking → reapply = refresh duration

### Speed Buffs/Debuffs

* % based modifiers
* Apply immediate effect to:

  * Speed
  * Bank (turn position)

### Stun

* Skips turn completely
* No action menu
* **No turn-start Energy gain**
* Still gains Energy when hit

---

## 9. Combat Flow

1. Determine next actor (Speed system)
2. Start-of-turn:

   * +1 Energy (unless Stunned)
   * Status effects tick
3. Choose action
4. Choose target
5. Resolve action:

   * Apply effects
   * Deduct Energy
   * Trigger on-hit Energy
6. Continue or end turn
7. Check win/loss

---

## 10. Win / Loss / Escape

### Conditions

* **Victory:** all enemies defeated
* **Defeat:** all player units defeated

### Escape (Run)

* Available anytime
* Not tied to turn system
* No cost
* 100% success

### Escape Result

* Full rollback:

  * Restore party HP, Energy, status
  * Reset encounter completely
  * Return to exploration state

---

## 11. Enemy AI (Baseline)

Priority logic:

1. Use support skill if high-value condition met
2. Use highest-cost affordable damage skill
3. Target lowest HP enemy
4. Skip if saving Energy is beneficial

---

## 12. UI / UX Guidelines

* Turn order preview (dynamic, updates instantly)
* Visible Energy for all units
* Grey out unaffordable actions
* Clear ally vs enemy targeting visuals
* Distinct **Run** button (not tied to unit menu)

---

## 13. System Identity

This combat system is defined by:

* **Proportional turn frequency via Speed Bank**
* **Persistent Energy economy**
* **Expandable turns (multi-action windows)**
* **Status effects that interact with turn economy (e.g., Stun, Speed)**

---