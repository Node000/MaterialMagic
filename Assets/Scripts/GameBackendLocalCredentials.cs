using UnityEngine;

[CreateAssetMenu(fileName = "GameBackendLocalCredentials", menuName = "Config/Game Backend Local Credentials")]
#if !MATERIALMAGIC_STANDALONE
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "GameBackendLocalCredentials")]
#endif
public class GameBackendLocalCredentials : ScriptableObject
{
    [SerializeField] private string tapTapClientId;
    [SerializeField] private string tapTapClientToken;

    public string TapTapClientId => tapTapClientId;
    public string TapTapClientToken => tapTapClientToken;
    public bool HasTapTapCredentials => !string.IsNullOrWhiteSpace(tapTapClientId) && !string.IsNullOrWhiteSpace(tapTapClientToken);
}
