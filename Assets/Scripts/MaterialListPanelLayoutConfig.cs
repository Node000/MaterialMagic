using UnityEngine;

[CreateAssetMenu(fileName = "MaterialListPanelLayoutConfig", menuName = "Config/Material List Panel Layout Config")]
public class MaterialListPanelLayoutConfig : ScriptableObject
{
    [SerializeField] private float arrowRowTotalLength = 780f;
    [SerializeField] private float arrowDefaultScale = 0.72f;
    [SerializeField] private float arrowHoverScale = 1.18f;

    public float ArrowRowTotalLength => Mathf.Max(0f, arrowRowTotalLength);
    public float ArrowDefaultScale => Mathf.Max(0.01f, arrowDefaultScale);
    public float ArrowHoverScale => Mathf.Max(0.01f, arrowHoverScale);
}
