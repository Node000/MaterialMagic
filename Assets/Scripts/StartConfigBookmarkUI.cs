using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StartConfigBookmarkUI : MonoBehaviour
{
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image textureImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private RectTransform magicRoot;
    [SerializeField] private RectTransform materialRoot;
    [SerializeField] private MagicItemView magicViewPrefab;
    [SerializeField] private MaterialCardView materialCardPrefab;
    [SerializeField] private Button button;
    [Header("书签移动")]
    [SerializeField] private float enterDuration = 0.34f;
    [SerializeField] private float selectDuration = 0.34f;
    [SerializeField] private float exitDuration = 0.34f;
    [SerializeField] private Ease enterEase = Ease.OutCubic;
    [SerializeField] private Ease selectEase = Ease.OutCubic;
    [SerializeField] private Ease exitEase = Ease.OutCubic;

    private readonly List<MagicItemView> magicViews = new List<MagicItemView>();
    private readonly List<GameObject> materialItems = new List<GameObject>();
    private Action<PlayerStartConfigData> onClick;
    private Tween moveTween;
    private Tween scaleTween;

    public PlayerStartConfigData Config { get; private set; }
    public RectTransform RectTransform => rectTransform != null ? rectTransform : (RectTransform)transform;

    private void Awake()
    {
        if (rectTransform == null)
            rectTransform = (RectTransform)transform;
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();
        if (button == null)
            button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(HandleClick);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);
        KillTweens();
    }

    public void Bind(PlayerStartConfigData config, Action<PlayerStartConfigData> clickHandler)
    {
        Config = config;
        onClick = clickHandler;

        if (nameText != null)
            nameText.text = !string.IsNullOrEmpty(config.displayName) ? config.displayName : config.id;
        if (healthText != null)
            healthText.text = "生命值 " + config.maxHealth;

        Color color = Color.white;
        if (!string.IsNullOrEmpty(config.color))
            ColorUtility.TryParseHtmlString(config.color, out color);
        color.a = 1f;
        if (backgroundImage != null)
            backgroundImage.color = color;

        if (textureImage != null)
        {
            textureImage.sprite = string.IsNullOrEmpty(config.texturePath) ? null : Resources.Load<Sprite>(config.texturePath);
            textureImage.enabled = textureImage.sprite != null;
        }

        RebuildMagicViews(config.initialMagics);
        RebuildMaterialViews(config.initialMaterials);
    }

    public void Show(float initialX, float readyX, float delay)
    {
        RectTransform.anchoredPosition = new Vector2(initialX, RectTransform.anchoredPosition.y);
        MoveTo(readyX, enterDuration, enterEase, delay);
    }

    public void Hide(float initialX, float delay, Action<StartConfigBookmarkUI> onComplete)
    {
        moveTween?.Kill(false);
        scaleTween?.Kill(false);
        scaleTween = RectTransform.DOScale(Vector3.one, exitDuration * 0.7f).SetEase(exitEase).SetUpdate(true).SetTarget(this);
        moveTween = RectTransform.DOAnchorPosX(initialX, exitDuration).SetDelay(delay).SetEase(exitEase).SetUpdate(true).SetTarget(this)
            .OnComplete(() => onComplete?.Invoke(this));
    }

    public void SetSelected(bool selected, float readyX, float displayX)
    {
        MoveTo(selected ? displayX : readyX, selectDuration, selectEase, 0f);
        scaleTween?.Kill(false);
        scaleTween = RectTransform.DOScale(selected ? Vector3.one * 1.035f : Vector3.one, selectDuration).SetEase(selectEase).SetUpdate(true).SetTarget(this);
    }

    public void KillTweens()
    {
        moveTween?.Kill(false);
        scaleTween?.Kill(false);
    }

    private void MoveTo(float x, float duration, Ease ease, float delay)
    {
        moveTween?.Kill(false);
        moveTween = RectTransform.DOAnchorPosX(x, duration).SetDelay(delay).SetEase(ease).SetUpdate(true).SetTarget(this);
    }

    private void HandleClick()
    {
        onClick?.Invoke(Config);
    }

    private void RebuildMagicViews(PlayerStartMagicData[] magics)
    {
        for (int i = 0; i < magicViews.Count; i++)
        {
            if (magicViews[i] != null)
                Destroy(magicViews[i].gameObject);
        }
        magicViews.Clear();

        if (magicRoot == null || magicViewPrefab == null)
            return;

        for (int i = 0; i < 6; i++)
        {
            MagicItemView view = Instantiate(magicViewPrefab, magicRoot);
            RectTransform rect = view.transform as RectTransform;
            rect.localScale = Vector3.one * 0.68f;
            rect.sizeDelta = new Vector2(196f, 92f);
            rect.anchoredPosition = new Vector2((i % 3) * 142f, -(i / 3) * 72f);
            view.Bind(GetMagicForSlot(magics, i));
            magicViews.Add(view);
        }
    }

    private void RebuildMaterialViews(PlayerStartMaterialData[] materials)
    {
        for (int i = 0; i < materialItems.Count; i++)
        {
            if (materialItems[i] != null)
                Destroy(materialItems[i]);
        }
        materialItems.Clear();

        if (materialRoot == null || materialCardPrefab == null || materials == null)
            return;

        for (int i = 0; i < materials.Length; i++)
        {
            PlayerStartMaterialData data = materials[i];
            if (data == null)
                continue;

            GameObject item = new GameObject("MaterialItem", typeof(RectTransform));
            item.transform.SetParent(materialRoot, false);
            RectTransform itemRect = item.GetComponent<RectTransform>();
            itemRect.sizeDelta = new Vector2(118f, 72f);
            itemRect.anchoredPosition = new Vector2((i % 2) * 126f, -(i / 2) * 76f);

            MaterialCardView card = Instantiate(materialCardPrefab, itemRect);
            RectTransform cardRect = card.transform as RectTransform;
            cardRect.localScale = Vector3.one * 0.42f;
            cardRect.anchoredPosition = new Vector2(0f, 0f);
            card.Bind(new MaterialModel(data.material + "_preview", data.material));

            TMP_Text countText = CreateCountText(itemRect);
            countText.text = "×" + data.count;
            materialItems.Add(item);
        }
    }

    private static MagicModel GetMagicForSlot(PlayerStartMagicData[] magics, int slotIndex)
    {
        if (magics == null)
            return null;

        for (int i = 0; i < magics.Length; i++)
        {
            if (magics[i] != null && magics[i].slotIndex == slotIndex)
                return PlayerState.CreateMagicFromData(magics[i].magicId, slotIndex);
        }
        return null;
    }

    private static TMP_Text CreateCountText(RectTransform parent)
    {
        GameObject textObject = new GameObject("Count", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -6f);
        rect.sizeDelta = new Vector2(52f, 28f);

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.font = UIManager.GetDefaultTMPFont();
        text.fontSize = 22;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.MidlineRight;
        text.color = new Color(1f, 0.94f, 0.7f, 1f);
        text.raycastTarget = false;
        return text;
    }
}
