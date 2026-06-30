using System;
using System.Collections.Generic;

/// <summary>
/// Plain C# class (not a MonoBehaviour) implementing the baseline enemy AI
/// from GDD §11. Stateless and pure: every call receives a full snapshot of
/// the relevant combat state and returns a single Decision. It never touches
/// CombatManager, CombatUnit mutation, or UI — CombatManager is the only
/// thing that calls SubmitAction/SubmitSkip with the result.
///
/// Mirrors CombatResolver's role: math/logic extracted out of CombatManager.
/// </summary>
public class EnemyCombatAI
{
    /// GDD 11.1: support/heal skill triggers if an ally's HP fraction is at
    /// or below this threshold.
    private const float SupportHpThreshold = 0.5f;

    /// GDD 11.4: if the best affordable damage action costs less than this,
    /// and the actor still has headroom to bank more Energy, skip instead of
    /// spending on a token-cost action.
    private const int MinMeaningfulCost = 2;

    public readonly struct Decision
    {
        public readonly CombatActionSO action;
        public readonly List<CombatUnit> targets;
        public readonly bool skip;

        private Decision(CombatActionSO action, List<CombatUnit> targets, bool skip)
        {
            this.action = action;
            this.targets = targets;
            this.skip = skip;
        }

        public static Decision Act(CombatActionSO action, List<CombatUnit> targets) => new(action, targets, false);
        public static Decision Skip() => new(null, null, true);
    }

    /// <param name="actor">The enemy unit whose turn it is.</param>
    /// <param name="availableActions">actor.source.combatActionList.</param>
    /// <param name="allies">Other units on actor's side (for support targeting).</param>
    /// <param name="maxEnergy">CombatManager.MaxEnergy, passed in — AI never assumes a constant.</param>
    /// <param name="getValidTargets">Bound to CombatManager.GetValidTargets(actor, action) by the caller.</param>
    public Decision DecideAction(
        CombatUnit actor,
        List<CombatActionSO> availableActions,
        List<CombatUnit> allies,
        int maxEnergy,
        Func<CombatActionSO, List<CombatUnit>> getValidTargets)
    {
        var affordable = new List<CombatActionSO>();
        foreach (var a in availableActions)
            if (a.energyCost <= actor.energy) affordable.Add(a);

        if (affordable.Count == 0) return Decision.Skip();

        // --- 1. Support skill if a high-value condition is met -------------
        var support = TrySupport(affordable, allies, getValidTargets);
        if (support.HasValue) return support.Value;

        // --- 2 & 3. Highest-cost affordable damage skill, lowest-HP target -
        var damage = TryDamage(affordable, getValidTargets);

        // --- 4. Skip if saving Energy is more valuable ----------------------
        if (damage.HasValue)
        {
            bool cheapPlay = damage.Value.action.energyCost < MinMeaningfulCost;
            bool hasHeadroom = actor.energy < maxEnergy;
            if (cheapPlay && hasHeadroom)
                return Decision.Skip();

            return damage.Value;
        }

        return Decision.Skip();
    }

    private Decision? TrySupport(
        List<CombatActionSO> affordable,
        List<CombatUnit> allies,
        Func<CombatActionSO, List<CombatUnit>> getValidTargets)
    {
        CombatActionSO bestAction = null;
        List<CombatUnit> bestTargets = null;

        foreach (var action in affordable)
        {
            if (action.target != CombatTargetEnum.Allies) continue;

            var validPool = getValidTargets(action);
            CombatUnit neediest = FindLowestHpFraction(validPool, out float neediestFraction);
            if (neediest == null || neediestFraction > SupportHpThreshold) continue;

            // High-value condition met. Prefer the highest-cost qualifying
            // support skill, consistent with rule 2's "highest-cost" framing.
            if (bestAction == null || action.energyCost > bestAction.energyCost)
            {
                bestAction = action;
                bestTargets = CombatTargetSelector.IsPartyWide(action)
                    ? validPool
                    : new List<CombatUnit> { neediest };
            }
        }

        return bestAction == null ? null : Decision.Act(bestAction, bestTargets);
    }

    private Decision? TryDamage(
        List<CombatActionSO> affordable,
        Func<CombatActionSO, List<CombatUnit>> getValidTargets)
    {
        CombatActionSO bestAction = null;
        List<CombatUnit> bestPool = null;

        foreach (var action in affordable)
        {
            if (action.target != CombatTargetEnum.Foes) continue;
            if (bestAction == null || action.energyCost > bestAction.energyCost)
            {
                bestAction = action;
                bestPool = getValidTargets(action);
            }
        }

        if (bestAction == null || bestPool == null || bestPool.Count == 0) return null;

        if (CombatTargetSelector.IsPartyWide(bestAction))
            return Decision.Act(bestAction, bestPool);

        // GDD 11.3: target lowest HP enemy. Sort ascending by current HP and
        // take up to targetCount.
        bestPool.Sort((a, b) => a.currentHealth.CompareTo(b.currentHealth));
        int count = Math.Max(1, Math.Min(bestAction.targetCount, bestPool.Count));
        var targets = bestPool.GetRange(0, count);

        return Decision.Act(bestAction, targets);
    }

    private CombatUnit FindLowestHpFraction(List<CombatUnit> pool, out float fraction)
    {
        CombatUnit lowest = null;
        fraction = float.MaxValue;

        foreach (var u in pool)
        {
            if (u.maxHealth <= 0f) continue;
            float f = u.currentHealth / u.maxHealth;
            if (f < fraction)
            {
                fraction = f;
                lowest = u;
            }
        }

        return lowest;
    }
}