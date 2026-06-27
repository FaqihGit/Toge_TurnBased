using System.Collections.Generic;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;

public class CombatPartyHandler : MonoBehaviour
{
    [Tooltip("If true then all party object would be hidden normally aside from world member\n else then always show all, no logic will hide them, player party always shows")]
    [SerializeField] private bool isDefaultHideParty = true;

    [Tooltip("Single indicator of current selection")]
    [SerializeField] private GameObject _targetIndicator; public GameObject targetIndicator => _targetIndicator;

    [SerializeField] private GameObject _worldMember;
    [SerializeField] private List<GameObject> _partyObjectList;
    [Tooltip("Visual cached indicator of which has been chosen in this turn")]
    [SerializeField] private List<GameObject> _partySelectedIndicatorList; public List<GameObject> partySelectedIndicatorList => _partySelectedIndicatorList;
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
            foreach (var member in _partyObjectList)
            {
                member.SetActive(true);
                var memberPos = member.transform.position;
                memberPos.x = playerXPos.Value + 11; // Hardcoded value as visually good offset position for combat
                member.transform.position = memberPos;
            }
            // Hide main member
            _worldMember.SetActive(false);
        }
        else
        {
            // hide all party member
            foreach (var member in _partyObjectList) member.SetActive(false);
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
        if (memberIdx < 0 || memberIdx >= _partyObjectList.Count) return;

        var targetPos = _targetIndicator.transform.position;
        targetPos.z = _partyObjectList[memberIdx].transform.position.z;
        _targetIndicator.transform.position = targetPos;
    }

    public void ShowSelectionAll(bool isShow)
    {
        foreach (var member in _partySelectedIndicatorList)
        {
            member.SetActive(isShow);
        }
    }

    public void ShowSelection(bool isShow, int memberIdx)
    {
        if (memberIdx < 0 || memberIdx >= _partySelectedIndicatorList.Count) return;

        _partySelectedIndicatorList[memberIdx].SetActive(isShow);
    }
}
