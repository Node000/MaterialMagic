using System;
using UnityEngine;

public class PlayerCastAnimatorUI : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string idleStateName = "Idle";
    [SerializeField] private string castStateName = "Cast";
    [SerializeField] private float castReleaseDelay = 0.5f;

    private int idleStateHash;
    private int castStateHash;
    private Action releaseHandler;
    private float resolvedCastReleaseDelay = -1f;

    public float CastReleaseDelay
    {
        get
        {
            if (resolvedCastReleaseDelay < 0f)
                resolvedCastReleaseDelay = ResolveCastReleaseDelay();
            return resolvedCastReleaseDelay;
        }
    }

    private void Awake()
    {
        Initialize();
    }

    public void Initialize()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        idleStateHash = Animator.StringToHash(idleStateName);
        castStateHash = Animator.StringToHash(castStateName);
        resolvedCastReleaseDelay = -1f;
        PlayIdle();
    }

    public void PlayCast()
    {
        if (animator == null)
            Initialize();
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        animator.Play(castStateHash, 0, 0f);
    }

    public void SetReleaseHandler(Action handler)
    {
        releaseHandler = handler;
    }

    public void OnCastReleaseFrame()
    {
        releaseHandler?.Invoke();
    }

    private float ResolveCastReleaseDelay()
    {
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            float releaseDelay = -1f;
            for (int i = 0; i < clips.Length; i++)
            {
                AnimationEvent[] events = clips[i].events;
                for (int j = 0; j < events.Length; j++)
                {
                    if (events[j].functionName == nameof(OnCastReleaseFrame) && (releaseDelay < 0f || events[j].time < releaseDelay))
                        releaseDelay = events[j].time;
                }
            }
            if (releaseDelay >= 0f)
                return releaseDelay;
        }

        return castReleaseDelay;
    }

    private void PlayIdle()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        animator.Play(idleStateHash, 0, 0f);
    }
}
