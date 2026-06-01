using System;
using UnityEngine;

public enum SpellEffectTarget
{
    Enemy,
    Player
}

public interface ISpellCastEffect
{
    void PlayMaterialFill(RectTransform from, RectTransform magicView, MaterialEnum material);
    void PlayCast(MagicModel magic, RectTransform from, RectTransform target, SpellEffectTarget targetType);
}

public interface ISpellCastImpactEffect : ISpellCastEffect
{
    void PlayCast(MagicModel magic, RectTransform from, RectTransform target, SpellEffectTarget targetType, Action onImpact);
}
