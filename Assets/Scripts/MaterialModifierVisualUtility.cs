using UnityEngine.UI;

public static class MaterialModifierVisualUtility
{
    public static void ApplyTo(Image image, MaterialModel card)
    {
        if (image == null)
            return;

        // 统一入口：有附魔 → RT 链；无附魔 → 清链回原图。
        // RT 链让附魔按 card.modifiers 顺序逐级消费上一级输出（原图/上一 RT），
        // 从而切半/改形/复制/光效等任意组合都链式生效，无需任何组合 shader。
        MaterialModifierRTChain.ApplyTo(image, card);
    }
}
