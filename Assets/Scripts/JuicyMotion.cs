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

    [Header("效果")]
    [SerializeField] private float scaleAmount = 0.15f;
    [SerializeField] private float shakeAmount = 8f;
    [SerializeField] private float hoverTiltAngle = 5f;
    [SerializeField] private float hoverTiltDuration = 0.16f;
    [SerializeField, Range(0f, 1f)] private float elasticity = 0.6f;

    [Header("时间")]
    [SerializeField] private float motionDuration = 0.25f;

    private Vector3 originalScale;
    private Vector3 originalLocalPosition;
    private Vector3 originalLocalEulerAngles;
    private Sequence motionSequence;
    private Tween hoverTween;

    private void Awake()
    {
        originalScale = transform.localScale;
        originalLocalPosition = transform.localPosition;
        originalLocalEulerAngles = transform.localEulerAngles;
    }

    private void OnEnable()
    {
        if (loopOnEnable)
            PlayLoop();
    }

    private void OnDisable()
    {
        StopMotion();
        StopHoverMotion();
        transform.localScale = originalScale;
        transform.localPosition = originalLocalPosition;
        transform.localEulerAngles = originalLocalEulerAngles;
    }

    private void OnDestroy()
    {
        StopMotion();
        StopHoverMotion();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!triggerOnHover)
            return;

        PlayHoverMotion(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!triggerOnHover)
            return;

        PlayHoverMotion(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!triggerOnClick)
            return;

        Play();
    }

    public void Play()
    {
        StopMotion();

        transform.localScale = originalScale;
        transform.localPosition = originalLocalPosition;

        motionSequence = DOTween.Sequence();
        motionSequence.Join(transform.DOPunchScale(originalScale * scaleAmount, motionDuration, 8, elasticity));

        if (shakeAmount > 0f)
            motionSequence.Join(transform.DOShakePosition(motionDuration, shakeAmount, 12, 90f, false, true));

        motionSequence.OnComplete(() =>
        {
            transform.localScale = originalScale;
            transform.localPosition = originalLocalPosition;
            motionSequence = null;
            if (loopOnEnable && isActiveAndEnabled)
                PlayLoop();
        });
    }

    private void PlayLoop()
    {
        StopMotion();
        transform.localScale = originalScale;
        transform.localPosition = originalLocalPosition;
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

        hoverTween = DOTween.Sequence()
            .Join(transform.DOLocalRotate(targetRotation, hoverTiltDuration).SetEase(Ease.OutBack))
            .Join(transform.DOScale(targetScale, hoverTiltDuration).SetEase(Ease.OutBack));
    }

    private void StopHoverMotion()
    {
        if (hoverTween == null)
            return;

        hoverTween.Kill(false);
        hoverTween = null;
    }
}
