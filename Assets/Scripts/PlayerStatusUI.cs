using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if !MATERIALMAGIC_STANDALONE
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "PlayerStatusUI")]
#endif
public class PlayerStatusUI : MonoBehaviour
{
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private TMP_Text shieldText;
    [SerializeField] private Image healthFill;
    [SerializeField] private RectTransform buffRoot;

    private HealthBarUI healthBar;
    private HandSystemUI owner;

    public RectTransform BuffRoot => buffRoot;
    public TMP_Text HealthText => healthText;
    public TMP_Text ShieldText => shieldText;
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
        healthBar.Initialize(healthText, healthFill, shieldText, playerState.CurrentHealth);
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

    public void Refresh(BattleStateSnapshot snapshot, bool instant)
    {
        if (snapshot == null) return;
        CacheReferences();
        if (healthBar == null)
        {
            healthBar = gameObject.GetComponent<HealthBarUI>() ?? gameObject.AddComponent<HealthBarUI>();
            healthBar.Initialize(healthText, healthFill, shieldText, snapshot.PlayerHealth);
        }
        healthBar.UpdateValue(snapshot.PlayerHealth, snapshot.PlayerMaxHealth, snapshot.PlayerShield, instant);
    }

    private void CacheReferences()
    {
        if (healthText == null)
            healthText = UIManager.FindChildComponent<TMP_Text>(transform, "HealthText");
        if (shieldText == null)
            shieldText = UIManager.FindChildComponent<TMP_Text>(transform, "ShieldText");
        if (healthFill == null)
            healthFill = UIManager.FindChildComponent<Image>(transform, "HealthBarBack/HealthFill");
        if (buffRoot == null)
            buffRoot = UIManager.FindChildRect(transform, "BuffRoot");
    }
}
