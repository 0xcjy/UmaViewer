using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WashLightAnimTest : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Animation targetAnimation;
    [SerializeField] private bool searchInChildren = false;

    [Header("Optional Manual Override")]
    [SerializeField] private string openClipName = "";
    [SerializeField] private string loopClipName = "";

    [Header("Auto Match Keywords")]
    [SerializeField] private string requiredKeyword = "wash";
    [SerializeField] private string openKeyword = "open";
    [SerializeField] private string loopKeyword1 = "circle";
    [SerializeField] private string loopKeyword2 = "loop";

    [Header("Playback")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool restartOnEnable = false;
    [SerializeField] private bool logClips = true;

    private Coroutine playRoutine;

    void Start()
    {
        if (playOnStart)
            BeginTest();
    }

    void OnEnable()
    {
        if (restartOnEnable && Application.isPlaying)
            BeginTest();
    }

    [ContextMenu("Begin Test")]
    public void BeginTest()
    {
        ResolveAnimation();

        if (targetAnimation == null)
        {
            Debug.LogWarning($"[WashLightAnimTest] No Animation found on {name}");
            return;
        }

        if (playRoutine != null)
            StopCoroutine(playRoutine);

        PrepareAnimation(targetAnimation);
        playRoutine = StartCoroutine(PlaySequence());
    }

    [ContextMenu("Stop Test")]
    public void StopTest()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        if (targetAnimation != null)
            targetAnimation.Stop();
    }

    private void ResolveAnimation()
    {
        if (targetAnimation != null)
            return;

        targetAnimation = searchInChildren
            ? GetComponentInChildren<Animation>(true)
            : GetComponent<Animation>();
    }

    private void PrepareAnimation(Animation anim)
    {
        anim.playAutomatically = false;
        anim.Stop();

        foreach (AnimationState st in anim)
        {
            st.layer = 0;
            st.weight = 1f;
            st.enabled = true;
        }

        if (logClips)
        {
            List<string> names = new List<string>();
            foreach (AnimationState st in anim)
                names.Add(st.name);

            Debug.Log($"[WashLightAnimTest] {gameObject.name} clips: {string.Join(", ", names)}");
        }
    }

    private IEnumerator PlaySequence()
    {
        string open = !string.IsNullOrEmpty(openClipName)
            ? FindExactClip(targetAnimation, openClipName)
            : FindBestOpenClip(targetAnimation);

        string loop = !string.IsNullOrEmpty(loopClipName)
            ? FindExactClip(targetAnimation, loopClipName)
            : FindBestLoopClip(targetAnimation);

        if (logClips)
            Debug.Log($"[WashLightAnimTest] Selected open={open ?? "null"}, loop={loop ?? "null"}");

        if (!string.IsNullOrEmpty(open))
        {
            AnimationClip openClip = targetAnimation.GetClip(open);
            if (openClip != null)
            {
                targetAnimation.wrapMode = WrapMode.Once;
                targetAnimation.Play(open);
                yield return new WaitForSeconds(openClip.length);
            }
        }

        if (!string.IsNullOrEmpty(loop))
        {
            AnimationState st = targetAnimation[loop];
            if (st != null)
                st.wrapMode = WrapMode.Loop;

            targetAnimation.wrapMode = WrapMode.Loop;
            targetAnimation.Play(loop);
        }
        else if (!string.IsNullOrEmpty(open))
        {
            // 没有 loop 的话就停在 open 最后一帧也行
            AnimationState st = targetAnimation[open];
            if (st != null)
            {
                st.speed = 0f;
                st.time = targetAnimation.GetClip(open).length;
            }
        }
        else
        {
            Debug.LogWarning($"[WashLightAnimTest] No suitable wash light clips found on {gameObject.name}");
        }
    }

    private string FindExactClip(Animation anim, string clipName)
    {
        if (anim.GetClip(clipName) != null)
            return clipName;

        foreach (AnimationState st in anim)
        {
            if (string.Equals(st.name, clipName, System.StringComparison.OrdinalIgnoreCase))
                return st.name;
        }

        return null;
    }

    private string FindBestOpenClip(Animation anim)
    {
        string fallback = null;

        foreach (AnimationState st in anim)
        {
            string n = st.name.ToLowerInvariant();

            if (!string.IsNullOrEmpty(requiredKeyword) && !n.Contains(requiredKeyword.ToLowerInvariant()))
                continue;

            if (n.Contains(openKeyword.ToLowerInvariant()))
                return st.name;

            if (fallback == null)
                fallback = st.name;
        }

        return fallback;
    }

    private string FindBestLoopClip(Animation anim)
    {
        string fallback = null;

        foreach (AnimationState st in anim)
        {
            string n = st.name.ToLowerInvariant();

            if (!string.IsNullOrEmpty(requiredKeyword) && !n.Contains(requiredKeyword.ToLowerInvariant()))
                continue;

            bool hasLoopKeyword =
                n.Contains(loopKeyword1.ToLowerInvariant()) ||
                n.Contains(loopKeyword2.ToLowerInvariant());

            if (hasLoopKeyword)
                return st.name;

            if (fallback == null && !n.Contains(openKeyword.ToLowerInvariant()))
                fallback = st.name;
        }

        return fallback;
    }
}