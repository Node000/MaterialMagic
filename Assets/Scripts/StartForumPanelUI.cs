using UnityEngine;
using UnityEngine.UI;

public class StartForumPanelUI : MonoBehaviour
{
    [SerializeField] private Button closeButton;

    private bool closeButtonBound;

    public bool IsShowing => gameObject.activeSelf;

    private void Awake()
    {
        ResolveReferences();
        BindCloseButton();
    }

    private void OnDestroy()
    {
        if (closeButton != null && closeButtonBound)
            closeButton.onClick.RemoveListener(Hide);
    }

    public void Show()
    {
        ResolveReferences();
        BindCloseButton();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    public void Hide()
    {
        if (!gameObject.activeSelf)
            return;

        gameObject.SetActive(false);
    }

    public bool Contains(Transform hit)
    {
        return hit != null && hit.IsChildOf(transform);
    }

    private void ResolveReferences()
    {
        // closeButton 走 Inspector 绑定：美术已统一停用窗口标题栏按钮，不再按路径/名字查找。
        if (closeButton == null)
            closeButton = GetComponentInChildren<Button>(true);
    }

    private void BindCloseButton()
    {
        if (closeButton == null || closeButtonBound)
            return;

        closeButton.onClick.AddListener(Hide);
        closeButtonBound = true;
    }
}
