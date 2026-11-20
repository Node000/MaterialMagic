using UnityEngine;

#if !MATERIALMAGIC_STANDALONE
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "PrefabReferenceLibrary")]
#endif
public class PrefabReferenceLibrary : MonoBehaviour
{
    [SerializeField] private RectTransform materialCardPrefab;
    [SerializeField] private RectTransform popupDragonWindowBlankPrefab;

    public RectTransform MaterialCardPrefab => materialCardPrefab;
    public RectTransform PopupDragonWindowBlankPrefab => popupDragonWindowBlankPrefab;
}
