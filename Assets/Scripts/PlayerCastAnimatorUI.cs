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

    public float CastReleaseDelay => castReleaseDelay;

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

    private void PlayIdle()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        animator.Play(idleStateHash, 0, 0f);
    }
}
