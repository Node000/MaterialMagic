using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class PlayerStatusUI : MonoBehaviour
{
    [SerializeField] private Text healthText;
    [SerializeField] private Image healthFill;
    [SerializeField] private RectTransform buffRoot;

    private HealthBarUI healthBar;
    private HandSystemUI owner;

    public RectTransform BuffRoot => buffRoot;
    public Text HealthText => healthText;
    public Image HealthFill => healthFill;

    public void Initialize(HandSystemUI owner)
    {
        this.owner = owner;
        CacheReferences();
        if (owner != null && owner.PlayerState != null)
            Setup(owner.PlayerState, true);
    }

    public void Setup(PlayerState playerState, bool instant)
    {
        CacheReferences();
        if (healthBar == null)
            healthBar = gameObject.GetComponent<HealthBarUI>() ?? gameObject.AddComponent<HealthBarUI>();
        healthBar.Initialize(healthText, healthFill, 72f, playerState.CurrentHealth);
        healthBar.UpdateValue(playerState.CurrentHealth, playerState.MaxHealth, playerState.Shield, instant);
    }

    public void Refresh(PlayerState playerState, bool instant)
    {
        if (playerState == null)
            return;

        CacheReferences();
        if (healthBar == null)
            Setup(playerState, true);
        healthBar.UpdateValue(playerState.CurrentHealth, playerState.MaxHealth, playerState.Shield, instant);
    }

    private void CacheReferences()
    {
        if (healthText == null)
            healthText = UIManager.FindChildComponent<Text>(transform, "HealthText");
        if (healthFill == null)
            healthFill = UIManager.FindChildComponent<Image>(transform, "HealthBarBack/HealthFill");
        if (buffRoot == null)
            buffRoot = UIManager.FindChildRect(transform, "BuffRoot");
    }
}
