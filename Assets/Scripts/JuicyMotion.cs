using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public class JuicyMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("触发")]
    [SerializeField] private bool triggerOnHover = true;
    [SerializeField] private bool triggerOnClick = false;
    [SerializeField] private bool loopOnEnable = false;
    [SerializeField] private bool loopTilt = false;
    [SerializeField] private GameObject hoverEventTarget;

    [Header("效果")]
    [SerializeField] private float scaleAmount = 0.15f;
    [SerializeField] private float shakeAmount = 8f;
    [SerializeField] private float hoverTiltAngle = 5f;
    [SerializeField] private float hoverTiltDuration = 0.16f;
    [SerializeField, Range(0f, 1f)] private float elasticity = 0.6f;

    [Header("时间")]
    [SerializeField] private float motionDuration = 0.25f;

    private Vector3 originalScale;
    private Vector3 originalLocalEulerAngles;
    private Sequence motionSequence;
    private Tween hoverTween;
    private JuicyMotionHoverTargetRelay hoverTargetRelay;

    private void Awake()
    {
        CaptureCurrentTransformAsBase();
        BindHoverEventTarget();
    }

    private void OnEnable()
    {
        CaptureCurrentTransformAsBase();
        BindHoverEventTarget();
        if (loopOnEnable)
            PlayLoop();
    }

    private void OnDisable()
    {
        UnbindHoverEventTarget();
        StopMotion();
        StopHoverMotion();
        transform.localScale = originalScale;
        transform.localEulerAngles = originalLocalEulerAngles;
    }

    private void OnDestroy()
    {
        UnbindHoverEventTarget();
        StopMotion();
        StopHoverMotion();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!triggerOnHover || hoverEventTarget != null)
            return;

        PlayHoverMotion(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!triggerOnHover || hoverEventTarget != null)
            return;

        PlayHoverMotion(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!triggerOnClick)
            return;

        Play();
    }

    public void SetHoverTiltAngle(float angle)
    {
        hoverTiltAngle = angle;
    }

    public void SetHoverEventTarget(GameObject target)
    {
        if (hoverEventTarget == target)
            return;

        UnbindHoverEventTarget();
        hoverEventTarget = target;
        BindHoverEventTarget();
    }

    public void SetHoverEffectEnabled(bool enabled)
    {
        if (triggerOnHover == enabled)
            return;

        triggerOnHover = enabled;
        if (enabled)
        {
            BindHoverEventTarget();
            return;
        }

        UnbindHoverEventTarget();
        StopHoverMotion();
        transform.localScale = originalScale;
        transform.localEulerAngles = originalLocalEulerAngles;
    }

    public void SetBaseScale(Vector3 scale, bool applyImmediately)
    {
        originalScale = scale;
        originalLocalEulerAngles = transform.localEulerAngles;
        if (applyImmediately)
            transform.localScale = originalScale;
    }

    public void CaptureCurrentTransformAsBase(bool applyImmediately = false)
    {
        originalScale = transform.localScale;
        originalLocalEulerAngles = transform.localEulerAngles;
        if (applyImmediately)
        {
            transform.localScale = originalScale;
            transform.localEulerAngles = originalLocalEulerAngles;
        }
    }

    public void Play()
    {
        StopMotion();

        transform.localScale = originalScale;

        motionSequence = DOTween.Sequence();
        motionSequence.Join(transform.DOPunchScale(originalScale * scaleAmount, motionDuration, 8, elasticity));

        if (shakeAmount > 0f)
            motionSequence.Join(transform.DOShakePosition(motionDuration, shakeAmount, 12, 90f, false, true));

        motionSequence.OnComplete(() =>
        {
            transform.localScale = originalScale;
            motionSequence = null;
            if (loopOnEnable && isActiveAndEnabled)
                PlayLoop();
        });
    }

    private void PlayLoop()
    {
        StopMotion();
        transform.localScale = originalScale;
        transform.localEulerAngles = originalLocalEulerAngles;
        motionSequence = DOTween.Sequence();
        if (loopTilt && hoverTiltAngle > 0f)
        {
            motionSequence.Append(transform.DOLocalRotate(originalLocalEulerAngles + new Vector3(0f, 0f, -hoverTiltAngle), motionDuration * 0.5f).SetEase(Ease.InOutSine));
            motionSequence.Append(transform.DOLocalRotate(originalLocalEulerAngles + new Vector3(0f, 0f, hoverTiltAngle), motionDuration).SetEase(Ease.InOutSine));
            motionSequence.Append(transform.DOLocalRotate(originalLocalEulerAngles, motionDuration * 0.5f).SetEase(Ease.InOutSine));
        }
        else if (shakeAmount > 0f)
        {
            motionSequence.Append(transform.DOShakePosition(motionDuration, shakeAmount, 8, 90f, false, true));
        }
        motionSequence.AppendInterval(0.12f);
        motionSequence.SetLoops(-1, LoopType.Restart);
    }

    private void StopMotion()
    {
        if (motionSequence == null)
            return;

        motionSequence.Kill(false);
        motionSequence = null;
    }

    internal void SetHoverFromExternalTarget(bool hovering)
    {
        if (!triggerOnHover || hoverEventTarget == null || !isActiveAndEnabled)
            return;

        PlayHoverMotion(hovering);
    }

    private void BindHoverEventTarget()
    {
        if (!triggerOnHover || hoverEventTarget == null)
            return;

        hoverTargetRelay = hoverEventTarget.GetComponent<JuicyMotionHoverTargetRelay>();
        if (hoverTargetRelay == null)
            hoverTargetRelay = hoverEventTarget.AddComponent<JuicyMotionHoverTargetRelay>();
        hoverTargetRelay.Register(this);
    }

    private void UnbindHoverEventTarget()
    {
        if (hoverTargetRelay == null)
            return;

        hoverTargetRelay.Unregister(this);
        hoverTargetRelay = null;
    }

    private void PlayHoverMotion(bool hovering)
    {
        StopHoverMotion();

        Vector3 targetRotation = originalLocalEulerAngles;
        Vector3 targetScale = originalScale;
        if (hovering)
        {
            if (hoverTiltAngle > 0f)
                targetRotation.z += hoverTiltAngle;
            if (scaleAmount > 0f)
                targetScale = originalScale * (1f + scaleAmount);
        }

        Sequence sequence = DOTween.Sequence();
        if (hoverTiltAngle > 0f)
            sequence.Join(transform.DOLocalRotate(targetRotation, hoverTiltDuration).SetEase(Ease.OutBack));
        sequence.Join(transform.DOScale(targetScale, hoverTiltDuration).SetEase(Ease.OutBack));
        hoverTween = sequence;
    }

    private void StopHoverMotion()
    {
        if (hoverTween == null)
            return;

        hoverTween.Kill(false);
        hoverTween = null;
    }
}

[DisallowMultipleComponent]
public class JuicyMotionHoverTargetRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private readonly List<JuicyMotion> motions = new List<JuicyMotion>();

    public void Register(JuicyMotion motion)
    {
        if (motion != null && !motions.Contains(motion))
            motions.Add(motion);
    }

    public void Unregister(JuicyMotion motion)
    {
        motions.Remove(motion);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetHovering(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetHovering(false);
    }

    private void SetHovering(bool hovering)
    {
        for (int i = motions.Count - 1; i >= 0; i--)
        {
            JuicyMotion motion = motions[i];
            if (motion == null)
            {
                motions.RemoveAt(i);
                continue;
            }

            motion.SetHoverFromExternalTarget(hovering);
        }
    }
}
