using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class CombatPartyHandler : MonoBehaviour
{
    [Header("Config")]
    [Tooltip("If true then all party object would be hidden normally aside from world member\n else then always show all, no logic will hide them, player party always shows")]
    [SerializeField] private bool isDefaultHideParty = true;
    [Tooltip("Single indicator of current selection")]
    [SerializeField] private GameObject _worldMember;
    [SerializeField] private List<CombatUnitVisual> _partyVisualList;
    public Vector3 WorldMemberPosition => _worldMember.transform.position;
    public List<UnitDataSO> partyUnitList;

    [Header("Animation")]
    [SerializeField] private float _undergroundOffset = 2f;
    [SerializeField] private AnimationCurve _showEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private AnimationCurve _hideEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private float[] _restingYPositions;
    private Tween[] _activeTweens;

    private Vector3 _worldMemberRestingPosition;
    private Tween _worldMemberTween;

    private enum PartyVisualState
    {
        WorldOnly,
        PartyShown,
        Transitioning
    }

    private PartyVisualState _state;

    void Awake()
    {
        _activeTweens = new Tween[_partyVisualList.Count];
        _restingYPositions = new float[_partyVisualList.Count];

        _worldMemberRestingPosition = _worldMember.transform.position;

        if (isDefaultHideParty)
        {
            SetPartyImmediate(false);
            _state = PartyVisualState.WorldOnly;
        }
        else
        {
            _state = PartyVisualState.PartyShown;
        }

        ShowSelectionAll(false);
    }

    // =========================
    // PUBLIC API
    // =========================

    public void ShowParty(bool isShow, float? playerXPos = null, float duration = 0.5f)
    {
        if (!isDefaultHideParty) return;

        if (_state == PartyVisualState.Transitioning)
            return;

        if (isShow && !playerXPos.HasValue)
        {
            Debug.LogError("ShowParty(true) requires playerXPos");
            return;
        }

        if (isShow && _state == PartyVisualState.PartyShown) return;
        if (!isShow && _state == PartyVisualState.WorldOnly) return;

        ShowPartyInternal(isShow, playerXPos, duration);
    }

    // =========================
    // CORE LOGIC
    // =========================

    private void ShowPartyInternal(bool isShow, float? playerXPos, float duration)
    {
        _state = PartyVisualState.Transitioning;

        if (isShow)
        {
            CaptureRestingY();
            AnimatePartyUp(playerXPos.Value, duration);
            AnimateWorldMember(false, duration);
        }
        else
        {
            AnimatePartyDown(duration);
            AnimateWorldMember(true, duration);
        }
    }

    private void CaptureRestingY()
    {
        for (int i = 0; i < _partyVisualList.Count; i++)
        {
            _restingYPositions[i] = _partyVisualList[i].transform.position.y;
        }
    }

    // =========================
    // PARTY ANIMATION
    // =========================

    private void AnimatePartyUp(float playerX, float duration)
    {
        int completed = 0;
        int total = _partyVisualList.Count;

        for (int i = 0; i < _partyVisualList.Count; i++)
        {
            var visual = _partyVisualList[i];
            _activeTweens[i]?.Kill();

            float restingY = _restingYPositions[i];

            Vector3 pos = visual.transform.position;
            pos.x = playerX + 11f;
            pos.y = restingY - _undergroundOffset;

            visual.transform.position = pos;
            visual.gameObject.SetActive(true);

            if (duration <= 0f)
            {
                pos.y = restingY;
                visual.transform.position = pos;
                completed++;
                continue;
            }

            _activeTweens[i] = visual.transform
                .DOMoveY(restingY, duration)
                .SetEase(_showEase)
                .OnComplete(() =>
                {
                    completed++;
                    if (completed >= total)
                        _state = PartyVisualState.PartyShown;
                });
        }

        if (duration <= 0f)
            _state = PartyVisualState.PartyShown;
    }

    private void AnimatePartyDown(float duration)
    {
        int completed = 0;
        int total = _partyVisualList.Count;

        for (int i = 0; i < _partyVisualList.Count; i++)
        {
            var visual = _partyVisualList[i];
            _activeTweens[i]?.Kill();

            if (!visual.gameObject.activeSelf)
            {
                completed++;
                continue;
            }

            float restingY = _restingYPositions[i];
            float targetY = restingY - _undergroundOffset;

            if (duration <= 0f)
            {
                visual.gameObject.SetActive(false);
                completed++;
                continue;
            }

            _activeTweens[i] = visual.transform
                .DOMoveY(targetY, duration)
                .SetEase(_hideEase)
                .OnComplete(() =>
                {
                    visual.gameObject.SetActive(false);
                    completed++;

                    if (completed >= total)
                        _state = PartyVisualState.WorldOnly;
                });
        }

        if (duration <= 0f)
            _state = PartyVisualState.WorldOnly;
    }

    // =========================
    // WORLD MEMBER
    // =========================

    private void AnimateWorldMember(bool isShow, float duration)
    {
        _worldMemberTween?.Kill();

        if (isShow)
        {
            Vector3 pos = _worldMemberRestingPosition;
            pos.y -= _undergroundOffset;

            _worldMember.transform.position = pos;
            _worldMember.SetActive(true);

            if (duration <= 0f)
            {
                _worldMember.transform.position = _worldMemberRestingPosition;
                return;
            }

            _worldMemberTween = _worldMember.transform
                .DOMoveY(_worldMemberRestingPosition.y, duration)
                .SetEase(_showEase);
        }
        else
        {
            if (!_worldMember.activeSelf) return;

            float targetY = _worldMemberRestingPosition.y - _undergroundOffset;

            if (duration <= 0f)
            {
                _worldMember.SetActive(false);
                return;
            }

            _worldMemberTween = _worldMember.transform
                .DOMoveY(targetY, duration)
                .SetEase(_hideEase)
                .OnComplete(() => _worldMember.SetActive(false));
        }
    }

    // =========================
    // INSTANT SETUP
    // =========================

    private void SetPartyImmediate(bool isShow)
    {
        for (int i = 0; i < _partyVisualList.Count; i++)
        {
            var visual = _partyVisualList[i];
            visual.gameObject.SetActive(isShow);
        }

        _worldMember.SetActive(!isShow);
    }

    // =========================
    // UI HELPERS
    // =========================

    public void ShowSelectionAll(bool isShow)
    {
        foreach (var visual in _partyVisualList)
            visual.selectionObject.SetActive(isShow);
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
            Debug.LogError($"Invalid memberIdx {memberIdx}");
            return null;
        }

        return _partyVisualList[memberIdx].canvasTarget;
    }

    void OnDestroy()
    {
        _worldMemberTween?.Kill();
        foreach (var tween in _activeTweens)
            tween?.Kill();
    }
}