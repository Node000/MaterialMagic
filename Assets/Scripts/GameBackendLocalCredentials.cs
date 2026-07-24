using UnityEngine;

[CreateAssetMenu(fileName = "GameBackendLocalCredentials", menuName = "Config/Game Backend Local Credentials")]
public class GameBackendLocalCredentials : ScriptableObject
{
    [SerializeField] private string tapTapClientId;
    [SerializeField] private string tapTapClientToken;

    public string TapTapClientId => tapTapClientId;
    public string TapTapClientToken => tapTapClientToken;
    public bool HasTapTapCredentials => !string.IsNullOrWhiteSpace(tapTapClientId) && !string.IsNullOrWhiteSpace(tapTapClientToken);
}
