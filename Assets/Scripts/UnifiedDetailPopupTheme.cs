using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

[CreateAssetMenu(fileName = "UnifiedDetailPopupTheme", menuName = "Config/Unified Detail Popup Theme")]
public class UnifiedDetailPopupTheme : ScriptableObject
{
    [Header("Animation")]
    [SerializeField] private Vector3 hiddenScale = new Vector3(0.82f, 0.82f, 1f);
    [SerializeField] private float fadeDuration = 0.12f;
    [SerializeField] private float scaleDuration = 0.18f;
    [SerializeField] private float hoverExitHideDelay = 0.15f;
    [SerializeField] private Ease showEase = Ease.OutBack;
    [SerializeField] private Ease hideEase = Ease.InBack;

    [Header("Icon Scale By Source")]
    [Tooltip("没有在下面逐项列出的来源类型，都按这个倍数缩放主图标；1 = 保持 Scene 里摆好的大小。")]
    [SerializeField, Min(0.01f)] private float defaultIconScale = 1f;
    [Tooltip("按详情来源类型覆盖主图标缩放：例如事件选项图标、商店功能按钮图标缩到 0.8。" +
             "倍数乘在 Scene 里 Icon 槽的 localScale 上（不写 sizeDelta，避免拉伸锚点语义差异），并自动补回 pivot 缩放带来的位移。")]
    [SerializeField] private List<IconScaleEntry> iconScales = new List<IconScaleEntry>();

    public Vector3 HiddenScale => hiddenScale;
    public float FadeDuration => Mathf.Max(0f, fadeDuration);
    public float ScaleDuration => Mathf.Max(0f, scaleDuration);
    public float HoverExitHideDelay => Mathf.Max(0f, hoverExitHideDelay);
    public Ease ShowEase => showEase;
    public Ease HideEase => hideEase;

    /// <summary>取某个详情来源类型的主图标缩放倍数；未在 iconScales 里列出的类型用 <see cref="defaultIconScale"/>。</summary>
    public float GetIconScale(UnifiedDetailSourceType sourceType)
    {
        if (iconScales != null)
        {
            for (int i = 0; i < iconScales.Count; i++)
            {
                IconScaleEntry entry = iconScales[i];
                if (entry.sourceType != sourceType)
                    continue;

                return Mathf.Max(0.01f, entry.scale);
            }
        }

        return Mathf.Max(0.01f, defaultIconScale);
    }

    /// <summary>详情面板主图标：某个来源类型对应的缩放倍数。</summary>
    [Serializable]
    public struct IconScaleEntry
    {
        [Tooltip("详情来源类型（UnifiedDetailSourceType）")]
        public UnifiedDetailSourceType sourceType;

        [Tooltip("缩放倍数：0.8 = 缩到 80%，1 = 保持原始大小")]
        [Min(0.01f)] public float scale;
    }
}
