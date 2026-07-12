using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class DebugUnlockA20Button : MonoBehaviour
{
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(UnlockAndSelectA20);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(UnlockAndSelectA20);
    }

    private void UnlockAndSelectA20()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        int targetLevel = Mathf.Min(20, AscensionSystem.MaxAscensionLevel);
        if (targetLevel <= 0)
            return;

        UnlockSystem.GrantUnlock(UnlockSystem.TargetFeature, "ascension", false);
        UnlockProgressData progress = UnlockProgressSaveSystem.LoadCurrent();
        progress.highestAscensionUnlocked = targetLevel;
        progress.selectedAscensionLevel = targetLevel;
        UnlockProgressSaveSystem.SaveCurrent(progress);
#endif
    }
}
