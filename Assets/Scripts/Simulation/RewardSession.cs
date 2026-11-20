using System;
using System.Collections.Generic;

public class BattleRewardChoices
{
    public int GoldAmount;
    public MagicData Magic;
    public RewardArrowOption Arrow;
}

public class RewardArrowOption
{
    public MaterialEnum material;
    public MaterialModifierData modifierData;
    public bool HasModifier => modifierData != null;
}

/// <summary>One reward decision. Opening or hiding a view never generates candidates or grants a reward.</summary>
public sealed class RewardSession : IChoiceSession
{
    private bool submitting;
    public PlayerState Owner { get; }
    public int MagicSlotCapacity { get; }
    public BattleRewardChoices BattleChoices { get; }
    public PendingChoice PendingChoice { get; private set; }
    public bool IsComplete { get; private set; }
    public ChoiceResult LastResult { get; private set; }

    public RewardSession(PlayerState owner, BattleRewardChoices choices, int magicSlotCapacity, string id = "battle_reward")
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        MagicSlotCapacity = Math.Max(0, magicSlotCapacity);
        BattleChoices = new BattleRewardChoices
        {
            GoldAmount = Math.Max(0, choices != null ? choices.GoldAmount : 0),
            Magic = choices?.Magic,
            Arrow = choices?.Arrow == null ? null : new RewardArrowOption { material = choices.Arrow.material, modifierData = choices.Arrow.modifierData }
        };
        var candidates = new List<ChoiceCandidate>();
        if (BattleChoices.GoldAmount > 0) candidates.Add(new ChoiceCandidate("gold", amount: BattleChoices.GoldAmount));
        if (BattleChoices.Magic != null) candidates.Add(new ChoiceCandidate("magic:" + BattleChoices.Magic.numericId + ":" + BattleChoices.Magic.id, magicData: BattleChoices.Magic));
        if (BattleChoices.Arrow != null && BattleChoices.Arrow.material != MaterialEnum.None)
            candidates.Add(new ChoiceCandidate("arrow:" + (int)BattleChoices.Arrow.material + ":" + BattleChoices.Arrow.modifierData?.id, arrow: BattleChoices.Arrow));
        PendingChoice = new PendingChoice(ChoiceSessionIdentity.Next(id), ChoiceKind.BattleReward, candidates.Count > 0 ? 1 : 0, candidates.Count > 0 ? 1 : 0, true, candidates);
    }

    private RewardSession(PlayerState owner, IReadOnlyList<MagicData> choices, int capacity, string id)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        MagicSlotCapacity = Math.Max(0, capacity);
        var candidates = new List<ChoiceCandidate>();
        for (int i = 0; choices != null && i < choices.Count; i++)
            if (choices[i] != null) candidates.Add(new ChoiceCandidate("magic:" + i + ":" + choices[i].numericId + ":" + choices[i].id, magicData: choices[i]));
        PendingChoice = new PendingChoice(ChoiceSessionIdentity.Next(id), ChoiceKind.MagicReward, candidates.Count > 0 ? 1 : 0, candidates.Count > 0 ? 1 : 0, true, candidates);
    }

    public static RewardSession MagicOnly(PlayerState owner, IReadOnlyList<MagicData> choices, int capacity, string id = "magic_reward")
        => new RewardSession(owner, choices, capacity, id);

    public ChoiceResult SubmitChoice(ChoiceSubmission submission)
    {
        if (submitting) return new ChoiceResult(ChoiceFailure.Busy);
        submitting = true;
        try { using (Owner.Rules.Enter()) return SubmitChoiceCore(submission); }
        finally { submitting = false; }
    }

    private ChoiceResult SubmitChoiceCore(ChoiceSubmission submission)
    {
        if (PendingChoice == null || IsComplete) return new ChoiceResult(ChoiceFailure.NoPendingChoice);
        ChoiceFailure failure = PendingChoice.Validate(submission, out List<ChoiceCandidate> selected);
        if (failure != ChoiceFailure.None) return new ChoiceResult(failure);
        var result = new ChoiceResult { Cancelled = submission.Cancel, GoldBefore = Owner.Gold, HealthBefore = Owner.CurrentHealth };
        if (!submission.Cancel && selected.Count > 0)
        {
            ChoiceCandidate candidate = selected[0];
            if (candidate.MagicData != null)
            {
                int slot = FirstFreeSlot(Owner, MagicSlotCapacity);
                if (slot < 0) return new ChoiceResult(ChoiceFailure.NoMagicSlot);
                MagicModel magic = MagicFactory.Create(candidate.MagicData, slot);
                if (magic == null) return new ChoiceResult(ChoiceFailure.InvalidReward);
                Owner.SetMagicAtSlot(magic, slot);
                result.MagicSlotIndex = slot; result.AcquiredMagic = magic;
            }
            else if (candidate.Arrow != null)
                result.AddedMaterial = Owner.AddDeckMaterial(candidate.Arrow.material, MaterialModifierFactory.Create(candidate.Arrow.modifierData));
            else if (candidate.Amount > 0)
                // Battle gold is already difficulty-adjusted. PlayerState still applies relic gain exactly once.
                Owner.AddGold(candidate.Amount, false, playFeedback: false);
        }
        result.GoldAfter = Owner.Gold; result.HealthAfter = Owner.CurrentHealth;
        result.ResultingMagicCount = Owner.MagicBook.Count;
        PendingChoice = null; IsComplete = true; LastResult = result;
        return result;
    }

    public static int FirstFreeSlot(PlayerState owner, int capacity)
    {
        for (int i = 0; i < capacity; i++) if (owner.GetMagicAtSlot(i) == null) return i;
        return -1;
    }
}
