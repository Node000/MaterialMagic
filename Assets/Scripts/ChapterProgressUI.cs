using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if !MATERIALMAGIC_STANDALONE
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "ChapterProgressUI")]
#endif
public class ChapterProgressUI : MonoBehaviour
{
    [SerializeField] private TMP_Text progressText;

    public void Initialize()
    {
        CacheReferences();
    }

    public void SetProgress(int current, int total)
    {
        CacheReferences();
        if (progressText != null)
            progressText.text = $"{current:00}/{total:00}";
    }

    private void CacheReferences()
    {
        if (progressText == null)
            progressText = GetComponent<TMP_Text>();
    }
}
