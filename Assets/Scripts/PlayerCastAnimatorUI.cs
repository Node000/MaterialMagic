using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PlayerCastAnimatorUI : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string idleStateName = "Idle";
    [SerializeField] private string castStateName = "Cast";
    [SerializeField] private string hitStateName = "Hit";
    [SerializeField] private string negativeStateName = "NegativeStatus";
    [SerializeField] private string magicSelectionStateName = "MagicSelection";
    [SerializeField] private string endTurnHoverStateName = "EndTurnHover";
    [SerializeField] private float castReleaseDelay = 0.17f;
    [SerializeField] private float castDuration = 0.5f;
    [SerializeField] private float hitDuration = 0.28f;
    [SerializeField] private float negativeStatusDuration = 1.2f;

    private const string BaseLayerPrefix = "Base Layer.";

    private int idleStateHash;
    private int castStateHash;
    private int hitStateHash;
    private int negativeStateHash;
    private int magicSelectionStateHash;
    private int endTurnHoverStateHash;
    private int currentLoopStateHash;
    private Action releaseHandler;
    private float resolvedCastReleaseDelay = -1f;
    private Coroutine temporaryStateRoutine;
    private bool hashesResolved;
    private bool magicSelectionActive;
    private int endTurnHoverCount;
    private int temporaryStateToken;

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

        ResolveStateHashes();
        resolvedCastReleaseDelay = -1f;
        ResumeLoopState(true);
    }

    public void PlayCast()
    {
        float duration = Mathf.Max(castDuration, CastReleaseDelay);
        PlayTemporaryState(castStateHash, duration);
    }

    public void PlayHit()
    {
        PlayTemporaryState(hitStateHash, hitDuration);
    }

    public void PlayNegativeStatus()
    {
        PlayTemporaryState(negativeStateHash, negativeStatusDuration);
    }

    public void SetMagicSelectionActive(bool active)
    {
        if (magicSelectionActive == active)
            return;

        magicSelectionActive = active;
        ResumeLoopState(false);
    }

    public void SetEndTurnHoverActive(bool active)
    {
        int nextCount = active ? endTurnHoverCount + 1 : Mathf.Max(0, endTurnHoverCount - 1);
        if (endTurnHoverCount == nextCount)
            return;

        endTurnHoverCount = nextCount;
        ResumeLoopState(false);
    }

    public void ClearEndTurnHover()
    {
        if (endTurnHoverCount == 0)
            return;

        endTurnHoverCount = 0;
        ResumeLoopState(false);
    }

    public void SetReleaseHandler(Action handler)
    {
        releaseHandler = handler;
    }

    public void OnCastReleaseFrame()
    {
        releaseHandler?.Invoke();
    }

    private void ResolveStateHashes()
    {
        idleStateHash = Animator.StringToHash(BaseLayerPrefix + idleStateName);
        castStateHash = Animator.StringToHash(BaseLayerPrefix + castStateName);
        hitStateHash = Animator.StringToHash(BaseLayerPrefix + hitStateName);
        negativeStateHash = Animator.StringToHash(BaseLayerPrefix + negativeStateName);
        magicSelectionStateHash = Animator.StringToHash(BaseLayerPrefix + magicSelectionStateName);
        endTurnHoverStateHash = Animator.StringToHash(BaseLayerPrefix + endTurnHoverStateName);
        hashesResolved = true;
    }

    private bool EnsureAnimator()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
        if (!hashesResolved)
            ResolveStateHashes();
        return animator != null && animator.runtimeAnimatorController != null;
    }

    private void PlayTemporaryState(int stateHash, float duration)
    {
        if (!EnsureAnimator() || !animator.HasState(0, stateHash))
            return;

        if (temporaryStateRoutine != null)
            StopCoroutine(temporaryStateRoutine);

        temporaryStateToken++;
        int token = temporaryStateToken;
        animator.Play(stateHash, 0, 0f);
        temporaryStateRoutine = StartCoroutine(ResumeLoopAfterTemporaryState(token, Mathf.Max(0f, duration)));
    }

    private IEnumerator ResumeLoopAfterTemporaryState(int token, float duration)
    {
        if (duration > 0f)
            yield return new WaitForSeconds(duration);

        if (token != temporaryStateToken)
            yield break;

        temporaryStateRoutine = null;
        ResumeLoopState(true);
    }

    private void ResumeLoopState(bool force)
    {
        if (temporaryStateRoutine != null)
            return;
        if (!EnsureAnimator())
            return;

        int targetHash = GetLoopStateHash();
        if (!force && currentLoopStateHash == targetHash)
            return;

        if (TryPlayState(targetHash))
        {
            currentLoopStateHash = targetHash;
            return;
        }

        if (targetHash != idleStateHash && TryPlayState(idleStateHash))
            currentLoopStateHash = idleStateHash;
    }

    private int GetLoopStateHash()
    {
        if (endTurnHoverCount > 0)
            return endTurnHoverStateHash;
        if (magicSelectionActive)
            return magicSelectionStateHash;
        return idleStateHash;
    }

    private bool TryPlayState(int stateHash)
    {
        if (!animator.HasState(0, stateHash))
            return false;

        animator.Play(stateHash, 0, 0f);
        return true;
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
}

[DisallowMultipleComponent]
public class PlayerAnimationHoverRelayUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private PlayerCastAnimatorUI target;
    private Selectable selectable;
    private bool hovering;

    private void Awake()
    {
        selectable = GetComponent<Selectable>();
    }

    public void Bind(PlayerCastAnimatorUI target)
    {
        if (hovering && this.target != null)
            SetTargetActive(false);

        this.target = target;
        if (selectable == null)
            selectable = GetComponent<Selectable>();

        if (hovering && this.target != null)
            SetTargetActive(true);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (selectable != null && !selectable.interactable)
            return;

        hovering = true;
        SetTargetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!hovering)
            return;

        hovering = false;
        SetTargetActive(false);
    }

    private void OnDisable()
    {
        if (!hovering)
            return;

        hovering = false;
        SetTargetActive(false);
    }

    private void SetTargetActive(bool active)
    {
        if (target == null)
            return;

        target.SetEndTurnHoverActive(active);
    }
}
