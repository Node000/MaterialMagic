using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ShaderEffectTestController : MonoBehaviour
{
    public enum EffectType
    {
        PixelParticleDissolve,
        SpatialRift,
        FluidInk,
        SquareParticleBurst
    }

    [SerializeField] private EffectType effectType;
    [SerializeField] private Material effectMaterial;
    [SerializeField] private Slider primarySlider;
    [SerializeField] private Slider secondarySlider;
    [SerializeField] private TMP_Text primaryValueText;
    [SerializeField] private TMP_Text secondaryValueText;
    [SerializeField] private ParticleSystem burstParticles;
    [SerializeField, Min(0.01f)] private float dissolveTransitionSpeed = 0.8f;

    private float dissolveTarget;

    private void Awake()
    {
        if (primarySlider != null)
            primarySlider.onValueChanged.AddListener(ApplyPrimaryValue);
        if (secondarySlider != null)
            secondarySlider.onValueChanged.AddListener(ApplySecondaryValue);
        ApplyPrimaryValue(primarySlider != null ? primarySlider.value : 0f);
        ApplySecondaryValue(secondarySlider != null ? secondarySlider.value : 0f);
    }

    private void OnDestroy()
    {
        if (primarySlider != null)
            primarySlider.onValueChanged.RemoveListener(ApplyPrimaryValue);
        if (secondarySlider != null)
            secondarySlider.onValueChanged.RemoveListener(ApplySecondaryValue);
    }

    private void Update()
    {
        if (effectType != EffectType.PixelParticleDissolve || effectMaterial == null)
            return;

        float progress = Mathf.MoveTowards(effectMaterial.GetFloat("_DissolveProgress"), dissolveTarget, dissolveTransitionSpeed * Time.unscaledDeltaTime);
        effectMaterial.SetFloat("_DissolveProgress", progress);
    }

    public void Gather()
    {
        dissolveTarget = 0f;
    }

    public void Dissipate()
    {
        dissolveTarget = 1f;
    }

    public void ReplayBurst()
    {
        if (burstParticles == null)
            return;

        burstParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        burstParticles.Play(true);
    }

    private void ApplyPrimaryValue(float value)
    {
        if (effectMaterial == null)
            return;

        switch (effectType)
        {
            case EffectType.SpatialRift:
                effectMaterial.SetFloat("_RiftSize", value);
                SetValueText(primaryValueText, value, "0.00");
                break;
            case EffectType.FluidInk:
                effectMaterial.SetFloat("_ColorCount", Mathf.Round(value));
                SetValueText(primaryValueText, Mathf.Round(value), "0");
                break;
        }
    }

    private void ApplySecondaryValue(float value)
    {
        if (effectMaterial == null)
            return;

        switch (effectType)
        {
            case EffectType.SpatialRift:
                effectMaterial.SetFloat("_RiftDensity", value);
                SetValueText(secondaryValueText, value, "0");
                break;
            case EffectType.FluidInk:
                effectMaterial.SetFloat("_FlowSpeed", value);
                SetValueText(secondaryValueText, value, "0.00");
                break;
        }
    }

    private static void SetValueText(TMP_Text valueText, float value, string format)
    {
        if (valueText != null)
            valueText.text = value.ToString(format);
    }
}
