using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 箭头附魔图标的两层颜色配置：每个附魔（<see cref="MaterialModifierData.id"/>）一组「上层 / 底色」颜色。
/// 资产：<c>Assets/Resources/Config/Enchant_Color.asset</c>。
/// 未在配置里的附魔保持图标预制体自身颜色（代码不写任何颜色默认值）。
/// </summary>
[CreateAssetMenu(fileName = "Enchant_Color", menuName = "Enchant/Enchant Color Config")]
public sealed class EnchantColorConfig : ScriptableObject
{
    private const string ResourcePath = "Config/Enchant_Color";

    [Serializable]
    public class Entry
    {
        [Tooltip("附魔 id（与 Assets/Resources/EnchantConfig/MaterialModifiers/<id>.asset 的 id 一致）")]
        public string modifierId;

        [Tooltip("附魔中文名注释（只给美术看，不参与运行逻辑）；用 Inspector 上的「刷新附魔中文名」或菜单 Tools/Content/Enchant 从本地化表重新写入")]
        public string displayName;

        [Tooltip("图标上层颜色")]
        public Color topColor = Color.white;

        [Tooltip("图标底色层颜色")]
        public Color baseColor = Color.white;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    private static EnchantColorConfig cached;

    /// <summary>配置资产（Resources/Config/Enchant_Color）；缺失时为 null。</summary>
    public static EnchantColorConfig Instance
    {
        get
        {
            if (cached == null)
                cached = Resources.Load<EnchantColorConfig>(ResourcePath);
            return cached;
        }
    }

    /// <summary>供编辑器工具（建表 / 补条目）使用，运行时只读。</summary>
    public List<Entry> Entries => entries;

    /// <summary>取某个附魔的两层颜色；未配置时返回 false（调用方保留已有颜色）。</summary>
    public static bool TryGetColors(string modifierId, out Color topColor, out Color baseColor)
    {
        topColor = Color.white;
        baseColor = Color.white;

        EnchantColorConfig config = Instance;
        if (config == null || config.entries == null || string.IsNullOrEmpty(modifierId))
            return false;

        for (int i = 0; i < config.entries.Count; i++)
        {
            Entry entry = config.entries[i];
            if (entry == null || entry.modifierId != modifierId)
                continue;

            topColor = entry.topColor;
            baseColor = entry.baseColor;
            return true;
        }

        return false;
    }
}
