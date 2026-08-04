using System;
using UnityEngine;

[Serializable]
public class ArrowUpgradeEffectValue
{
    public string nodeId;
    public int primaryValue = 1;
    public int secondaryValue;
}

[CreateAssetMenu(fileName = "ArrowUpgradeConfig", menuName = "Material Magic/Arrow Upgrade Config")]
public class ArrowUpgradeConfig : ScriptableObject
{
    [SerializeField] private ArrowUpgradeEffectValue[] effects = Array.Empty<ArrowUpgradeEffectValue>();

    public int GetPrimaryValue(string nodeId, int fallback)
    {
        ArrowUpgradeEffectValue value = Get(nodeId);
        return value != null ? value.primaryValue : fallback;
    }

    public int GetSecondaryValue(string nodeId, int fallback)
    {
        ArrowUpgradeEffectValue value = Get(nodeId);
        return value != null ? value.secondaryValue : fallback;
    }

    private ArrowUpgradeEffectValue Get(string nodeId)
    {
        for (int i = 0; i < effects.Length; i++)
        {
            ArrowUpgradeEffectValue value = effects[i];
            if (value != null && value.nodeId == nodeId)
                return value;
        }
        return null;
    }
}
