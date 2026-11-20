using UnityEngine;
using UnityEngine.EventSystems;

#if !MATERIALMAGIC_STANDALONE
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "RunHistoryArrowItemUI")]
#endif
public class RunHistoryArrowItemUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private RunHistoryArrowRowUI owner;
    private int index;
    private MaterialModel material;

    public void Initialize(RunHistoryArrowRowUI owner, int index, MaterialModel material)
    {
        this.owner = owner;
        this.index = index;
        this.material = material;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        owner?.SetHover(index, material);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        owner?.ClearHover(index);
    }
}
