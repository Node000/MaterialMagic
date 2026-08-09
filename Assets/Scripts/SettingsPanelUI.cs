using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SettingsPanelUI : MonoBehaviour
{
    [SerializeField] private string startSceneName = "StartScene";

    private Slider musicSlider;
    private Slider sfxSlider;
    private HandSystemUI owner;

    public void Initialize(HandSystemUI owner)
    {
        this.owner = owner;
        BindControls();
    }

    public void Toggle()
    {
        bool show = !gameObject.activeSelf;
        gameObject.SetActive(show);
        if (show)
            BindControls();
    }

    private void BindControls()
    {
        BindCloseButton();
        BindReturnButton();
        BindSliders();
        RefreshLocalizedText();
    }

    private void RefreshLocalizedText()
    {
        SetLocalizedText("PopupDragonWindowBackground/TitleText", "ui.start_settings.title", "C:/设置");
        SetLocalizedText("Title", "ui.battle_settings.title", "设置");
        SetLocalizedText("MusicLabel", "ui.battle_settings.music", "音乐");
        SetLocalizedText("SfxLabel", "ui.battle_settings.sfx", "音效");
        SetLocalizedText("CloseButton/Text", "ui.common.close", "关闭");
        SetLocalizedText(
            "ReturnStartButton/Text",
            RunSaveSystem.IsTutorialRunActive() ? "ui.battle_settings.return_menu" : "ui.battle_settings.return_start",
            RunSaveSystem.IsTutorialRunActive() ? "返回主菜单" : "保存并退出");
    }

    private void SetLocalizedText(string path, string key, string fallback)
    {
        TMPro.TMP_Text target = UIManager.FindChildComponent<TMPro.TMP_Text>(transform, path);
        if (target != null)
            target.text = LocalizationSystem.GetText(key, fallback);
    }

    private void BindCloseButton()
    {
        Button closeButton = UIManager.FindChildComponent<Button>(transform, "CloseButton");
        if (closeButton == null)
            return;

        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(Toggle);
    }

    private void BindReturnButton()
    {
        Button returnButton = UIManager.FindChildComponent<Button>(transform, "ReturnStartButton");
        if (returnButton == null)
            return;

        returnButton.onClick.RemoveAllListeners();
        returnButton.onClick.AddListener(ReturnToStartMenu);
    }

    private void BindSliders()
    {
        musicSlider = UIManager.FindChildComponent<Slider>(transform, "MusicSlider");
        sfxSlider = UIManager.FindChildComponent<Slider>(transform, "SfxSlider");

        if (musicSlider != null)
        {
            musicSlider.onValueChanged.RemoveListener(SetMusicVolume);
            musicSlider.SetValueWithoutNotify(AudioManager.Instance != null ? AudioManager.Instance.MusicVolume : 0.8f);
            musicSlider.onValueChanged.AddListener(SetMusicVolume);
        }

        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.RemoveListener(SetSfxVolume);
            sfxSlider.SetValueWithoutNotify(AudioManager.Instance != null ? AudioManager.Instance.SfxVolume : 0.8f);
            sfxSlider.onValueChanged.AddListener(SetSfxVolume);
        }
    }

    private void SetMusicVolume(float value)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetMusicVolume(value);
    }

    private void SetSfxVolume(float value)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetSfxVolume(value);
    }

    private void ReturnToStartMenu()
    {
        if (owner != null)
        {
            owner.SaveCurrentRunAndReturnToStart(startSceneName);
            return;
        }

        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.LoadSceneWithTransition(startSceneName);
        else
            SceneManager.LoadScene(startSceneName);
    }
}
