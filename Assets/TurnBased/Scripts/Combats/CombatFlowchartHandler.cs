using System;
using System.Collections.Generic;
using Fungus;

/// Owns all Fungus/Flowchart interop for the combat action menu — reading
/// ActionIdx, writing option/interactable variables, building display
/// strings. Reports a validated action choice via OnActionChosen;
/// CombatManager decides what to do with it (BeginTargeting) and remains
/// the actual Fungus call target since Fungus can only invoke MonoBehaviours.
public class CombatFlowchartHandler
{
    private Flowchart combatFlowchart;

    private static readonly string ActionPromptVariable = "ActionPrompt";

    private static readonly string[] OptionVariables =
    {
        "OptionA", "OptionB", "OptionC", "OptionD"
    };

    private static readonly string[] InteractableVariables =
    {
        "InteractableA", "InteractableB", "InteractableC", "InteractableD"
    };

    public event Action<CombatActionSO> OnActionChosen;

    public void Init(Flowchart combatFlowchart)
    {
        this.combatFlowchart = combatFlowchart;
    }

    public void TriggerActionBlock()
    {
        combatFlowchart.ExecuteBlock("TriggerAction");
    }

    /// canAct bundles isCombatActive / awaitingAction / !IsAwaitingTarget —
    /// those are CombatManager's state, so it computes the flag and passes
    /// it in rather than this class reaching back out to check them.
    public void SubmitFlowchartAction(bool canAct, CombatUnit currentActor)
    {
        if (!canAct || currentActor == null) return;

        int actionIdx = combatFlowchart.GetIntegerVariable("ActionIdx");
        var actionList = currentActor.source.combatActionList;
        if (actionIdx < 0 || actionIdx >= actionList.Count) return;

        var action = actionList[actionIdx];
        if (action.energyCost > currentActor.energy) return;

        OnActionChosen?.Invoke(action);
    }

    public void SetActionOption(CombatUnit unit, List<CombatActionSO> actionList)
    {
        for (int i = 0; i < OptionVariables.Length; i++)
        {
            bool hasAction = i < actionList.Count;

            combatFlowchart.SetStringVariable(
                OptionVariables[i],
                hasAction
                    ? ActionStringBuilder(actionList[i].name, actionList[i].energyCost)
                    : "Unavailable");

            combatFlowchart.SetBooleanVariable(
                InteractableVariables[i],
                hasAction && unit.energy >= actionList[i].energyCost);
        }
    }

    private static string ActionStringBuilder(string name, int energy)
    {
        return $"{name}\n E:{energy}";
    }

    public void SetActionPrompt(string prompt)
    {
        combatFlowchart.SetStringVariable(ActionPromptVariable, string.IsNullOrEmpty(prompt) ? "Choose your action" : prompt);
    }
}