using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
[AddComponentMenu("VFX/Screen Edge Scribble Effect")]
#if !MATERIALMAGIC_STANDALONE
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "ScreenEdgeScribbleEffect")]
#endif
public class ScreenEdgeScribbleEffect : MonoBehaviour
{
    [SerializeField] private Material edgeMaskMaterial;

    private Renderer cachedRenderer;

    private void OnEnable()
    {
        ApplyMaskMaterial();
    }

    private void OnValidate()
    {
        ApplyMaskMaterial();
    }

    public void SetEdgeMaskMaterial(Material material)
    {
        edgeMaskMaterial = material;
        ApplyMaskMaterial();
    }

    private void ApplyMaskMaterial()
    {
        if (cachedRenderer == null)
            cachedRenderer = GetComponent<Renderer>();
        if (cachedRenderer != null && edgeMaskMaterial != null)
            cachedRenderer.sharedMaterial = edgeMaskMaterial;
    }
}
