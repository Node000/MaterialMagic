using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public class LocalizedImageSprite : MonoBehaviour
{
    [Serializable]
    private class LocalizedSpriteEntry
    {
        public string key;
        public Sprite sprite;
    }

    [SerializeField] private Sprite defaultSprite;
    [SerializeField] private List<LocalizedSpriteEntry> localizedSprites = new List<LocalizedSpriteEntry>();

    private Image image;

    private void Awake()
    {
        CacheImage();
        if (defaultSprite == null && image != null)
            defaultSprite = image.sprite;
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

    public void Refresh()
    {
        CacheImage();
        if (image == null)
            return;

        LocalizationSystem.Initialize();
        Sprite sprite = GetSprite(LocalizationSystem.CurrentLanguage);
        if (sprite != null)
            image.sprite = sprite;
    }

    private Sprite GetSprite(string key)
    {
        for (int i = 0; i < localizedSprites.Count; i++)
        {
            LocalizedSpriteEntry entry = localizedSprites[i];
            if (entry != null && entry.key == key && entry.sprite != null)
                return entry.sprite;
        }

        return defaultSprite;
    }

    private void CacheImage()
    {
        if (image == null)
            image = GetComponent<Image>();
    }
}
