using System;
using UnityEngine;

[Serializable]
public struct UnifiedDetailLocalizedText
{
    public string key;
    [TextArea] public string fallback;

    public string Value => LocalizationSystem.GetText(key, fallback);

    public UnifiedDetailLocalizedText(string key, string fallback)
    {
        this.key = key;
        this.fallback = fallback;
    }
}

[CreateAssetMenu(fileName = "UnifiedDetailTextConfig", menuName = "Config/Unified Detail Text Config")]
public class UnifiedDetailTextConfig : ScriptableObject
{
    [Header("Section Titles")]
    [SerializeField] private UnifiedDetailLocalizedText modifierSectionTitle = new UnifiedDetailLocalizedText("ui.unified_detail.section.modifier", "强化效果");
    [SerializeField] private UnifiedDetailLocalizedText arrowEffectSectionTitle = new UnifiedDetailLocalizedText("ui.unified_detail.section.arrow_effect", "箭头效果");
    [SerializeField] private UnifiedDetailLocalizedText tagSectionTitle = new UnifiedDetailLocalizedText("ui.unified_detail.section.tag", "关键词");
    [SerializeField] private UnifiedDetailLocalizedText displayDirectionLabel = new UnifiedDetailLocalizedText("ui.unified_detail.material.display_direction", "显示方向");
    [SerializeField] private UnifiedDetailLocalizedText eventEffectPrefix = new UnifiedDetailLocalizedText("ui.unified_detail.event.effect_prefix", "效果：");

    [Header("Paragraph Formatting")]
    [SerializeField] private float inlineIconScale = 1.5f;
    [SerializeField] private float inlineIconVerticalOffsetEm = -0.08f;
    [SerializeField] private string paragraphSeparator = "\n\n";
    [SerializeField] private string lineBreak = "\n";
    [SerializeField] private string bulletPrefix = "• ";
    [SerializeField] private string titleValueSeparator = "：";
    [SerializeField] private bool tintSectionTitles = false;
    [SerializeField] private Color sectionTitleColor = new Color(1f, 0.91f, 0.62f, 1f);
    [SerializeField] private Color modifierTitleColor = new Color(1f, 0.62f, 0.22f, 1f);
    [SerializeField] private Color tagTitleColor = new Color(0.48f, 0.86f, 1f, 1f);
    [SerializeField] private Color arrowEffectTitleColor = new Color(0.68f, 1f, 0.42f, 1f);
    [SerializeField] private Color enhancementTextColor = new Color(0.48f, 0.86f, 1f, 1f);
    [SerializeField] private Color modifierTextColor = new Color(1f, 0.62f, 0.22f, 1f);

    [Header("Added Detail Frames")]
    [SerializeField] private Color keywordFrameColor = new Color(0.48f, 0.86f, 1f, 1f);
    [SerializeField] private Color enhancementFrameColor = new Color(0.68f, 1f, 0.42f, 1f);
    [SerializeField] private Color modifierFrameColor = new Color(1f, 0.62f, 0.22f, 1f);
    [SerializeField] private float addedDetailFirstYOffset = -292f;
    [SerializeField] private float addedDetailYSpacing = -118f;

    [Header("Buff / Fallback")]
    [SerializeField] private string buffTitleStackSeparator = "  ";
    [SerializeField] private UnifiedDetailLocalizedText fallbackMaterialTitle = new UnifiedDetailLocalizedText("ui.unified_detail.material.fallback_title", "箭头");
    [SerializeField] private UnifiedDetailLocalizedText fallbackBonusRewardNone = new UnifiedDetailLocalizedText("ui.unified_detail.reward.none", "无");
    [SerializeField] private UnifiedDetailLocalizedText rewardGoldText = new UnifiedDetailLocalizedText("ui.unified_detail.reward.gold", "获得金币 ×{0}");
    [SerializeField] private UnifiedDetailLocalizedText rewardHealText = new UnifiedDetailLocalizedText("ui.unified_detail.reward.heal", "恢复生命 ×{0}");

    [Header("Map Movement")]
    [SerializeField] private UnifiedDetailLocalizedText mapMoveTitle = new UnifiedDetailLocalizedText("ui.unified_detail.map_move.title", "地图移动");
    [SerializeField] private UnifiedDetailLocalizedText fireMapMoveBody = new UnifiedDetailLocalizedText("ui.unified_detail.map_move.fire.body", "向上移动一格。");
    [SerializeField] private UnifiedDetailLocalizedText windMapMoveBody = new UnifiedDetailLocalizedText("ui.unified_detail.map_move.wind.body", "向左移动一格。");
    [SerializeField] private UnifiedDetailLocalizedText waterMapMoveBody = new UnifiedDetailLocalizedText("ui.unified_detail.map_move.water.body", "向下移动一格。");
    [SerializeField] private UnifiedDetailLocalizedText earthMapMoveBody = new UnifiedDetailLocalizedText("ui.unified_detail.map_move.earth.body", "向右移动一格。");

    [Header("Material Base Effects")]
    [SerializeField] private UnifiedDetailLocalizedText fireSinglePlay = new UnifiedDetailLocalizedText("material.fire.single_play", "向上：造成<attack>3点伤害");
    [SerializeField] private UnifiedDetailLocalizedText waterSinglePlay = new UnifiedDetailLocalizedText("material.water.single_play", "向下：随机施加1层<buff_weak>或<buff_slow>");
    [SerializeField] private UnifiedDetailLocalizedText windSinglePlay = new UnifiedDetailLocalizedText("material.wind.single_play", "向左：下回合额外抽1张牌");
    [SerializeField] private UnifiedDetailLocalizedText earthSinglePlay = new UnifiedDetailLocalizedText("material.earth.single_play", "向右：获得<shield>3");
    [SerializeField] private UnifiedDetailLocalizedText wildSinglePlay = new UnifiedDetailLocalizedText("material.wild.single_play", "这是一张特殊箭头");
    [SerializeField] private UnifiedDetailLocalizedText noneSinglePlay = new UnifiedDetailLocalizedText("material.none.single_play", "无基础效果");

    public string ModifierSectionTitle => modifierSectionTitle.Value;
    public string ArrowEffectSectionTitle => arrowEffectSectionTitle.Value;
    public string TagSectionTitle => tagSectionTitle.Value;
    public string DisplayDirectionLabel => displayDirectionLabel.Value;
    public string EventEffectPrefix => eventEffectPrefix.Value;
    public float InlineIconScale => Mathf.Max(0.01f, inlineIconScale);
    public float InlineIconVerticalOffsetEm => inlineIconVerticalOffsetEm;
    public string ParagraphSeparator => paragraphSeparator;
    public string LineBreak => lineBreak;
    public string BulletPrefix => bulletPrefix;
    public string TitleValueSeparator => titleValueSeparator;
    public string BuffTitleStackSeparator => buffTitleStackSeparator;
    public string FallbackMaterialTitle => fallbackMaterialTitle.Value;
    public string FallbackBonusRewardNone => fallbackBonusRewardNone.Value;
    public string MapMoveTitle => mapMoveTitle.Value;
    public bool TintSectionTitles => tintSectionTitles;
    public Color EnhancementTextColor => enhancementTextColor;
    public Color ModifierTextColor => modifierTextColor;
    public Color KeywordTextColor => tagTitleColor;
    public Color KeywordFrameColor => keywordFrameColor;
    public Color EnhancementFrameColor => enhancementFrameColor;
    public Color ModifierFrameColor => modifierFrameColor;
    public float AddedDetailFirstYOffset => addedDetailFirstYOffset;
    public float AddedDetailYSpacing => addedDetailYSpacing;

    public string GetRewardGoldText(int amount)
    {
        return string.Format(rewardGoldText.Value, amount);
    }

    public string GetRewardHealText(int amount)
    {
        return string.Format(rewardHealText.Value, amount);
    }

    public string GetMapMoveBody(MaterialEnum material)
    {
        switch (material)
        {
            case MaterialEnum.Fire: return fireMapMoveBody.Value;
            case MaterialEnum.Wind: return windMapMoveBody.Value;
            case MaterialEnum.Water: return waterMapMoveBody.Value;
            case MaterialEnum.Earth: return earthMapMoveBody.Value;
            default: return string.Empty;
        }
    }

    public string GetMaterialSinglePlay(MaterialEnum material)
    {
        switch (material)
        {
            case MaterialEnum.Fire: return fireSinglePlay.Value;
            case MaterialEnum.Water: return waterSinglePlay.Value;
            case MaterialEnum.Wind: return windSinglePlay.Value;
            case MaterialEnum.Earth: return earthSinglePlay.Value;
            case MaterialEnum.Wild: return wildSinglePlay.Value;
            default: return noneSinglePlay.Value;
        }
    }

    public string FormatSectionLabel(string label)
    {
        return FormatSectionLabel(label, sectionTitleColor);
    }

    public string FormatModifierLabel(string label)
    {
        return FormatSectionLabel(label, modifierTitleColor);
    }

    public string FormatTagLabel(string label)
    {
        return FormatSectionLabel(label, tagTitleColor);
    }

    public string FormatArrowEffectLabel(string label)
    {
        return FormatSectionLabel(label, arrowEffectTitleColor);
    }

    private string FormatSectionLabel(string label, Color color)
    {
        if (string.IsNullOrEmpty(label))
            return string.Empty;
        string value = label + titleValueSeparator;
        if (!tintSectionTitles)
            return value;
        return "<color=#" + ColorUtility.ToHtmlStringRGBA(color) + ">" + value + "</color>";
    }
}
