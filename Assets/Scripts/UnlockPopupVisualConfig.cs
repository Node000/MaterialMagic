using UnityEngine;

[CreateAssetMenu(fileName = "UnlockPopupVisualConfig", menuName = "Config/Unlock Popup Visual Config")]
public class UnlockPopupVisualConfig : ScriptableObject
{
    [SerializeField] private Color startConfigTypeColor = new Color(0.95f, 0.72f, 0.28f, 1f);
    [SerializeField] private Color magicTypeColor = new Color(0.45f, 0.78f, 1f, 1f);
    [SerializeField] private Color materialModifierTypeColor = new Color(1f, 0.55f, 0.24f, 1f);
    [SerializeField] private Color magicModifierTypeColor = new Color(0.8f, 0.55f, 1f, 1f);
    [SerializeField] private Color titleColor = Color.white;
    [SerializeField] private Color nameColor = Color.white;

    public Color StartConfigTypeColor => startConfigTypeColor;
    public Color MagicTypeColor => magicTypeColor;
    public Color MaterialModifierTypeColor => materialModifierTypeColor;
    public Color MagicModifierTypeColor => magicModifierTypeColor;
    public Color TitleColor => titleColor;
    public Color NameColor => nameColor;

    public Color GetTypeColor(string targetType)
    {
        switch (targetType)
        {
            case UnlockSystem.TargetStartConfig:
                return startConfigTypeColor;
            case UnlockSystem.TargetMagic:
                return magicTypeColor;
            case UnlockSystem.TargetMaterialModifier:
                return materialModifierTypeColor;
            case UnlockSystem.TargetMagicModifier:
                return magicModifierTypeColor;
            default:
                return nameColor;
        }
    }
}
