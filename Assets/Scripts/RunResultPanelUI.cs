using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RunResultPanelUI : MonoBehaviour
{
    [SerializeField] private string startSceneName = "StartScene";

    private Text titleText;
    private Text bodyText;
    private Button returnButton;

    public void Initialize(HandSystemUI owner)
    {
        CacheReferences();
        gameObject.SetActive(false);
    }

    public void ShowVictory()
    {
        Show("胜利", "你已通过最后一个章节的最后一关。");
    }

    public void ShowDefeat()
    {
        Show("失败", "生命值归零，冒险结束。");
    }

    private void Show(string title, string body)
    {
        CacheReferences();
        if (titleText != null)
            titleText.text = title;
        if (bodyText != null)
            bodyText.text = body;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    private void CacheReferences()
    {
        if (titleText == null)
            titleText = UIManager.FindChildComponent<Text>(transform, "Title");
        if (bodyText == null)
            bodyText = UIManager.FindChildComponent<Text>(transform, "Body");
        if (returnButton == null)
            returnButton = UIManager.FindChildComponent<Button>(transform, "ReturnStartButton");

        if (returnButton != null)
        {
            returnButton.onClick.RemoveAllListeners();
            returnButton.onClick.AddListener(ReturnToStartMenu);
        }
    }

    private void ReturnToStartMenu()
    {
        RunSaveSystem.ClearCurrentRun();
        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.LoadSceneWithTransition(startSceneName);
        else
            SceneManager.LoadScene(startSceneName);
    }
}
