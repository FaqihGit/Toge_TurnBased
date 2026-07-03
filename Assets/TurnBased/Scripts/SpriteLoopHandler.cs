using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SpriteLoopHandler : MonoBehaviour
{
    [SerializeField] private SpriteRenderer sr;
    [SerializeField] private List<Sprite> spriteSheet;
    [SerializeField] private int fpsCount;

    private Coroutine loopRoutine;

    public void StartLoping(bool isStart)
    {
        if (!isStart)
        {
            StopLoop();
            sr.sprite = spriteSheet.FirstOrDefault();
            return;
        }

        StopLoop();
        loopRoutine = StartCoroutine(LoopSprites());
    }

    public void SetXFlip(bool isFlip)
    {
        sr.flipX = isFlip;
    }

    private void StopLoop()
    {
        if (loopRoutine == null) return;

        StopCoroutine(loopRoutine);
        loopRoutine = null;
    }

    private IEnumerator LoopSprites()
    {
        if (spriteSheet.Count == 0 || fpsCount <= 0)
            yield break;

        var frameDelay = new WaitForSeconds(1f / fpsCount);
        var index = 0;

        while (true)
        {
            sr.sprite = spriteSheet[index];
            index = (index + 1) % spriteSheet.Count;
            yield return frameDelay;
        }
    }

    private void OnDisable()
    {
        StopLoop();
    }
}