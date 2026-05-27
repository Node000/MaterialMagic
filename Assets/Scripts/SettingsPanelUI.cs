using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SettingsPanelUI : MonoBehaviour
{
    [SerializeField] private string startSceneName = "StartScene";

    private Slider musicSlider;
    private Slider sfxSlider;

    public void Initialize(HandSystemUI owner)
    {
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
        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.LoadSceneWithTransition(startSceneName);
        else
            SceneManager.LoadScene(startSceneName);
    }
}
