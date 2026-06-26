using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum UnifiedDetailSourceType
{
    None = 0,
    Magic = 1,
    Material = 2,
    Buff = 3,
    EnemyIntent = 4,
    EventOption = 5,
    BonusReward = 6
}

public enum UnifiedDetailAddedDetailType
{
    Keyword = 0,
    Enhancement = 1,
    Modifier = 2
}

public struct UnifiedDetailAddedDetail
{
    public UnifiedDetailAddedDetailType Type;
    public string Title;
    public string Body;
}

public struct UnifiedDetailContent
{
    public UnifiedDetailSourceType SourceType;
    public Sprite Icon;
    public string Title;
    public string Body;
    public Color AccentColor;
    public List<UnifiedDetailAddedDetail> AddedDetails;
}

public static class UnifiedDetailContentBuilder
{
    private const string DefaultSectionColor = "#FFE99E";
    private static UnifiedDetailTextConfig cachedTextConfig;

    private static UnifiedDetailTextConfig TextConfig
    {
        get
        {
            if (cachedTextConfig == null)
                cachedTextConfig = Resources.Load<UnifiedDetailTextConfig>("Config/UnifiedDetailTextConfig");
            return cachedTextConfig;
        }
    }
    public static UnifiedDetailContent Build(MagicModel magic)
    {
        UnifiedDetailContent content = new UnifiedDetailContent
        {
            SourceType = UnifiedDetailSourceType.Magic,
            Title = magic != null ? magic.Name : string.Empty,
            Body = BuildMagicBody(magic),
            AccentColor = Color.white,
            Icon = LoadMagicIcon(magic),
            AddedDetails = BuildMagicAddedDetails(magic)
        };
        return content;
    }

    public static UnifiedDetailContent BuildEmptyMagicSlot()
    {
        UnifiedDetailContent content = new UnifiedDetailContent
        {
            SourceType = UnifiedDetailSourceType.Magic,
            Title = LocalizationSystem.GetText("ui.magic.empty_slot.title", "空道具栏"),
            Body = LocalizationSystem.GetText("ui.magic.empty_slot.body", "可以放入新的道具"),
            AccentColor = Color.white,
            Icon = null
        };
        return content;
    }

    public static UnifiedDetailContent Build(MaterialModel material)
    {
        MaterialEnum displayMaterial = material != null ? material.GetArrowDisplayMaterial() : MaterialEnum.None;
        UnifiedDetailContent content = new UnifiedDetailContent
        {
            SourceType = UnifiedDetailSourceType.Material,
            Title = GetMaterialTitle(material, displayMaterial),
            Body = BuildMaterialBody(material),
            AccentColor = GetMaterialAccentColor(material, displayMaterial),
            Icon = MaterialCardView.GetMaterialIcon(displayMaterial),
            AddedDetails = BuildMaterialAddedDetails(material)
        };
        return content;
    }

    public static UnifiedDetailContent BuildMapMove(MaterialEnum material)
    {
        StringBuilder body = new StringBuilder();
        AppendParagraph(body, TextConfig.GetMapMoveBody(material));
        AppendParagraph(body, BuildMaterialArrowEffectBody(new MaterialModel("map_move_" + material, material)));

        UnifiedDetailContent content = new UnifiedDetailContent
        {
            SourceType = UnifiedDetailSourceType.Material,
            Title = TextConfig.MapMoveTitle,
            Body = body.ToString(),
            AccentColor = MaterialCardView.GetMaterialColor(material),
            Icon = MaterialCardView.GetMaterialIcon(material)
        };
        return content;
    }

    public static UnifiedDetailContent Build(BuffModel buff)
    {
        BuffDisplayData display = buff != null ? BuffDisplayDatabase.Get(buff.buffType) : null;
        string stackText = buff != null ? buff.GetTooltipStackText() : string.Empty;
        UnifiedDetailContent content = new UnifiedDetailContent
        {
            SourceType = UnifiedDetailSourceType.Buff,
            Title = display != null ? display.Name + (!string.IsNullOrEmpty(stackText) ? TextConfig.BuffTitleStackSeparator + stackText : string.Empty) : string.Empty,
            Body = buff != null ? buff.GetDesc() : string.Empty,
            AccentColor = display != null && display.Icon == null ? display.FallbackColor : Color.white,
            Icon = display != null ? display.Icon : null
        };
        return content;
    }

    public static UnifiedDetailContent Build(EnemyModel enemy, EnemyIntentData intent, PlayerState playerState)
    {
        UnifiedDetailContent content = new UnifiedDetailContent
        {
            SourceType = UnifiedDetailSourceType.EnemyIntent,
            Title = enemy != null ? enemy.GetIntentTooltipTitle(intent) : string.Empty,
            Body = BuildEnemyIntentBody(enemy, intent, playerState),
            AccentColor = new Color(1f, 0.48f, 0.9f, 1f),
            Icon = LoadEnemyIntentIcon(intent),
            AddedDetails = BuildIntentBuffDetails(enemy != null ? enemy.GetIntentTooltipBuffs(intent, playerState) : null)
        };
        return content;
    }

    private static string BuildEnemyIntentBody(EnemyModel enemy, EnemyIntentData intent, PlayerState playerState)
    {
        if (enemy == null)
            return string.Empty;

        StringBuilder builder = new StringBuilder();
        AppendParagraph(builder, enemy.GetIntentTooltipDescription(intent, playerState));
        return builder.ToString();
    }

    private static string BuildIntentBuffBody(IReadOnlyList<BuffStackData> buffs)
    {
        if (buffs == null || buffs.Count == 0)
            return string.Empty;

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < buffs.Count; i++)
        {
            BuffStackData buffData = buffs[i];
            if (buffData == null || buffData.buffType == BuffEnum.None)
                continue;

            BuffModel buff = BuffModel.Create(buffData.buffType, buffData.stack);
            if (buff == null)
                continue;

            string name = LocalizationKeys.GetBuffName(buffData.buffType);
            if (string.IsNullOrEmpty(name))
                name = buffData.buffType.ToString();

            string description = buff.GetDesc();
            if (string.IsNullOrEmpty(description))
                continue;

            if (builder.Length > 0)
                builder.Append(TextConfig != null ? TextConfig.ParagraphSeparator : "\n\n");
            builder.Append(FormatTagTitle(name));
            builder.Append(TextConfig.LineBreak);
            builder.Append(description);
        }
        return builder.ToString();
    }

    public static UnifiedDetailContent Build(EventOptionData option)
    {
        UnifiedDetailContent content = new UnifiedDetailContent
        {
            SourceType = UnifiedDetailSourceType.EventOption,
            Title = option != null ? LocalizationSystem.GetText(option.titleKey, option.id) : string.Empty,
            Body = BuildEventOptionBody(option),
            AccentColor = new Color(0.94f, 0.76f, 0.34f, 1f),
            Icon = null,
            AddedDetails = BuildTagDetails(option != null ? option.tagIds : null)
        };
        return content;
    }

    public static UnifiedDetailContent Build(BonusRewardData rewardData)
    {
        UnifiedDetailContent content = new UnifiedDetailContent
        {
            SourceType = UnifiedDetailSourceType.BonusReward,
            Title = rewardData != null ? rewardData.rewardName : string.Empty,
            Body = BuildBonusRewardBody(rewardData),
            AccentColor = GetRewardAccentColor(rewardData),
            Icon = LoadRewardIcon(rewardData)
        };
        return content;
    }

    private static string BuildMagicBody(MagicModel magic)
    {
        if (magic == null)
            return string.Empty;

        StringBuilder builder = new StringBuilder();
        AppendParagraph(builder, magic.Description);
        return builder.ToString();
    }

    private static string BuildMaterialBody(MaterialModel material)
    {
        if (material == null)
            return string.Empty;

        StringBuilder builder = new StringBuilder();
        AppendParagraph(builder, BuildMaterialArrowEffectBody(material));

        MaterialEnum displayMaterial = material.GetArrowDisplayMaterial();
        if (displayMaterial != material.material && displayMaterial != MaterialEnum.None)
            AppendParagraph(builder, FormatInlineLabelValue(TextConfig.DisplayDirectionLabel, GetMaterialDirectionToken(displayMaterial)));

        return builder.ToString();
    }

    private static List<UnifiedDetailAddedDetail> BuildMagicAddedDetails(MagicModel magic)
    {
        List<UnifiedDetailAddedDetail> details = BuildTagDetails(magic != null && magic.Data != null ? magic.Data.tagIds : null);
        if (magic != null && magic.HasModifier && magic.PrimaryModifier != null)
            AddDetail(details, UnifiedDetailAddedDetailType.Enhancement, magic.PrimaryModifier.Name, magic.PrimaryModifier.Description);
        return details;
    }

    private static List<UnifiedDetailAddedDetail> BuildMaterialAddedDetails(MaterialModel material)
    {
        List<UnifiedDetailAddedDetail> details = new List<UnifiedDetailAddedDetail>();
        if (material == null)
            return details;

        for (int i = 0; i < material.enhancementIds.Count; i++)
            AddDetail(details, UnifiedDetailAddedDetailType.Enhancement, material.enhancementIds[i], string.Empty);

        if (material.modifiers != null)
        {
            for (int i = 0; i < material.modifiers.Count; i++)
            {
                MaterialModifierModel modifier = material.modifiers[i];
                if (modifier == null)
                    continue;

                AddDetail(details, UnifiedDetailAddedDetailType.Modifier, LocalizationKeys.GetModifierName(modifier), LocalizationKeys.GetModifierDescription(modifier));
            }
        }
        return details;
    }

    private static List<UnifiedDetailAddedDetail> BuildIntentBuffDetails(IReadOnlyList<BuffStackData> buffs)
    {
        List<UnifiedDetailAddedDetail> details = new List<UnifiedDetailAddedDetail>();
        if (buffs == null)
            return details;

        for (int i = 0; i < buffs.Count; i++)
        {
            BuffStackData buffData = buffs[i];
            if (buffData == null || buffData.buffType == BuffEnum.None)
                continue;

            BuffModel buff = BuffModel.Create(buffData.buffType, buffData.stack);
            if (buff == null)
                continue;

            string name = LocalizationKeys.GetBuffName(buffData.buffType);
            if (string.IsNullOrEmpty(name))
                name = buffData.buffType.ToString();
            AddDetail(details, UnifiedDetailAddedDetailType.Keyword, name, buff.GetDesc());
        }
        return details;
    }

    private static List<UnifiedDetailAddedDetail> BuildTagDetails(string[] tagIds)
    {
        List<UnifiedDetailAddedDetail> details = new List<UnifiedDetailAddedDetail>();
        if (tagIds == null)
            return details;

        for (int i = 0; i < tagIds.Length; i++)
        {
            string tagId = tagIds[i];
            if (string.IsNullOrEmpty(tagId) || !GameDataDatabase.TryGetTagData(tagId, out TagData tag))
                continue;

            AddDetail(details, UnifiedDetailAddedDetailType.Keyword, LocalizationKeys.GetTagName(tag), LocalizationKeys.GetTagDescription(tag));
        }
        return details;
    }

    private static void AddDetail(List<UnifiedDetailAddedDetail> details, UnifiedDetailAddedDetailType type, string title, string body)
    {
        if (details == null || string.IsNullOrEmpty(title))
            return;

        details.Add(new UnifiedDetailAddedDetail
        {
            Type = type,
            Title = title,
            Body = body
        });
    }

    private static string BuildMaterialArrowEffectBody(MaterialModel material)
    {
        if (material == null)
            return string.Empty;

        List<string> lines = new List<string>();
        lines.Add(GetMaterialSinglePlayEffectText(material.material));
        AppendMaterialReadRuleLines(lines, material);

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < lines.Count; i++)
        {
            if (i > 0)
                builder.Append(TextConfig.LineBreak);
            builder.Append(i == 0 ? StripMaterialEffectPrefix(lines[i]) : lines[i]);
        }
        return builder.ToString();
    }

    private static string BuildMaterialModifierBody(MaterialModel material)
    {
        if (material == null || material.modifiers == null || material.modifiers.Count == 0)
            return string.Empty;

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < material.modifiers.Count; i++)
        {
            MaterialModifierModel modifier = material.modifiers[i];
            if (modifier == null)
                continue;

            string name = LocalizationKeys.GetModifierName(modifier);
            string description = LocalizationKeys.GetModifierDescription(modifier);
            if (builder.Length > 0)
                builder.Append(TextConfig != null ? TextConfig.ParagraphSeparator : "\n\n");
            builder.Append(FormatModifierTitle(name));
            builder.Append(TextConfig.LineBreak);
            builder.Append(!string.IsNullOrEmpty(description) ? description : name);
        }
        return builder.ToString();
    }

    private static void AppendMaterialReadRuleLines(List<string> lines, MaterialModel material)
    {
        if (material == null)
            return;

        AppendCanActAsLines(lines, material);

        int additionalReadCount = material.GetAdditionalArrowReadCount();
        if (additionalReadCount > 0)
            lines.Add(LocalizationSystem.GetText("material.read_rule.additional_read", "额外读取{0}次").Replace("{0}", additionalReadCount.ToString()));
        if (material.ShouldPackFollowingArrows())
            lines.Add(LocalizationSystem.GetText("material.read_rule.pack_following", "读取时会打包后续箭头"));
        if (material.IsLinkedArrowContainer())
            lines.Add(LocalizationSystem.GetText("material.read_rule.linked_container", "这是一个连锁/容器箭头"));
        if (material.ShouldRemoveSourceAfterArrowRead())
            lines.Add(LocalizationSystem.GetText("material.read_rule.remove_self", "读取后会移除自身"));

        ArrowReadAfterReadAction action = material.GetArrowAfterReadAction();
        if (action == ArrowReadAfterReadAction.ReturnNextTurn)
            lines.Add(LocalizationSystem.GetText("material.read_rule.return_next_turn", "读取后下回合返回"));
        else if (action == ArrowReadAfterReadAction.Consume)
            lines.Add(LocalizationSystem.GetText("material.read_rule.consume", "读取后消耗"));
        else if (action == ArrowReadAfterReadAction.SplitIntoHalfArrowsToDiscard)
            lines.Add(LocalizationSystem.GetText("material.read_rule.split_half_discard", "读取后拆分为半箭头并进入弃牌堆"));

        IReadOnlyList<MaterialModel> linkedCards = material.GetArrowLinkedCards();
        if (linkedCards != null && linkedCards.Count > 0)
            lines.Add(LocalizationSystem.GetText("material.read_rule.linked_count", "内部包含{0}张连锁箭头").Replace("{0}", linkedCards.Count.ToString()));

    }

    private static void AppendCanActAsLines(List<string> lines, MaterialModel material)
    {
        bool canFire = material.CanActAs(MaterialEnum.Fire);
        bool canWater = material.CanActAs(MaterialEnum.Water);
        bool canWind = material.CanActAs(MaterialEnum.Wind);
        bool canEarth = material.CanActAs(MaterialEnum.Earth);

        MaterialEnum displayMaterial = material.GetArrowDisplayMaterial();
        List<string> actsAs = new List<string>(4);
        if (canFire && !IsDisplayedNativeDirection(material, displayMaterial, MaterialEnum.Fire))
            actsAs.Add(GetMaterialDirectionToken(MaterialEnum.Fire));
        if (canWater && !IsDisplayedNativeDirection(material, displayMaterial, MaterialEnum.Water))
            actsAs.Add(GetMaterialDirectionToken(MaterialEnum.Water));
        if (canWind && !IsDisplayedNativeDirection(material, displayMaterial, MaterialEnum.Wind))
            actsAs.Add(GetMaterialDirectionToken(MaterialEnum.Wind));
        if (canEarth && !IsDisplayedNativeDirection(material, displayMaterial, MaterialEnum.Earth))
            actsAs.Add(GetMaterialDirectionToken(MaterialEnum.Earth));
        if (actsAs.Count > 0)
            lines.Add(LocalizationSystem.GetText("material.read_rule.can_act_as", "可视为{0}").Replace("{0}", string.Join(" / ", actsAs)));
    }

    private static bool IsDisplayedNativeDirection(MaterialModel material, MaterialEnum displayMaterial, MaterialEnum direction)
    {
        return displayMaterial == direction && (material.material == direction || material.alternateMaterial == direction);
    }

    private static string BuildEventOptionBody(EventOptionData option)
    {
        if (option == null)
            return string.Empty;

        StringBuilder builder = new StringBuilder();
        AppendParagraph(builder, EventDetailTextUtility.GetOptionEffectText(option));
        return builder.ToString();
    }

    private static string BuildBonusRewardBody(BonusRewardData rewardData)
    {
        if (rewardData == null)
            return string.Empty;

        switch (rewardData.rewardType)
        {
            case BonusRewardType.Gold:
                return TextConfig.GetRewardGoldText(rewardData.amount);
            case BonusRewardType.Heal:
                return TextConfig.GetRewardHealText(rewardData.amount);
            default:
                return !string.IsNullOrEmpty(rewardData.rewardName) ? rewardData.rewardName : TextConfig.FallbackBonusRewardNone;
        }
    }

    private static string BuildTagBody(string[] tagIds)
    {
        if (tagIds == null || tagIds.Length == 0)
            return string.Empty;

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < tagIds.Length; i++)
        {
            string tagId = tagIds[i];
            if (string.IsNullOrEmpty(tagId) || !GameDataDatabase.TryGetTagData(tagId, out TagData tag))
                continue;

            string name = LocalizationKeys.GetTagName(tag);
            string description = LocalizationKeys.GetTagDescription(tag);
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(description))
                continue;

            if (builder.Length > 0)
                builder.Append(TextConfig != null ? TextConfig.ParagraphSeparator : "\n\n");
            builder.Append(FormatTagTitle(name));
            builder.Append(TextConfig.LineBreak);
            builder.Append(description);
        }
        return builder.ToString();
    }

    private static void AppendParagraph(StringBuilder builder, string paragraph)
    {
        if (string.IsNullOrEmpty(paragraph))
            return;

        if (builder.Length > 0)
            builder.Append(TextConfig.ParagraphSeparator);
        builder.Append(paragraph);
    }

    private static string FormatLabelValue(string label, string value, bool inline, string description)
    {
        if (inline)
            return FormatInlineLabelValue(label, value);

        if (string.IsNullOrEmpty(description))
            return FormatInlineLabelValue(label, value);

        StringBuilder builder = new StringBuilder();
        builder.Append(FormatInlineLabelValue(label, value));
        builder.Append(TextConfig != null ? TextConfig.LineBreak : "\n");
        builder.Append(description);
        return builder.ToString();
    }

    private static string FormatInlineLabelValue(string label, string value)
    {
        return FormatSectionTitle(label) + value;
    }

    private static string FormatSectionTitle(string title)
    {
        if (string.IsNullOrEmpty(title))
            return string.Empty;

        if (TextConfig != null)
            return TextConfig.FormatSectionLabel(title);

        return "<color=" + DefaultSectionColor + ">" + title + "：</color>";
    }

    private static string FormatModifierTitle(string title)
    {
        if (string.IsNullOrEmpty(title))
            return string.Empty;

        if (TextConfig != null)
            return TextConfig.FormatModifierLabel(title);

        return "<color=" + DefaultSectionColor + ">" + title + "：</color>";
    }

    private static string FormatTagTitle(string title)
    {
        if (string.IsNullOrEmpty(title))
            return string.Empty;

        if (TextConfig != null)
            return TextConfig.FormatTagLabel(title);

        return "<color=" + DefaultSectionColor + ">" + title + "：</color>";
    }

    private static string FormatArrowEffectTitle(string title)
    {
        if (string.IsNullOrEmpty(title))
            return string.Empty;

        if (TextConfig != null)
            return TextConfig.FormatArrowEffectLabel(title);

        return "<color=" + DefaultSectionColor + ">" + title + "：</color>";
    }

    private static string GetMaterialTitle(MaterialModel material, MaterialEnum displayMaterial)
    {
        string materialName = LocalizationKeys.GetMaterialName(displayMaterial);
        string fallbackTitle = TextConfig != null ? TextConfig.FallbackMaterialTitle : "箭头";
        return string.IsNullOrEmpty(materialName) ? fallbackTitle : materialName + fallbackTitle;
    }

    private static string GetMaterialSinglePlayEffectText(MaterialEnum material)
    {
        return TextConfig != null ? TextConfig.GetMaterialSinglePlay(material) : GetMaterialSinglePlayFallback(material);
    }

    private static string StripMaterialEffectPrefix(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        int separatorIndex = text.IndexOf('：');
        if (separatorIndex < 0)
            separatorIndex = text.IndexOf(':');
        if (separatorIndex < 0 || separatorIndex >= text.Length - 1)
            return text;

        string prefix = text.Substring(0, separatorIndex);
        if (prefix.StartsWith("向", StringComparison.Ordinal) || prefix.StartsWith("单独打出", StringComparison.Ordinal))
            return text.Substring(separatorIndex + 1).TrimStart();
        return text;
    }

    private static string GetMaterialSinglePlayFallback(MaterialEnum material)
    {
        switch (material)
        {
            case MaterialEnum.Fire:
                return "向上：造成<attack>3点伤害";
            case MaterialEnum.Water:
                return "向下：随机施加1层<buff_weak>或<buff_slow>";
            case MaterialEnum.Wind:
                return "向左：下回合额外抽1张牌";
            case MaterialEnum.Earth:
                return "向右：获得<shield>3";
            case MaterialEnum.Wild:
                return "这是一张特殊箭头";
            default:
                return "无基础效果";
        }
    }

    private static string GetMaterialDirectionToken(MaterialEnum material)
    {
        switch (material)
        {
            case MaterialEnum.Fire: return "<fire>上";
            case MaterialEnum.Water: return "<water>下";
            case MaterialEnum.Wind: return "<wind>左";
            case MaterialEnum.Earth: return "<earth>右";
            case MaterialEnum.Wild: return "万能";
            default: return "无";
        }
    }

    private static Color GetMaterialAccentColor(MaterialModel material, MaterialEnum displayMaterial)
    {
        if (material != null && material.modifiers != null)
        {
            for (int i = 0; i < material.modifiers.Count; i++)
            {
                MaterialModifierModel modifier = material.modifiers[i];
                if (modifier != null && MaterialModifierDisplayDatabase.TryGetLineColor(modifier, out Color color))
                    return color;
            }
        }
        return MaterialCardView.GetMaterialColor(displayMaterial);
    }

    private static Sprite LoadMagicIcon(MagicModel magic)
    {
        if (magic == null || magic.Data == null || string.IsNullOrEmpty(magic.Data.iconName))
            return null;
        return Resources.Load<Sprite>("Images/Magics/" + magic.Data.iconName);
    }

    private static Sprite LoadEnemyIntentIcon(EnemyIntentData intent)
    {
        string displayType = intent != null ? intent.displayType : null;
        if (!string.IsNullOrEmpty(displayType))
        {
            Sprite sprite = Resources.Load<Sprite>("Images/Intent/" + displayType);
            if (sprite != null)
                return sprite;
        }
        return null;
    }

    private static Sprite LoadRewardIcon(BonusRewardData rewardData)
    {
        if (rewardData == null || string.IsNullOrEmpty(rewardData.texturePath))
            return null;
        return Resources.Load<Sprite>(rewardData.texturePath);
    }

    private static Color GetRewardAccentColor(BonusRewardData rewardData)
    {
        if (rewardData == null)
            return Color.white;
        switch (rewardData.rewardType)
        {
            case BonusRewardType.Gold: return new Color(1f, 0.88f, 0.22f, 1f);
            case BonusRewardType.Heal: return new Color(0.2f, 1f, 0.78f, 1f);
            default: return Color.white;
        }
    }
}

public static class EventDetailTextUtility
{
    public static string GetOptionEffectText(EventOptionData option)
    {
        if (option != null && option.effects != null && option.effects.Length > 0)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < option.effects.Length; i++)
            {
                string effectText = GetEffectDataText(option.effects[i], option);
                if (string.IsNullOrEmpty(effectText))
                    continue;
                if (builder.Length > 0)
                    builder.Append("；");
                builder.Append(effectText);
            }
            return "效果：" + (builder.Length > 0 ? builder.ToString() : "无直接效果");
        }

        string effect = "无直接效果";
        if (option.resultId == 1)
            effect = "恢复10点生命";
        else if (option.resultId == 2)
            effect = "之后每回合抽牌数+1";
        else if (option.resultId == 100)
            effect = "选择并删除" + GetChoiceCountText(option) + "张箭头";
        else if (option.resultId >= 101 && option.resultId <= 104)
            effect = "获得1张箭头";
        else if (option.resultId == 201)
            effect = "选择" + GetChoiceCountText(option) + "张手牌箭头，添加助燃";
        else if (option.resultId == 202)
            effect = "选择" + GetChoiceCountText(option) + "张手牌箭头，添加流转";
        else if (option.resultId == 203)
            effect = "选择" + GetChoiceCountText(option) + "张手牌箭头，添加液化";
        else if (option.resultId == 300)
            effect = "恢复30%最大生命";
        else if (option.resultId == 301)
            effect = LocalizationSystem.GetText("rest.option.study.effect", "从2个强化中选择1个，附魔到一个道具上");
        else if (option.resultId == 302)
            effect = LocalizationSystem.GetText("rest.option.deep_study.effect", "从3个强化中选择1个，附魔到一个道具上");

        return "效果：" + effect;
    }

    private static string GetEffectDataText(EventEffectData effect, EventOptionData option)
    {
        if (effect == null)
            return string.Empty;

        switch (effect.rewardType)
        {
            case EventRewardType.Heal:
                return "恢复" + GetEffectAmountText(effect, 10) + "点生命";
            case EventRewardType.LoseHealth:
                return "失去" + GetEffectAmountText(effect, 1) + "点生命" + (effect.escalatePerUse > 0 ? "，每次+" + effect.escalatePerUse : string.Empty);
            case EventRewardType.GainGold:
                return "获得" + GetEffectAmountText(effect, 1) + "金币";
            case EventRewardType.GainMagic:
                return "获得一次道具奖励";
            case EventRewardType.GainMagicModifier:
                return "获得一次道具强化";
            case EventRewardType.IncreaseMaxHealth:
                return "生命上限+" + GetEffectAmountText(effect, 5);
            case EventRewardType.GainMaterial:
                return "获得" + GetEffectCountText(effect, 1) + "张箭头";
            case EventRewardType.GainRandomMaterial:
                return "获得" + GetEffectCountText(effect, 1) + "张随机箭头";
            case EventRewardType.GainSameRandomMaterials:
                return "获得" + GetEffectCountText(effect, 2) + "张相同的随机箭头";
            case EventRewardType.IncreaseDrawCount:
                return "每回合抽牌数+" + GetEffectAmountText(effect, 1);
            case EventRewardType.RemoveMaterial:
                return "删除" + GetEffectChoiceCountText(effect, option, 1) + "张箭头";
            case EventRewardType.GainNextBattleStartShield:
                return "下一场战斗开始时获得" + GetEffectAmountText(effect, 3) + "点护盾";
            case EventRewardType.GainMaterialModifier:
                return "选择" + GetEffectChoiceCountText(effect, option, 1) + "张箭头获得" + GetModifierNameText(effect.modifierId);
            case EventRewardType.SpendAllGold:
                return "花光所有金币";
            case EventRewardType.RandomizeDeckBasicMaterials:
                return "将牌库中的基础箭头随机重置";
            case EventRewardType.GainRandomSyntaxMaterial:
                return "获得" + GetEffectCountText(effect, 1) + "张随机语法箭头";
            default:
                return string.Empty;
        }
    }

    private static string GetModifierNameText(string modifierId)
    {
        return LocalizationSystem.GetText("modifier." + modifierId + ".name", modifierId);
    }

    private static string GetEffectAmountText(EventEffectData effect, int fallback)
    {
        int amount = effect != null && effect.amount > 0 ? effect.amount : fallback;
        return amount.ToString();
    }

    private static string GetEffectCountText(EventEffectData effect, int fallback)
    {
        int count = effect != null && effect.count > 0 ? effect.count : fallback;
        return count.ToString();
    }

    private static string GetEffectChoiceCountText(EventEffectData effect, EventOptionData option, int fallback)
    {
        int count = effect != null && effect.choiceCount > 0 ? effect.choiceCount : option != null && option.choiceCount > 0 ? option.choiceCount : fallback;
        return count.ToString();
    }

    private static string GetChoiceCountText(EventOptionData option)
    {
        return option != null && option.choiceCount > 0 ? option.choiceCount.ToString() : "1";
    }
}
