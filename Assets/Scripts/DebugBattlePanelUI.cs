using UnityEngine;
using UnityEngine.UI;

public class DebugBattlePanelUI : MonoBehaviour
{
    [SerializeField] private HandSystemUI handSystem;
    [SerializeField] private Button damageButton;
    [SerializeField] private Button healButton;
    [SerializeField] private Button shieldButton;

    private const int Amount = 10;

    private void Awake()
    {
        if (handSystem == null)
            handSystem = GetComponentInParent<HandSystemUI>();

        damageButton?.onClick.AddListener(DealDamage);
        healButton?.onClick.AddListener(HealPlayer);
        shieldButton?.onClick.AddListener(GainShield);
    }

    private void OnDestroy()
    {
        damageButton?.onClick.RemoveListener(DealDamage);
        healButton?.onClick.RemoveListener(HealPlayer);
        shieldButton?.onClick.RemoveListener(GainShield);
    }

    private void DealDamage()
    {
        handSystem?.DebugDealDamageToTarget(Amount);
    }

    private void HealPlayer()
    {
        handSystem?.DebugHealPlayer(Amount);
    }

    private void GainShield()
    {
        handSystem?.DebugGainPlayerShield(Amount);
    }
}
