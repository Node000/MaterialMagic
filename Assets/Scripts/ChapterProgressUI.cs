using UnityEngine;
using UnityEngine.UI;

public class ChapterProgressUI : MonoBehaviour
{
    [SerializeField] private Text progressText;

    public void Initialize()
    {
        CacheReferences();
    }

    public void SetProgress(int current, int total)
    {
        CacheReferences();
        if (progressText != null)
            progressText.text = $"关卡 {current:00}/{total:00}";
    }

    private void CacheReferences()
    {
        if (progressText == null)
            progressText = GetComponent<Text>();
    }
}
