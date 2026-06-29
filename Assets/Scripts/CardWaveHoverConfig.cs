using UnityEngine;

[CreateAssetMenu(fileName = "CardWaveHoverConfig", menuName = "Config/Card Wave Hover Config")]
public class CardWaveHoverConfig : ScriptableObject
{
    [SerializeField] private float hoverYOffset = 32f;
    [SerializeField] private float hoverScale = 1.18f;
    [SerializeField] private float falloffPower = 1.35f;

    public float HoverYOffset => Mathf.Max(0f, hoverYOffset);
    public float HoverScale => Mathf.Max(0.01f, hoverScale);
    public float FalloffPower => Mathf.Max(0.01f, falloffPower);
}
