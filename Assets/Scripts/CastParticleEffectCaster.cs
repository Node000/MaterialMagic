using System;
using UnityEngine;

public sealed class CastParticleEffectCaster : CastParticleEffectBase, ISpellCastImpactEffect
{
    public void PlayCast(MagicModel magic, RectTransform from, RectTransform target, SpellEffectTarget targetType)
    {
        PlayCast(magic, from, target, targetType, null);
    }

    public void PlayCast(MagicModel magic, RectTransform from, RectTransform target, SpellEffectTarget targetType, Action onImpact)
    {
        if (magic == null)
            return;

        GetCastVisual(magic, out Sprite icon, out Color color, out float visualSize);
        PlayBurst(from, target, 1, color, icon, visualSize, onImpact);
    }
}
