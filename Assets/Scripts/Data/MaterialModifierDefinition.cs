using UnityEngine;

[CreateAssetMenu(fileName = "MaterialModifierDefinition", menuName = "Enchant/Material Modifier Definition")]
#if !MATERIALMAGIC_STANDALONE
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "MaterialModifierDefinition")]
#endif
public sealed class MaterialModifierDefinition : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string script;
    [SerializeField] private string nameKey;
    [SerializeField] private string descriptionKey;
    [SerializeField] private Color lineColor = Color.white;
    [SerializeField] private bool inArrowModifierRewardPool;
    [Tooltip("随机附魔掉落的相对权重。0 = 不参与随机掉落；不影响指定发放或商店。")]
    [SerializeField] private int weight = 1;
    [Tooltip("同一箭头可获得多个独立实例。仅用于效果可以累加的附魔。")]
    [SerializeField] private bool stackable;
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
            weight = weight,
            stackable = stackable,
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
        weight = value.weight;
        stackable = value.stackable;
        price = value.price;
        visualMaterial = valueVisualMaterial;
    }
}
