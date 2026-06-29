using UnityEngine;

[CreateAssetMenu(fileName = "ArrowSelectionWaveHoverConfig", menuName = "Config/Arrow Selection Wave Hover Config")]
public class ArrowSelectionWaveHoverConfig : ScriptableObject
{
    [SerializeField] private float hoverYOffset = 32f;
    [SerializeField] private float hoverScale = 1.18f;
    [SerializeField] private float falloffPower = 1.35f;
    [SerializeField] private float hoverSpread = 42f;

    public float HoverYOffset => Mathf.Max(0f, hoverYOffset);
    public float HoverScale => Mathf.Max(0.01f, hoverScale);
    public float FalloffPower => Mathf.Max(0.01f, falloffPower);
    public float HoverSpread => Mathf.Max(0f, hoverSpread);
}
