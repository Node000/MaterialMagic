using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class CastParticleEffectPlayer : CastParticleEffectBase, ISpellCastMultiTargetImpactEffect
{
    public new void PlayMaterialFill(RectTransform from, RectTransform magicView, MaterialEnum material)
    {
        Sprite icon = MaterialCardView.GetMaterialIcon(material);
        PlayBurst(from, magicView, 1, Color.white, icon, 18f * 2.5f);
    }

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

    public void PlayCast(MagicModel magic, RectTransform from, IReadOnlyList<RectTransform> targets, SpellEffectTarget targetType, Action onImpact)
    {
        if (magic == null || targets == null)
            return;

        int lastTargetIndex = -1;
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] != null)
                lastTargetIndex = i;
        }

        if (lastTargetIndex < 0)
            return;

        GetCastVisual(magic, out Sprite icon, out Color color, out float visualSize);
        for (int i = 0; i < targets.Count; i++)
        {
            RectTransform target = targets[i];
            if (target != null)
                PlayBurst(from, target, 1, color, icon, visualSize, i == lastTargetIndex ? onImpact : null);
        }
    }
}
