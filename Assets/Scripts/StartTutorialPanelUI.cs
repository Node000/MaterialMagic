using System;
using UnityEngine;
using UnityEngine.UI;

public class StartTutorialPanelUI : MonoBehaviour
{
    [SerializeField] private Button closeButton;

    private bool closeButtonBound;

    public bool IsShowing => gameObject.activeSelf;
    public event Action Hidden;

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
        Hidden?.Invoke();
    }

    public bool Contains(Transform hit)
    {
        return hit != null && hit.IsChildOf(transform);
    }

    private void ResolveReferences()
    {
        if (closeButton == null)
            closeButton = transform.Find("PopupDragonWindow/Frame/TitleBar/Close")?.GetComponent<Button>();
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
