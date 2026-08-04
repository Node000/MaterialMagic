using System;
using UnityEngine;

[Serializable]
public class ArrowUpgradeVisualProfile
{
    public ArrowUpgradeDirection direction;
    public Color outlineColor = Color.white;
    [Min(0f)] public float outlineWidth = 2f;
    [Min(0f)] public float pulseSpeed = 1.2f;
    [Range(0f, 1f)] public float pulseAmount = 0.2f;
    public bool useOuterGlow = true;
    public Color outerGlowColor = Color.white;
    [Min(1f)] public float iconScale = 1.06f;
}

[CreateAssetMenu(fileName = "ArrowUpgradeVisualConfig", menuName = "Material Magic/Arrow Upgrade Visual Config")]
public class ArrowUpgradeVisualConfig : ScriptableObject
{
    [SerializeField] private ArrowUpgradeVisualProfile[] profiles = Array.Empty<ArrowUpgradeVisualProfile>();

    public ArrowUpgradeVisualProfile GetProfile(ArrowUpgradeDirection direction)
    {
        for (int i = 0; i < profiles.Length; i++)
        {
            ArrowUpgradeVisualProfile profile = profiles[i];
            if (profile != null && profile.direction == direction)
                return profile;
        }
        return null;
    }
}
