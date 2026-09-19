using UnityEngine;

[CreateAssetMenu(fileName = "MaterialModifierDefinition", menuName = "Enchant/Material Modifier Definition")]
public sealed class MaterialModifierDefinition : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string script;
    [SerializeField] private string nameKey;
    [SerializeField] private string descriptionKey;
    [SerializeField] private Color lineColor = Color.white;
    [SerializeField] private bool inArrowModifierRewardPool;
    [Tooltip("商店价格差值：相对「无附魔普通箭头价」的增减（正数更贵、负数更便宜）。0 = 不影响价格。")]
    [SerializeField] private int price;
    [SerializeField] private Material visualMaterial;

    public string Id => id;
    public string Script => script;
    public int Price => price;
    public Material VisualMaterial => visualMaterial;

    public MaterialModifierData CreateRuntimeData()
    {
        return new MaterialModifierData
        {
            id = id,
            script = script,
            nameKey = nameKey,
            descriptionKey = descriptionKey,
            lineColor = "#" + ColorUtility.ToHtmlStringRGB(lineColor),
            inArrowModifierRewardPool = inArrowModifierRewardPool,
            price = price
        };
    }

    public void SetData(MaterialModifierData value, Material valueVisualMaterial)
    {
        if (value == null)
            return;

        id = value.id;
        script = value.script;
        nameKey = value.nameKey;
        descriptionKey = value.descriptionKey;
        if (!ColorUtility.TryParseHtmlString(value.lineColor, out lineColor))
            lineColor = Color.white;
        inArrowModifierRewardPool = value.inArrowModifierRewardPool;
        price = value.price;
        visualMaterial = valueVisualMaterial;
    }
}
