using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class PlayerFeedbackUI : MonoBehaviour
{
    [SerializeField] private RectTransform playerVirtualTarget;
    [SerializeField] private RectTransform damageShakeTarget;
    [SerializeField] private Image playerVignetteFeedback;

    [SerializeField] private float damageShakeDuration = 0.28f;
    [SerializeField] private Vector2 damageShakeStrength = new Vector2(10f, 4f);
    [SerializeField] private int damageShakeVibrato = 14;
    [SerializeField] private float fullHealthInnerRadius = 0.42f;
    [SerializeField] private float lowHealthInnerRadius = 0.2f;
    [Header("滤镜动画参数")]
    [SerializeField] private float vignetteFadeInDuration = 0.12f;
    [SerializeField] private float vignetteFadeOutDuration = 0.42f;
    [SerializeField] private Ease vignetteFadeOutEase = Ease.OutQuad;
    [Header("滤镜材质参数")]
    [SerializeField] private float defaultInnerRadius = 0.27f;
    [SerializeField] private float outerRadius = 0.54f;
    [SerializeField] private float aspectScale = 0.56f;
    [SerializeField] private float edgePower = 1.45f;

    private Material playerVignetteMaterial;
    private Color currentPlayerVignetteColor;
    private Tween playerFeedbackTween;

    public RectTransform PlayerVirtualTarget => playerVirtualTarget;

    public void Initialize(HandSystemUI owner, Transform root)
    {
        CacheReferences(root);
        CreateVignetteFeedback();
    }

    public void PlayCornerFeedback(Color color)
    {
        if (playerVignetteFeedback == null || playerVignetteMaterial == null)
            CreateVignetteFeedback();
        if (playerVignetteMaterial == null)
            return;

        playerFeedbackTween?.Kill(false);
        playerVignetteFeedback.gameObject.SetActive(true);
        playerVignetteFeedback.transform.SetAsLastSibling();
        currentPlayerVignetteColor = color;
        color.a = 0f;
        playerVignetteMaterial.SetColor("_VignetteColor", color);
        playerFeedbackTween = DOVirtual.Float(0f, 1f, vignetteFadeInDuration, value =>
        {
            Color vignetteColor = currentPlayerVignetteColor;
            vignetteColor.a *= value;
            playerVignetteMaterial.SetColor("_VignetteColor", vignetteColor);
        }).SetTarget(this).OnComplete(() =>
        {
            playerFeedbackTween = DOVirtual.Float(1f, 0f, vignetteFadeOutDuration, value =>
            {
                Color vignetteColor = currentPlayerVignetteColor;
                vignetteColor.a *= value;
                playerVignetteMaterial.SetColor("_VignetteColor", vignetteColor);
            }).SetEase(vignetteFadeOutEase).SetTarget(this).OnComplete(() =>
            {
                playerVignetteMaterial.SetColor("_VignetteColor", Color.clear);
                if (playerVignetteFeedback != null)
                    playerVignetteFeedback.gameObject.SetActive(false);
            });
        });
    }

    public void PlayDamageFeedback(Color color, PlayerState playerState)
    {
        UpdateVignetteRange(playerState);
        PlayCornerFeedback(color);
        RectTransform shakeTarget = damageShakeTarget != null ? damageShakeTarget : playerVirtualTarget;
        if (shakeTarget == null)
            return;

        shakeTarget.DOKill(false);
        shakeTarget.DOShakeAnchorPos(damageShakeDuration, damageShakeStrength, damageShakeVibrato, 90f, false, true).SetTarget(this);
    }

    public void UpdateVignetteRange(PlayerState playerState)
    {
        if (playerVignetteMaterial == null)
            CreateVignetteFeedback();
        if (playerVignetteMaterial == null || playerState == null || playerState.MaxHealth <= 0)
            return;

        float health01 = Mathf.Clamp01(playerState.CurrentHealth / (float)playerState.MaxHealth);
        playerVignetteMaterial.SetFloat("_InnerRadius", Mathf.Lerp(lowHealthInnerRadius, fullHealthInnerRadius, health01));
    }

    private void CacheReferences(Transform root)
    {
        if (root == null)
            return;
        if (playerVirtualTarget == null)
            playerVirtualTarget = UIManager.FindChildRect(root, "PlayerVirtualTarget");
        if (damageShakeTarget == null)
            damageShakeTarget = UIManager.FindChildRect(root, "PlayerArea");
        if (playerVignetteFeedback == null)
            playerVignetteFeedback = UIManager.FindChildComponent<Image>(root, "PlayerVignetteFeedback");
    }

    private void CreateVignetteFeedback()
    {
        Shader shader = Shader.Find("UI/CornerVignetteFeedback");
        if (shader == null)
            return;

        if (playerVignetteMaterial == null)
        {
            playerVignetteMaterial = new Material(shader);
            playerVignetteMaterial.SetFloat("_InnerRadius", defaultInnerRadius);
            playerVignetteMaterial.SetFloat("_OuterRadius", outerRadius);
            playerVignetteMaterial.SetFloat("_AspectScale", aspectScale);
            playerVignetteMaterial.SetFloat("_EdgePower", edgePower);
            playerVignetteMaterial.SetColor("_VignetteColor", Color.clear);
        }

        if (playerVignetteFeedback == null)
            return;

        playerVignetteFeedback.gameObject.SetActive(true);
        playerVignetteFeedback.raycastTarget = false;
        playerVignetteFeedback.color = Color.white;
        playerVignetteFeedback.material = playerVignetteMaterial;
        playerVignetteFeedback.transform.SetAsLastSibling();
        playerVignetteFeedback.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        playerFeedbackTween?.Kill(false);
        if (playerVignetteMaterial != null)
            Destroy(playerVignetteMaterial);
    }
}
