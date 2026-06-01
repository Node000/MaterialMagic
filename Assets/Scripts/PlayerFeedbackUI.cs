using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class PlayerFeedbackUI : MonoBehaviour
{
    [SerializeField] private RectTransform playerVirtualTarget;
    [SerializeField] private RectTransform damageShakeTarget;
    [SerializeField] private Image playerVignetteFeedback;
    [SerializeField] private Image playerSpriteFeedbackImage;
    [SerializeField] private RectTransform playerFloatingTextTarget;

    [SerializeField] private float damageShakeDuration = 0.28f;
    [SerializeField] private Vector2 damageShakeStrength = new Vector2(10f, 4f);
    [SerializeField] private int damageShakeVibrato = 14;
    [Header("施法屏幕震动参数")]
    [SerializeField] private RectTransform castShakeTarget;
    [SerializeField] private int castShakeStartCastCount = 3;
    [SerializeField] private Vector2 castShakeStrengthPerCast = new Vector2(4f, 2f);
    [SerializeField] private Vector2 castShakeMaxStrength = new Vector2(28f, 14f);
    [SerializeField] private float castShakeDuration = 0.18f;
    [SerializeField] private int castShakeVibrato = 12;
    [SerializeField] private float fullHealthInnerRadius = 0.42f;
    [SerializeField] private float lowHealthInnerRadius = 0.2f;
    [Header("滤镜动画参数")]
    [SerializeField] private float vignetteFadeInDuration = 0.12f;
    [SerializeField] private float vignetteFadeOutDuration = 0.42f;
    [SerializeField] private Ease vignetteFadeOutEase = Ease.OutQuad;
    [Header("玩家贴图受击参数")]
    [SerializeField] private Color playerSpriteHitColor = new Color(1f, 0.08f, 0.04f, 1f);
    [SerializeField] private float playerSpriteHitFlashDuration = 0.08f;
    [SerializeField] private float playerSpriteHitRecoverDuration = 0.18f;
    [SerializeField] private Ease playerSpriteHitRecoverEase = Ease.OutQuad;
    [SerializeField] private float playerSpriteHitPunchScale = 0.08f;
    [SerializeField] private float playerSpriteHitPunchDuration = 0.2f;
    [SerializeField] private int playerSpriteHitPunchVibrato = 6;
    [SerializeField] private float playerSpriteHitPunchElasticity = 0.65f;
    [Header("滤镜材质参数")]
    [SerializeField] private float defaultInnerRadius = 0.27f;
    [SerializeField] private float outerRadius = 0.54f;
    [SerializeField] private float aspectScale = 0.56f;
    [SerializeField] private float edgePower = 1.45f;

    private Material playerVignetteMaterial;
    private Color currentPlayerVignetteColor;
    private Color playerSpriteBaseColor = Color.white;
    private Vector3 playerSpriteBaseScale = Vector3.one;
    private Transform activePlayerSpriteTransform;
    private Tween playerFeedbackTween;
    private Sequence playerSpriteHitTween;
    private Tween castShakeTween;
    private RectTransform activeCastShakeTarget;
    private Vector2 castShakeOrigin;

    public RectTransform PlayerVirtualTarget => playerVirtualTarget;
    public RectTransform PlayerFloatingTextTarget => playerFloatingTextTarget != null ? playerFloatingTextTarget : playerVirtualTarget;

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
        PlayPlayerSpriteHitFeedback();
        RectTransform shakeTarget = damageShakeTarget != null ? damageShakeTarget : playerVirtualTarget;
        if (shakeTarget == null)
            return;

        shakeTarget.DOKill(false);
        shakeTarget.DOShakeAnchorPos(damageShakeDuration, damageShakeStrength, damageShakeVibrato, 90f, false, true).SetTarget(this);
    }

    public void PlayCastScreenShake(int continuousCastCount)
    {
        int startCount = Mathf.Max(1, castShakeStartCastCount);
        if (continuousCastCount < startCount)
            return;

        RectTransform target = castShakeTarget != null ? castShakeTarget : transform as RectTransform;
        if (target == null)
            return;

        float multiplier = continuousCastCount - startCount + 1;
        Vector2 strength = new Vector2(
            Mathf.Min(Mathf.Max(0f, castShakeMaxStrength.x), Mathf.Max(0f, castShakeStrengthPerCast.x) * multiplier),
            Mathf.Min(Mathf.Max(0f, castShakeMaxStrength.y), Mathf.Max(0f, castShakeStrengthPerCast.y) * multiplier));
        if (strength == Vector2.zero || castShakeDuration <= 0f)
            return;

        StopCastScreenShake();
        activeCastShakeTarget = target;
        castShakeOrigin = target.anchoredPosition;
        castShakeTween = target.DOShakeAnchorPos(castShakeDuration, strength, castShakeVibrato, 90f, false, true).SetTarget(this).OnComplete(() =>
        {
            if (target != null)
                target.anchoredPosition = castShakeOrigin;
            castShakeTween = null;
            activeCastShakeTarget = null;
        });
    }

    private void StopCastScreenShake()
    {
        if (castShakeTween != null)
        {
            castShakeTween.Kill(false);
            castShakeTween = null;
        }
        if (activeCastShakeTarget != null)
            activeCastShakeTarget.anchoredPosition = castShakeOrigin;
        activeCastShakeTarget = null;
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
        if (playerFloatingTextTarget == null)
        {
            Transform floatingTextTarget = UIManager.FindChildRecursive(root, "PlayerFloatingTextTarget");
            playerFloatingTextTarget = floatingTextTarget as RectTransform;
        }
        if (damageShakeTarget == null)
            damageShakeTarget = UIManager.FindChildRect(root, "PlayerArea");
        if (castShakeTarget == null)
            castShakeTarget = root as RectTransform;
        if (playerVignetteFeedback == null)
            playerVignetteFeedback = UIManager.FindChildComponent<Image>(root, "PlayerVignetteFeedback");
        CachePlayerSpriteFeedbackTarget(root);
    }

    private void CachePlayerSpriteFeedbackTarget(Transform root)
    {
        if (playerSpriteFeedbackImage == null)
        {
            Transform playerSprite = UIManager.FindChildRecursive(root, "PlayerSprite");
            if (playerSprite != null)
                playerSpriteFeedbackImage = playerSprite.GetComponent<Image>();
        }

        if (playerSpriteFeedbackImage == null)
            return;

        activePlayerSpriteTransform = playerSpriteFeedbackImage.transform;
        playerSpriteBaseColor = playerSpriteFeedbackImage.color;
        playerSpriteBaseScale = activePlayerSpriteTransform.localScale;
    }

    private void PlayPlayerSpriteHitFeedback()
    {
        if (playerSpriteFeedbackImage == null || activePlayerSpriteTransform == null)
            CachePlayerSpriteFeedbackTarget(transform);
        if (playerSpriteFeedbackImage == null || activePlayerSpriteTransform == null)
            return;

        playerSpriteHitTween?.Kill(false);
        playerSpriteFeedbackImage.color = playerSpriteHitColor;
        activePlayerSpriteTransform.localScale = playerSpriteBaseScale;

        playerSpriteHitTween = DOTween.Sequence().SetTarget(this);
        playerSpriteHitTween.Append(activePlayerSpriteTransform.DOPunchScale(playerSpriteBaseScale * playerSpriteHitPunchScale, playerSpriteHitPunchDuration, playerSpriteHitPunchVibrato, playerSpriteHitPunchElasticity));
        playerSpriteHitTween.Insert(playerSpriteHitFlashDuration, playerSpriteFeedbackImage.DOColor(playerSpriteBaseColor, playerSpriteHitRecoverDuration).SetEase(playerSpriteHitRecoverEase));
        playerSpriteHitTween.OnComplete(() =>
        {
            if (playerSpriteFeedbackImage != null)
                playerSpriteFeedbackImage.color = playerSpriteBaseColor;
            if (activePlayerSpriteTransform != null)
                activePlayerSpriteTransform.localScale = playerSpriteBaseScale;
            playerSpriteHitTween = null;
        });
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
        StopCastScreenShake();
        playerSpriteHitTween?.Kill(false);
        playerFeedbackTween?.Kill(false);
        if (playerVignetteMaterial != null)
            Destroy(playerVignetteMaterial);
    }
}
