using UnityEngine;

#if !MATERIALMAGIC_STANDALONE
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "SceneTransitionFocusManager")]
#endif
public class SceneTransitionFocusManager : MonoBehaviour
{
    [SerializeField] private GameObject focusTarget;

    public GameObject FocusTarget => focusTarget;
}
