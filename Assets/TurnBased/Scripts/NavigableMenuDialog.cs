using System;
using System.Linq;
using Fungus;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Extends Fungus' MenuDialog with explicit navigation methods so option
/// selection can be driven from an Input System action map (e.g. a
/// "Dialogue" map's Selection / Select actions) instead of relying solely
/// on Unity's built-in UI navigation + InputSystemUIInputModule.
///
/// Kept as a subclass rather than editing MenuDialog.cs directly so
/// Fungus library updates can still be pulled in cleanly.
/// </summary>
public class NavigableMenuDialog : MenuDialog
{
    public Action<int> OnOptionSelection;
    [Header("Navigation")]
    [Tooltip("Minimum |Selection| input value before a directional move is registered. Prevents stick drift/noise from triggering repeated moves.")]
    [SerializeField] private float selectionDeadzone = 0.5f;

    /// <summary>
    /// Deadzone used by external input handlers deciding whether a Selection value should trigger a move.
    /// </summary>
    public float SelectionDeadzone => selectionDeadzone;

    /// <summary>
    /// Currently active and interactable buttons, in hierarchy order.
    /// Inactive/disabled options (e.g. hidden via AddOption's hideOption flag) are excluded.
    /// </summary>
    private Button[] ActiveOptions =>
        CachedButtons.Where(b => b != null && b.gameObject.activeSelf && b.interactable).ToArray();

    /// <summary>
    /// Moves the highlighted option to the next one in the list, wrapping around.
    /// </summary>
    public void SelectNextOption() => Move(1);

    /// <summary>
    /// Moves the highlighted option to the previous one in the list, wrapping around.
    /// </summary>
    public void SelectPreviousOption() => Move(-1);

    /// <summary>
    /// Highlights the option at the given index among currently active/interactable options.
    /// </summary>
    public void SelectOption(int index)
    {
        var options = ActiveOptions;
        if (index < 0 || index >= options.Length) return;

        EventSystem.current.SetSelectedGameObject(options[index].gameObject);

        OnOptionSelection?.Invoke(index);
    }

    /// <summary>
    /// Invokes the onClick of whichever option is currently highlighted, as if it were clicked.
    /// Use this from a "Select / Confirm" input action.
    /// </summary>
    public void ConfirmSelection()
    {
        var selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null) return;

        var button = ActiveOptions.FirstOrDefault(b => b.gameObject == selected);
        if (button != null)
        {
            button.onClick.Invoke();
        }
    }

    /// <summary>
    /// True while this menu has at least one selectable option visible.
    /// </summary>
    public bool HasActiveOptions => ActiveOptions.Length > 0;

    private void Move(int direction)
    {
        var options = ActiveOptions;
        if (options.Length == 0) return;

        var current = EventSystem.current.currentSelectedGameObject;
        int currentIndex = current != null
            ? System.Array.FindIndex(options, b => b.gameObject == current)
            : -1;

        int nextIndex = currentIndex < 0
            ? 0
            : Mathf.Clamp(currentIndex + direction, 0, options.Length - 1);

        SelectOption(nextIndex);
    }
}