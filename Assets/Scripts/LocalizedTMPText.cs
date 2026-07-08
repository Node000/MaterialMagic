using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class LocalizedTMPText : MonoBehaviour
{
    [SerializeField] private string key;
    [SerializeField, TextArea] private string fallback;

    private TMP_Text text;

    private void Awake()
    {
        CacheText();
        Refresh();
    }

    private void OnEnable()
    {
        LocalizationSystem.LanguageChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        LocalizationSystem.LanguageChanged -= Refresh;
    }

    public void SetKey(string key, string fallback)
    {
        this.key = key;
        this.fallback = fallback;
        Refresh();
    }

    public void Refresh()
    {
        CacheText();
        if (text != null)
            text.text = LocalizationSystem.GetText(key, fallback);
    }

    private void CacheText()
    {
        if (text == null)
            text = GetComponent<TMP_Text>();
    }
}
