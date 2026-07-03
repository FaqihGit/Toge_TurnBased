using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class CombatPartyHandler : MonoBehaviour
{
    [Tooltip("Single indicator of current selection")]
    [SerializeField] private GameObject _worldMember;
    [SerializeField] private List<CombatUnitVisual> _partyVisualList;
    public Vector3 WorldMemberPosition => _worldMember.transform.position;
    [HideInInspector] public List<UnitDataSO> partyUnitList;

    [Header("Animation")]
    [SerializeField] private float _undergroundOffset = -2f;
    [SerializeField] private AnimationCurve _showEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private AnimationCurve _hideEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
    private Tween[] _activeTweens;

    [Header("Action Feedback")]
    [SerializeField] private float _thrustDistance = 0.6f;
    [SerializeField] private float _thrustOutDuration = 0.12f;
    [SerializeField] private float _thrustReturnDuration = 0.18f;
    [SerializeField] private float _hitShakeStrength = 0.15f;
    [SerializeField] private int _hitShakeVibrato = 20;
    [SerializeField] private float _hitShakeDuration = 0.3f;
    private Tween[] _actionTweens;
    private Tween[] _shakeTweens;
    private Tween _worldMemberTween;

    private enum PartyVisualState
    {
        WorldOnly,
        PartyShown,
        Transitioning
    }

    private PartyVisualState _state;

    /// Number of party slots that should actually be visible, driven by
    /// partyUnitList and capped to however many visual slots exist (4).
    private int ActivePartyCount =>
        partyUnitList != null
            ? Mathf.Clamp(partyUnitList.Count, 0, _partyVisualList.Count)
            : _partyVisualList.Count;

    void Awake()
    {
        _activeTweens = new Tween[_partyVisualList.Count];
        _actionTweens = new Tween[_partyVisualList.Count];
        _shakeTweens = new Tween[_partyVisualList.Count];
        _activeTweens = new Tween[_partyVisualList.Count];

        SetPartyImmediate(false);
        _state = PartyVisualState.WorldOnly;

        ShowSelectionAll(false);
    }

    // =========================
    // PUBLIC API
    // =========================

    public void ShowParty(bool isShow, float? playerXPos = null, float duration = 1)
    {
        if (_state == PartyVisualState.Transitioning)
            return;

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
            AnimateWorldMember(false, duration);
            AnimatePartyUp(playerXPos, duration);
        }
        else
        {
            AnimateWorldMember(true, duration);
            AnimatePartyDown(duration);
        }
    }

    // =========================
    // PARTY ANIMATION
    // =========================

    private void AnimatePartyUp(float? playerX, float duration)
    {
        int activeCount = ActivePartyCount;
        int completed = 0;

        for (int i = 0; i < _partyVisualList.Count; i++)
        {
            var visual = _partyVisualList[i];
            _activeTweens[i]?.Kill();

            // Slots beyond the current party size stay hidden entirely.
            if (i >= activeCount)
            {
                if (visual.gameObject.activeSelf)
                    visual.gameObject.SetActive(false);
                continue;
            }

            Vector3 pos = visual.transform.position;
            if (playerX.HasValue) pos.x = playerX.Value + 11f;
            visual.transform.position = pos;

            Vector3 localPos = visual.transform.localPosition;
            localPos.y = _undergroundOffset;
            visual.transform.localPosition = localPos;

            visual.gameObject.SetActive(true);

            if (duration <= 0f)
            {
                localPos.y = 0;
                visual.transform.localPosition = localPos;
                completed++;
                continue;
            }

            _activeTweens[i] = visual.transform
                .DOLocalMoveY(0, duration)
                .SetEase(_showEase)
                .OnComplete(() =>
                {
                    completed++;
                    if (completed >= activeCount)
                        _state = PartyVisualState.PartyShown;
                });
        }

        // Nothing to animate (party of 0) or instant application already
        // resolved every active slot synchronously.
        if (activeCount == 0 || completed >= activeCount)
            _state = PartyVisualState.PartyShown;
    }

    private void AnimatePartyDown(float duration)
    {
        int activeCount = ActivePartyCount;
        int completed = 0;
        int total = _partyVisualList.Count;

        for (int i = 0; i < _partyVisualList.Count; i++)
        {
            var visual = _partyVisualList[i];
            _activeTweens[i]?.Kill();

            // Slots beyond the current party size are already excluded /
            // get force-hidden, and count as immediately "done".
            if (i >= activeCount)
            {
                if (visual.gameObject.activeSelf)
                    visual.gameObject.SetActive(false);
                completed++;
                continue;
            }

            if (!visual.gameObject.activeSelf)
            {
                completed++;
                continue;
            }

            if (duration <= 0f)
            {
                visual.gameObject.SetActive(false);
                completed++;
                continue;
            }

            _activeTweens[i] = visual.transform
                .DOLocalMoveY(_undergroundOffset, duration)
                .SetEase(_hideEase)
                .OnComplete(() =>
                {
                    visual.gameObject.SetActive(false);
                    completed++;

                    if (completed >= total)
                        _state = PartyVisualState.WorldOnly;
                });
        }

        if (completed >= total)
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
            Vector3 pos = _worldMember.transform.localPosition;
            pos.y = _undergroundOffset;

            _worldMember.transform.localPosition = pos;
            _worldMember.SetActive(true);

            if (duration <= 0f)
            {
                pos.y = 0;
                _worldMember.transform.localPosition = pos;
                return;
            }

            _worldMemberTween = _worldMember.transform
                .DOLocalMoveY(0, duration)
                .SetEase(_showEase);
        }
        else
        {
            if (!_worldMember.activeSelf) return;

            if (duration <= 0f)
            {
                _worldMember.SetActive(false);
                return;
            }

            _worldMemberTween = _worldMember.transform
                .DOLocalMoveY(_undergroundOffset, duration)
                .SetEase(_hideEase)
                .OnComplete(() => _worldMember.SetActive(false));
        }
    }

    // =========================
    // INSTANT SETUP
    // =========================

    private void SetPartyImmediate(bool isShow)
    {
        int activeCount = ActivePartyCount;

        for (int i = 0; i < _partyVisualList.Count; i++)
        {
            var visual = _partyVisualList[i];
            visual.gameObject.SetActive(isShow && i < activeCount);
        }

        if (_worldMember) _worldMember.SetActive(!isShow);
    }

    // =========================
    // UI HELPERS
    // =========================

    public void ShowSelectionAll(bool isShow)
    {
        int activeCount = ActivePartyCount;

        for (int i = 0; i < _partyVisualList.Count; i++)
            _partyVisualList[i].selectionObject.SetActive(isShow && i < activeCount);
    }

    public void ShowSelection(bool isShow, int memberIdx)
    {
        if (memberIdx < 0 || memberIdx >= _partyVisualList.Count) return;
        if (memberIdx >= ActivePartyCount) return;

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

    // =========================
    // ACTION FEEDBACK
    // =========================

    /// Short out-and-back thrust along X. isBackward=true for enemies (they
    /// thrust toward negative X, i.e. toward the player side). onPeak fires
    /// at the forward-most point — CombatManager resolves damage there so
    /// the hit visually lands at the moment of impact.
    public void PlayAttackThrust(int memberIdx, bool isBackward, Action onPeak = null, Action onComplete = null)
    {
        if (memberIdx < 0 || memberIdx >= _partyVisualList.Count) return;

        Transform t = _partyVisualList[memberIdx].transform;
        _actionTweens[memberIdx]?.Kill();

        Vector3 origin = t.position;
        float dir = isBackward ? -1f : 1f;
        Vector3 peak = origin + new Vector3(_thrustDistance * dir, 0f, 0f);

        Sequence seq = DOTween.Sequence();
        seq.Append(t.DOMove(peak, _thrustOutDuration).SetEase(Ease.OutQuad));
        seq.AppendCallback(() => onPeak?.Invoke());
        seq.Append(t.DOMove(origin, _thrustReturnDuration).SetEase(Ease.InQuad));
        seq.OnComplete(() => onComplete?.Invoke());

        _actionTweens[memberIdx] = seq;
    }

    /// Small X-axis vibrate to indicate a unit is on the receiving end of
    /// an action (damage, heal, buff — anything that targets it).
    public void PlayHitShake(int memberIdx, Action onComplete = null)
    {
        if (memberIdx < 0 || memberIdx >= _partyVisualList.Count) return;

        Transform t = _partyVisualList[memberIdx].transform;
        _shakeTweens[memberIdx]?.Kill();

        _shakeTweens[memberIdx] = t.DOShakePosition(
                _hitShakeDuration,
                strength: new Vector3(_hitShakeStrength, 0f, 0f),
                vibrato: _hitShakeVibrato,
                randomness: 0f,
                fadeOut: true)
            .OnComplete(() => onComplete?.Invoke());
    }

    void OnDestroy()
    {
        _worldMemberTween?.Kill();
        foreach (var tween in _activeTweens) tween?.Kill();
        foreach (var tween in _actionTweens) tween?.Kill();
        foreach (var tween in _shakeTweens) tween?.Kill();
    }
}