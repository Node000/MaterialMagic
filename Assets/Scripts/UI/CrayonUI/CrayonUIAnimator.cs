using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RawImage))]
public sealed class CrayonUIAnimator : MonoBehaviour
{
    [Tooltip("Keep the background animated when Time.timeScale is zero.")]
    [SerializeField] private bool useUnscaledTime = true;

    private static readonly int AspectId = Shader.PropertyToID("_Aspect");
    private static readonly int TimeId = Shader.PropertyToID("_CrayonTime");
    private RawImage image;
    private Material originalMaterial;
    private Material instance;
    private float startScaledTime;
    private float startUnscaledTime;

    private void OnEnable()
    {
        image = GetComponent<RawImage>();
        originalMaterial = image.material;
        if (originalMaterial == null || originalMaterial.shader == null ||
            originalMaterial.shader.name != "UI/ProceduralCrayon")
        {
            Debug.LogError("Assign a UI/ProceduralCrayon material to this RawImage first.", this);
            return;
        }

        // Separate instances prevent different UI rectangles sharing aspect/time.
        instance = new Material(originalMaterial)
        {
            name = originalMaterial.name + " (Runtime)",
            hideFlags = HideFlags.DontSave
        };
        image.material = instance;
        startScaledTime = Time.time;
        startUnscaledTime = Time.unscaledTime;
        Canvas.willRenderCanvases += UpdateMaterial;
        UpdateMaterial();
    }

    private void UpdateMaterial()
    {
        if (instance == null || image == null)
            return;

        Rect rect = image.rectTransform.rect;
        float aspect = Mathf.Abs(rect.width) / Mathf.Max(Mathf.Abs(rect.height), 0.001f);
        float elapsed = useUnscaledTime
            ? Time.unscaledTime - startUnscaledTime
            : Time.time - startScaledTime;
        Apply(instance, aspect, elapsed);

        // A parent Mask can cause uGUI to render with a stencil material copy.
        // Update only our two properties; leave stencil settings under uGUI control.
        Material rendered = image.materialForRendering;
        if (rendered != null && rendered != instance)
            Apply(rendered, aspect, elapsed);
    }

    private static void Apply(Material target, float aspect, float elapsed)
    {
        target.SetFloat(AspectId, aspect);
        target.SetFloat(TimeId, elapsed);
    }

    private void OnDisable()
    {
        Canvas.willRenderCanvases -= UpdateMaterial;
        if (image != null && image.material == instance)
            image.material = originalMaterial;
        if (instance != null)
            Destroy(instance);
        instance = null;
    }
}
