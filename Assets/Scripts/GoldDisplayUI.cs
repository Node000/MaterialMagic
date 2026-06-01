using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GoldDisplayUI : MonoBehaviour
{
    [SerializeField] private TMP_Text amountText;
    [SerializeField] private RectTransform coinTarget;
    [SerializeField] private float coinTravelDuration = 0.56f;
    [SerializeField] private float coinStagger = 0.045f;
    [SerializeField] private float coinArcHeight = 130f;
    [SerializeField] private Vector2 coinSize = new Vector2(28f, 28f);

    private RectTransform rectTransform;
    private Canvas rootCanvas;

    public RectTransform TargetRect => coinTarget != null ? coinTarget : rectTransform;

    public void Initialize()
    {
        CacheReferences();
    }

    public void SetGold(int gold, bool instant)
    {
        CacheReferences();
        if (amountText != null)
            amountText.text = gold.ToString();
        if (!instant)
            PlayArrivalPunch();
    }

    public IEnumerator PlayGain(int amount, RectTransform sourceRect, Action onCoinArrived, Func<int> getCurrentGold)
    {
        if (amount <= 0)
            yield break;

        CacheReferences();
        RectTransform animationRoot = GetAnimationRoot();
        RectTransform targetRect = TargetRect;
        if (animationRoot == null || sourceRect == null || targetRect == null)
        {
            for (int i = 0; i < amount; i++)
                onCoinArrived?.Invoke();
            if (getCurrentGold != null)
                SetGold(getCurrentGold(), false);
            yield break;
        }

        Vector2 targetPosition = GetRectCenterLocalPoint(animationRoot, targetRect);
        Sequence allCoins = DOTween.Sequence().SetTarget(this);
        for (int i = 0; i < amount; i++)
        {
            RectTransform coin = CreateCoinParticle(animationRoot);
            Vector2 startPosition = GetRectCenterLocalPoint(animationRoot, sourceRect) + GetStartOffset(i);
            Vector2 controlPosition = GetControlPoint(startPosition, targetPosition, i);
            coin.anchoredPosition = startPosition;
            coin.localScale = Vector3.zero;

            Sequence coinSequence = DOTween.Sequence().SetTarget(this);
            coinSequence.Append(coin.DOScale(Vector3.one, 0.08f).SetEase(Ease.OutBack));
            coinSequence.Join(DOTween.To(() => 0f, value => coin.anchoredPosition = EvaluateQuadratic(startPosition, controlPosition, targetPosition, value), 1f, coinTravelDuration).SetEase(Ease.OutCubic));
            coinSequence.Join(coin.DORotate(new Vector3(0f, 0f, 360f), coinTravelDuration, RotateMode.FastBeyond360).SetEase(Ease.OutCubic));
            coinSequence.Insert(coinTravelDuration * 0.72f, coin.DOScale(Vector3.one * 0.72f, coinTravelDuration * 0.28f).SetEase(Ease.InQuad));
            coinSequence.OnComplete(() =>
            {
                onCoinArrived?.Invoke();
                if (getCurrentGold != null)
                    SetGold(getCurrentGold(), false);
                if (coin != null)
                    Destroy(coin.gameObject);
            });
            allCoins.Insert(i * coinStagger, coinSequence);
        }

        yield return allCoins.WaitForCompletion();
    }

    private void OnDisable()
    {
        DOTween.Kill(this);
    }

    private void CacheReferences()
    {
        if (rectTransform == null)
            rectTransform = transform as RectTransform;
        if (amountText == null)
            amountText = UIManager.FindChildComponent<TMP_Text>(transform, "AmountText");
        if (coinTarget == null)
            coinTarget = UIManager.FindChildRect(transform, "CoinIcon");
        if (rootCanvas == null)
            rootCanvas = GetComponentInParent<Canvas>();
    }

    private RectTransform GetAnimationRoot()
    {
        CacheReferences();
        Canvas canvas = rootCanvas != null ? rootCanvas.rootCanvas : null;
        return canvas != null ? canvas.transform as RectTransform : null;
    }

    private RectTransform CreateCoinParticle(RectTransform parent)
    {
        TMP_Text coinText = new GameObject("GoldParticle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        coinText.transform.SetParent(parent, false);
        coinText.text = "●";
        coinText.font = UIManager.GetDefaultTMPFont();
        coinText.fontSize = 30;
        coinText.fontStyle = FontStyles.Bold;
        coinText.alignment = TextAlignmentOptions.Center;
        coinText.color = new Color(1f, 0.86f, 0.18f, 1f);
        coinText.raycastTarget = false;
        RectTransform coinRect = coinText.rectTransform;
        coinRect.anchorMin = new Vector2(0.5f, 0.5f);
        coinRect.anchorMax = new Vector2(0.5f, 0.5f);
        coinRect.pivot = new Vector2(0.5f, 0.5f);
        coinRect.sizeDelta = coinSize;
        coinRect.SetAsLastSibling();
        return coinRect;
    }

    private Vector2 GetRectCenterLocalPoint(RectTransform root, RectTransform rect)
    {
        Canvas canvas = rootCanvas != null ? rootCanvas.rootCanvas : null;
        Camera camera = canvas != null ? canvas.worldCamera : null;
        Vector3 worldCenter = rect.TransformPoint(rect.rect.center);
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(camera, worldCenter);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPoint, camera, out Vector2 localPoint);
        return localPoint;
    }

    private Vector2 GetStartOffset(int index)
    {
        float angle = index * 2.39996f;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 8f;
    }

    private Vector2 GetControlPoint(Vector2 start, Vector2 end, int index)
    {
        float side = index % 2 == 0 ? -1f : 1f;
        float sideOffset = side * (30f + index % 3 * 12f);
        return (start + end) * 0.5f + new Vector2(sideOffset, coinArcHeight);
    }

    private static Vector2 EvaluateQuadratic(Vector2 start, Vector2 control, Vector2 end, float t)
    {
        float inverse = 1f - t;
        return inverse * inverse * start + 2f * inverse * t * control + t * t * end;
    }

    private void PlayArrivalPunch()
    {
        if (rectTransform == null)
            return;

        rectTransform.DOKill(false);
        rectTransform.localScale = Vector3.one;
        rectTransform.DOPunchScale(Vector3.one * 0.12f, 0.14f, 5, 0.65f).SetTarget(this);
    }
}
