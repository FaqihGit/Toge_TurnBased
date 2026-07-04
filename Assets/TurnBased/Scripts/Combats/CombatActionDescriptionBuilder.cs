using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// Builds a human-readable description of a CombatActionSO (used for the
/// Fungus ActionPrompt / tooltip text). Pure data -> string, no Unity
/// lifecycle needed, so it's a plain static class rather than a MonoBehaviour.
///
/// Damage and Buff/Heal are mutually exclusive per GDD 5 — this branches
/// once on isBuff and never mixes segments across the two branches.
public static class CombatActionDescriptionBuilder
{
    private const string B_OPEN = "{b}";
    private const string B_CLOSE = "{/b}";

    private const string I_OPEN = "{i}";
    private const string I_CLOSE = "{/i}";

    public static string Build(CombatUnit unit, CombatActionSO action)
    {
        return action.isBuff ? BuildBuffDescription(action) : BuildDamageDescription(unit, action);
    }

    private static string BuildDamageDescription(CombatUnit unit, CombatActionSO action)
    {
        var sb = new StringBuilder();
        if (action.damageMult >= 0f)
            sb.Append($"Deal {B_OPEN}{(unit.source.stats.combatAttack * action.damageMult):0.##}{B_CLOSE} damage to {TargetPhrase(action)}");
        else
            sb.Append($"{I_OPEN}Heal{I_CLOSE} for {B_OPEN}{-action.damageMult:0.##}{B_CLOSE} health to {TargetPhrase(action)}");

        if (action.appliesStun)
            sb.Append($" and applies {I_OPEN}Stun{I_CLOSE} for {B_OPEN}{action.buffTurn}{B_CLOSE} turns");

        if (action.isIgnoreDef)
            sb.Append($" {I_OPEN}(ignores defense){I_CLOSE}");

        return sb.ToString();
    }

    private static string BuildBuffDescription(CombatActionSO action)
    {
        var segments = new List<string>();

        string statPhrase = BuffStatPhrase(action.buffValue);
        if (!string.IsNullOrEmpty(statPhrase))
            segments.Add($"Buff {statPhrase}");

        if (action.isGuard)
            segments.Add($"Apply {I_OPEN}Guard{I_CLOSE}");

        if (segments.Count == 0)
            return $"No effect to {TargetPhrase(action)}";

        string body = JoinWithAnd(segments);
        string turnWord = action.buffTurn == 1 ? "turn" : "turns";
        return $"{body} to {TargetPhrase(action)} for {B_OPEN}{action.buffTurn}{B_CLOSE} {turnWord}";
    }

    private static string BuffStatPhrase(UnitStats stats)
    {
        var parts = new List<string>();
        if (stats.combatAttack != 0) parts.Add($"{B_OPEN}{Signed(stats.combatAttack)}{B_CLOSE} {I_OPEN}attack{I_CLOSE}");
        if (stats.combatDefense != 0) parts.Add($"{B_OPEN}{Signed(stats.combatDefense)}{B_CLOSE} {I_OPEN}defense{I_CLOSE}");
        if (stats.combatSpeed != 0) parts.Add($"{B_OPEN}{Signed(stats.combatSpeed)}{B_CLOSE} {I_OPEN}speed{I_CLOSE}");
        return parts.Count == 0 ? string.Empty : string.Join(", ", parts);
    }

    private static string Signed(float value) => value > 0 ? $"+{value:0.##}" : value.ToString("0.##");

    private static string TargetPhrase(CombatActionSO action)
    {
        bool partyWide = CombatTargetSelector.IsPartyWide(action);
        string who = action.target == CombatTargetEnum.Allies ? "targets" : "targets";

        if (partyWide)
            return $"all {who}";

        int count = Mathf.Max(1, action.targetCount);
        return count == 1 ? $"1 {Singular(who)}" : $"{count} {who}";
    }

    private static string Singular(string plural) => plural == "allies" ? "target" : "target";

    private static string JoinWithAnd(List<string> segments)
    {
        return segments.Count == 1 ? segments[0] : string.Join(" and ", segments);
    }
}