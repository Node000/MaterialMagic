using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class RunResultPanelUI : MonoBehaviour
{
    [SerializeField] private string startSceneName = "StartScene";
    [SerializeField] private float unfoldDuration = 0.35f;
    [SerializeField] private Ease unfoldEase = Ease.OutCubic;
    [SerializeField] private float fadeDuration = 0.12f;

    private TMP_Text titleText;
    private TMP_Text bodyText;
    private TMP_Text buttonText;
    private Button returnButton;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Vector3 shownScale = Vector3.one;
    private Tween showTween;

    public void Initialize(HandSystemUI owner)
    {
        CacheReferences();
        if (rectTransform != null)
            rectTransform.localScale = shownScale;
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
        gameObject.SetActive(false);
    }

    public void ShowVictory()
    {
        ShowVictory(0f, null, false);
    }

    public void ShowVictory(float playSeconds, IReadOnlyList<string> magicNames, bool tutorialVictory = false)
    {
        string bodyTemplate = tutorialVictory
            ? LocalizationSystem.GetText("ui.run_result.victory.tutorial.body", "恭喜你完成了新手教程！")
            : LocalizationSystem.GetText(
                "ui.run_result.victory.body",
                "你花费了{0}，通过了全部的关卡\n你携带的东西有{1}\n真是一场令人喜悦的胜利\n现在，该休息一会儿了");
        if (!tutorialVictory)
            bodyTemplate = bodyTemplate.Replace("{0}", FormatPlayTime(playSeconds)).Replace("{1}", FormatMagicList(magicNames));
        Show(
            LocalizationSystem.GetText("ui.run_result.victory.title", "胜利"),
            bodyTemplate,
            LocalizationSystem.GetText("ui.run_result.victory.return_button", "结束"));
    }

    public void ShowDefeat(string defeatingEnemyName = null)
    {
        string enemyName = string.IsNullOrEmpty(defeatingEnemyName)
            ? LocalizationSystem.GetText("ui.run_result.unknown_enemy", "未知敌人")
            : defeatingEnemyName;
        string bodyTemplate = LocalizationSystem.GetText(
            "ui.run_result.defeat.body",
            "你到达了，最后被{0}击败。\n显然，你的精神已经非常疲惫。\n现在该睡觉了");
        Show(
            LocalizationSystem.GetText("ui.run_result.defeat.title", "游戏结束"),
            bodyTemplate.Replace("{0}", enemyName),
            LocalizationSystem.GetText("ui.run_result.defeat.return_button", "结束"));
    }

    private string FormatPlayTime(float playSeconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(playSeconds));
        int hours = totalSeconds / 3600;
        int minutes = totalSeconds % 3600 / 60;
        int seconds = totalSeconds % 60;

        if (hours > 0)
        {
            return string.Format(
                LocalizationSystem.GetText("ui.run_result.time.hours_minutes_seconds", "{0}小时{1}分钟{2}秒"),
                hours,
                minutes,
                seconds);
        }

        if (minutes > 0)
        {
            return string.Format(
                LocalizationSystem.GetText("ui.run_result.time.minutes_seconds", "{0}分钟{1}秒"),
                minutes,
                seconds);
        }

        return string.Format(
            LocalizationSystem.GetText("ui.run_result.time.seconds", "{0}秒"),
            seconds);
    }

    private string FormatMagicList(IReadOnlyList<string> magicNames)
    {
        string emptyText = LocalizationSystem.GetText("ui.run_result.victory.empty_magic_list", "无");
        if (magicNames == null || magicNames.Count == 0)
            return emptyText;

        string separator = LocalizationSystem.GetText("ui.run_result.victory.magic_separator", "、");
        StringBuilder builder = new StringBuilder();
        int appendedCount = 0;
        for (int i = 0; i < magicNames.Count; i++)
        {
            string magicName = magicNames[i];
            if (string.IsNullOrEmpty(magicName))
                continue;

            if (appendedCount > 0)
                builder.Append(separator);
            builder.Append(magicName);
            appendedCount++;
        }

        return appendedCount > 0 ? builder.ToString() : emptyText;
    }

    public void Hide()
    {
        showTween?.Kill(false);
        gameObject.SetActive(false);
    }

    private void Show(string title, string body, string returnLabel)
    {
        CacheReferences();
        if (titleText != null)
            titleText.text = title;
        if (bodyText != null)
            bodyText.text = body;
        if (buttonText != null)
            buttonText.text = returnLabel;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        PlayShowAnimation();
    }

    private void PlayShowAnimation()
    {
        showTween?.Kill(false);
        if (returnButton != null)
            returnButton.interactable = false;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }
        if (rectTransform != null)
            rectTransform.localScale = new Vector3(shownScale.x, 0f, shownScale.z);

        Sequence sequence = DOTween.Sequence().SetTarget(this);
        if (rectTransform != null)
            sequence.Join(rectTransform.DOScaleY(shownScale.y, unfoldDuration).SetEase(unfoldEase));
        if (canvasGroup != null)
            sequence.Join(canvasGroup.DOFade(1f, fadeDuration));
        sequence.OnComplete(() =>
        {
            if (rectTransform != null)
                rectTransform.localScale = shownScale;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = true;
            }
            if (returnButton != null)
                returnButton.interactable = true;
        });
        showTween = sequence;
    }

    private void CacheReferences()
    {
        if (rectTransform == null)
        {
            rectTransform = transform as RectTransform;
            shownScale = rectTransform != null ? rectTransform.localScale : transform.localScale;
        }
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        if (titleText == null)
            titleText = UIManager.FindChildComponent<TMP_Text>(transform, "Title");
        if (bodyText == null)
            bodyText = UIManager.FindChildComponent<TMP_Text>(transform, "Body");
        if (returnButton == null)
            returnButton = UIManager.FindChildComponent<Button>(transform, "ReturnStartButton");
        if (buttonText == null && returnButton != null)
            buttonText = UIManager.FindChildComponent<TMP_Text>(returnButton.transform, "Text");

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

    private void OnDestroy()
    {
        showTween?.Kill(false);
    }
}
