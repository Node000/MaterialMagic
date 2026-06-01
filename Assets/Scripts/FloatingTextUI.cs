using DG.Tweening;
using TMPro;
using UnityEngine;

public enum FloatingTextType
{
    Damage,
    Shield,
    Heal
}

public class FloatingTextUI : MonoBehaviour
{
    [SerializeField] private TMP_Text text;
    [SerializeField] private Color damageColor = new Color(1f, 0.25f, 0.18f, 1f);
    [SerializeField] private Color blockedColor = new Color(0.45f, 0.75f, 1f, 1f);
    [SerializeField] private Color shieldColor = new Color(0.45f, 0.75f, 1f, 1f);
    [SerializeField] private Color healColor = new Color(0.25f, 1f, 0.38f, 1f);

    private RectTransform rectTransform;
    private Sequence sequence;

    public TMP_Text Text => text != null ? text : text = GetComponent<TMP_Text>();
    private RectTransform RectTransform => rectTransform != null ? rectTransform : rectTransform = (RectTransform)transform;

    private void Awake()
    {
        rectTransform = (RectTransform)transform;
        if (text == null)
            text = GetComponent<TMP_Text>();
    }

    private void OnDestroy()
    {
        sequence?.Kill(false);
    }

    public void Play(string content, FloatingTextType type, bool blocked, float yOffset, float duration, Ease moveEase, Ease fadeEase)
    {
        TMP_Text targetText = Text;
        if (targetText == null)
            return;

        targetText.text = content;
        targetText.color = GetColor(type, blocked);
        RectTransform.localScale = Vector3.one;

        sequence?.Kill(false);
        sequence = DOTween.Sequence().SetTarget(this);
        sequence.Join(RectTransform.DOAnchorPos(RectTransform.anchoredPosition + new Vector2(0f, yOffset), duration).SetEase(moveEase));
        sequence.Join(DOTween.ToAlpha(() => targetText.color, c => targetText.color = c, 0f, duration).SetEase(fadeEase));
        sequence.OnComplete(() => Destroy(gameObject));
    }

    private Color GetColor(FloatingTextType type, bool blocked)
    {
        if (blocked)
            return blockedColor;

        switch (type)
        {
            case FloatingTextType.Shield:
                return shieldColor;
            case FloatingTextType.Heal:
                return healColor;
            default:
                return damageColor;
        }
    }
}
