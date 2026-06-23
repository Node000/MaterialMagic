using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class BuffPopupEffectController : MonoBehaviour
{
    private const string SettingsResourcePath = "GlobalConfig/BuffPopupEffectSettings";

    [SerializeField] private RectTransform effectRoot;
    [SerializeField] private Image iconImage;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private BuffPopupEffectSettings settings;

    private readonly Queue<BuffEnum> pendingBuffs = new Queue<BuffEnum>();
    private Sequence playbackSequence;
    private bool isPlaying;

    public float FadeInDuration => ActiveSettings != null ? ActiveSettings.FadeInDuration : 0.12f;
    public float FadeOutDuration => ActiveSettings != null ? ActiveSettings.FadeOutDuration : 0.18f;
    public float DisplayDuration => ActiveSettings != null ? ActiveSettings.DisplayDuration : 0.18f;
    public float Interval => ActiveSettings != null ? ActiveSettings.Interval : 0.08f;
    public float StartScale => ActiveSettings != null ? ActiveSettings.StartScale : 1f;
    public float EndScale => ActiveSettings != null ? ActiveSettings.EndScale : 1.5f;
    public float PeakAlpha => ActiveSettings != null ? ActiveSettings.PeakAlpha : 0.5f;

    private BuffPopupEffectSettings ActiveSettings => settings != null ? settings : (settings = Resources.Load<BuffPopupEffectSettings>(SettingsResourcePath));

    private void Awake()
    {
        CacheReferences();
        ResetVisual();
    }

    private void OnValidate()
    {
        CacheReferences();
        if (!Application.isPlaying)
            ResetVisual();
    }

    public void Play(BuffEnum buffType)
    {
        if (buffType == BuffEnum.None)
            return;

        pendingBuffs.Enqueue(buffType);
        if (!isPlaying)
            PlayNext();
    }

    public void ClearQueue(bool resetVisual = true)
    {
        pendingBuffs.Clear();
        isPlaying = false;
        if (playbackSequence != null)
        {
            playbackSequence.Kill(false);
            playbackSequence = null;
        }

        if (resetVisual)
            ResetVisual();
    }

    public void CacheReferences()
    {
        if (effectRoot == null)
            effectRoot = transform as RectTransform;
        if (iconImage == null)
            iconImage = GetComponentInChildren<Image>(true);
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (iconImage != null)
            iconImage.raycastTarget = false;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    private void PlayNext()
    {
        if (pendingBuffs.Count == 0)
        {
            isPlaying = false;
            playbackSequence = null;
            ResetVisual();
            return;
        }

        CacheReferences();
        BuffEnum buffType = pendingBuffs.Dequeue();
        BuffDisplayData data = BuffDisplayDatabase.Get(buffType);
        if (iconImage == null)
        {
            isPlaying = false;
            return;
        }

        iconImage.sprite = data.Icon;
        iconImage.color = data.Icon != null ? Color.white : data.FallbackColor;
        transform.localScale = Vector3.one * StartScale;
        canvasGroup.alpha = 0f;
        isPlaying = true;

        playbackSequence = DOTween.Sequence().SetTarget(this);
        playbackSequence.Append(canvasGroup.DOFade(PeakAlpha, FadeInDuration).SetEase(Ease.OutQuad));
        playbackSequence.Join(transform.DOScale(EndScale, FadeInDuration).SetEase(Ease.OutBack));
        playbackSequence.AppendInterval(DisplayDuration);
        playbackSequence.Append(canvasGroup.DOFade(0f, FadeOutDuration).SetEase(Ease.InQuad));
        playbackSequence.AppendInterval(Interval);
        playbackSequence.OnComplete(() =>
        {
            playbackSequence = null;
            PlayNext();
        });
    }

    private void ResetVisual()
    {
        CacheReferences();
        transform.localScale = Vector3.one * StartScale;
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    private void OnDisable()
    {
        ClearQueue();
    }
}
