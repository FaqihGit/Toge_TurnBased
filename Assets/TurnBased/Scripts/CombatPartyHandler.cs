using System.Collections.Generic;
using UnityEngine;

public class CombatPartyHandler : MonoBehaviour
{
    [Tooltip("If true then all party object would be hidden normally aside from world member\n else then always show all, no logic will hide them, player party always shows")]
    [SerializeField] private bool isDefaultHideParty = true;

    [Tooltip("Single indicator of current selection")]
    [SerializeField] private GameObject _targetIndicator; public GameObject targetIndicator => _targetIndicator;

    [SerializeField] private GameObject _worldMember;
    [SerializeField] private List<CombatUnitVisual> _partyVisualList;
    public List<UnitDataSO> partyUnitList;

    void Awake()
    {
        if (isDefaultHideParty) ShowParty(false);
        ShowIndicator(false);
        ShowSelectionAll(false);
    }

    public void ShowParty(bool isShow, float? playerXPos = null)
    {
        if (!isDefaultHideParty) return;

        if (isShow && !playerXPos.HasValue)
        {
            Debug.LogError($"Trying to show non player party but player pos not given");
            return;
        }

        if (isShow)
        {
            // show all party member
            foreach (var visual in _partyVisualList)
            {
                visual.gameObject.SetActive(true);
                var memberPos = visual.gameObject.transform.position;
                memberPos.x = playerXPos.Value + 11; // Hardcoded value as visually good offset position for combat
                visual.gameObject.transform.position = memberPos;
            }
            // Hide main member
            _worldMember.SetActive(false);
        }
        else
        {
            // hide all party member
            foreach (var visual in _partyVisualList) visual.gameObject.SetActive(false);
            // show main member
            _worldMember.SetActive(true);
        }
    }

    public void ShowIndicator(bool isShow)
    {
        _targetIndicator.SetActive(isShow);
    }

    public void SetIndicator(int memberIdx)
    {
        if (memberIdx < 0 || memberIdx >= _partyVisualList.Count) return;

        var targetPos = _targetIndicator.transform.position;
        targetPos.z = _partyVisualList[memberIdx].gameObject.transform.position.z;
        _targetIndicator.transform.position = targetPos;
    }

    public void ShowSelectionAll(bool isShow)
    {
        foreach (var visual in _partyVisualList)
        {
            visual.selectionObject.SetActive(isShow);
        }
    }

    public void ShowSelection(bool isShow, int memberIdx)
    {
        if (memberIdx < 0 || memberIdx >= _partyVisualList.Count) return;

        _partyVisualList[memberIdx].selectionObject.SetActive(isShow);
    }

    public Transform GetCanvasTarget(int memberIdx)
    {
        if (memberIdx < 0 || memberIdx >= _partyVisualList.Count)
        {
            Debug.LogError($"CombatPartyHandler: memberIdx {memberIdx} out of range for canvas target lookup");
            return null;
        }

        return _partyVisualList[memberIdx].canvasTarget;
    }
}