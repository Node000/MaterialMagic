using System;
using System.Collections.Generic;

/// <summary>
/// Synchronous production event/rest rules. Advance commits one effect/discard/transition; choices pause it.
/// Immediate effects keep their source order, followed by force-discard, resolve-count increment, deferred effects.
/// A missing view never answers a choice. The caller may submit its stable IDs and resume this same session.
/// </summary>
public sealed class EventResolutionSession : IChoiceSession
{
    private readonly EventRuleServices services;
    private readonly string sessionId = ChoiceSessionIdentity.Next("event");
    private readonly bool rest;
    private readonly bool legacyChoice;
    private readonly List<MaterialModel> legacyTargets;
    private bool executing;
    private int phase;
    private int effectIndex;
    private int choiceIndex;
    private bool legacyApplied;
    private Func<List<ChoiceCandidate>, bool, ChoiceResult> answer;
    public PlayerState Owner { get; }
    public EventModel Event { get; }
    public EventOptionData Option { get; }
    public PendingChoice PendingChoice { get; private set; }
    public bool IsComplete { get; private set; }
    public bool NodeAdvanced { get; private set; }
    public bool PlayerDefeated => Owner.CurrentHealth <= 0;
    public EventResolutionStep LastStep { get; private set; }

    public EventResolutionSession(PlayerState owner, EventModel model, EventOptionData option, EventRuleServices services = null, bool isRest = false)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Event = model ?? throw new ArgumentNullException(nameof(model));
        Option = option;
        this.services = services ?? new EventRuleServices(owner);
        rest = isRest;
        legacyChoice = !HasEffects && option != null && (option.resultId == 100 || EventModel.CreateModifierForResult(option.resultId) != null);
        // Old UI discarded the hand before asking to select from that now-empty hand. Preserve the intended
        // hand domain by capturing identities before discard, rather than reproducing an unanswerable decision.
        legacyTargets = legacyChoice ? new List<MaterialModel>(owner.Hand) : null;
    }

    private bool HasEffects => Option?.effects != null && Option.effects.Length > 0;
    public static bool IsDeferred(EventRewardType type) => type == EventRewardType.GainMagic || type == EventRewardType.GainMagicModifier || type == EventRewardType.GainMaterialModifier;

    public EventResolutionStep Advance()
    {
        if (executing) return null;
        executing = true;
        try { using (Owner.Rules.Enter()) return AdvanceCore(); }
        finally { executing = false; }
    }

    private EventResolutionStep AdvanceCore()
    {
        if (IsComplete) return null;
        if (PendingChoice != null) return LastStep = new EventResolutionStep { Kind = EventResolutionStepKind.Choice };
        if (PlayerDefeated) { IsComplete = true; return LastStep = new EventResolutionStep { Kind = EventResolutionStepKind.Complete }; }
        while (true)
        {
            if (phase == 0)
            {
                if (rest) { phase = 1; continue; }
                if (!HasEffects)
                {
                    if (!legacyApplied && !legacyChoice && Option != null)
                    {
                        legacyApplied = true;
                        ChoiceResult receipt = Before(); Event.ResolveResult(Option.resultId, Owner); After(receipt);
                        return LastStep = new EventResolutionStep { Kind = EventResolutionStepKind.Effect, Result = receipt };
                    }
                }
                else
                {
                    while (effectIndex < Option.effects.Length)
                    {
                        EventEffectData effect = Option.effects[effectIndex++];
                        if (effect != null && !IsDeferred(effect.rewardType)) return Execute(effect);
                    }
                }
                phase = 1;
            }
            if (phase == 1)
            {
                phase = 2; effectIndex = 0;
                var removed = new List<MaterialModel>(); Owner.ForceDiscardAll(removed);
                return LastStep = new EventResolutionStep { Kind = EventResolutionStepKind.Discard, RemovedTemporaryCards = removed.AsReadOnly() };
            }
            if (phase == 2)
            {
                phase = 3;
                if (rest)
                {
                    if (Option != null && Option.resultId == EventRestRules.StudyResultId)
                        return OpenMagicModifier(null, 2);
                    if (Option != null && Option.resultId == EventRestRules.ArrowModifierResultId)
                        return OpenMaterialModifier(null, 2, 1, true);
                    return Execute(new EventEffectData { rewardType = EventRewardType.Heal, amount = Math.Max(1, (int)Math.Ceiling(Owner.MaxHealth * EventRestRules.HealRatio)) });
                }
                if (legacyChoice)
                    return OpenLegacyChoice();
                Event.MarkOptionResolved(Option);
            }
            if (phase == 3)
            {
                if (!rest && HasEffects)
                {
                    while (effectIndex < Option.effects.Length)
                    {
                        EventEffectData effect = Option.effects[effectIndex++];
                        if (effect != null && IsDeferred(effect.rewardType)) return Execute(effect);
                    }
                }
                phase = 4;
            }
            if (phase == 4)
            {
                NodeAdvanced = Option != null && Event.AdvanceToNextNode(Option);
                IsComplete = true;
                return LastStep = new EventResolutionStep { Kind = EventResolutionStepKind.Complete };
            }
        }
    }

    public void RunUntilChoiceOrComplete()
    {
        while (!IsComplete && PendingChoice == null) Advance();
    }

    public ChoiceResult SubmitChoice(ChoiceSubmission submission)
    {
        if (executing) return new ChoiceResult(ChoiceFailure.Busy);
        executing = true;
        try { using (Owner.Rules.Enter()) return SubmitChoiceCore(submission); }
        finally { executing = false; }
    }

    private ChoiceResult SubmitChoiceCore(ChoiceSubmission submission)
    {
        if (PendingChoice == null) return new ChoiceResult(ChoiceFailure.NoPendingChoice);
        ChoiceFailure failure = PendingChoice.Validate(submission, out List<ChoiceCandidate> selected);
        if (failure != ChoiceFailure.None) return new ChoiceResult(failure);
        return answer(selected, submission.Cancel);
    }

    private ChoiceResult Before() => new ChoiceResult { GoldBefore = Owner.Gold, HealthBefore = Owner.CurrentHealth };
    private ChoiceResult After(ChoiceResult receipt)
    {
        receipt.GoldAfter = Owner.Gold; receipt.HealthAfter = Owner.CurrentHealth; receipt.ResultingMagicCount = Owner.MagicBook.Count;
        return receipt;
    }
    private ChoiceResult FinishChoice(ChoiceResult result)
    {
        PendingChoice = null; answer = null;
        return After(result);
    }
    private static int Amount(EventEffectData effect, int fallback) => effect.amount != 0 ? effect.amount : fallback;
    private static int Count(EventEffectData effect, int fallback) => effect.count > 0 ? effect.count : fallback;
    private int ChoiceCount(EventEffectData effect, int fallback) => effect.choiceCount > 0 ? effect.choiceCount : Option != null && Option.choiceCount > 0 ? Option.choiceCount : fallback;
    private MaterialEnum RandomBasic() => (MaterialEnum)services.NextRandomInt((int)MaterialEnum.Fire, (int)MaterialEnum.Earth + 1);
    private bool Removable(MaterialModel card) => card != null && Owner.Deck.Contains(card) && !PlayerState.IsDeckPlaceholderMaterial(card);
    private bool ModifierTarget(MaterialModel card) => card != null && Owner.Deck.Contains(card) && card.material != MaterialEnum.None;

    private EventResolutionStep Execute(EventEffectData effect)
    {
        ChoiceResult receipt = Before();
        var acquired = new List<MagicModel>();
        switch (effect.rewardType)
        {
            case EventRewardType.Heal: Owner.Heal(Amount(effect, 10)); break;
            case EventRewardType.LoseHealth: Owner.TakeDirectDamage(Amount(effect, 1) + Event.GetOptionResolveCount(Option) * effect.escalatePerUse); break;
            case EventRewardType.GainGold:
                int gold = Amount(effect, 1);
                if (gold > 0) Owner.AddGold(gold, playFeedback: false);
                break;
            case EventRewardType.GainMagic: return OpenMagicReward();
            case EventRewardType.GainMagicModifier: return OpenMagicModifier(effect.modifierId, ChoiceCount(effect, 2));
            case EventRewardType.GainMaterialModifier: return OpenMaterialModifier(effect.modifierId, 1, ChoiceCount(effect, 1), false);
            case EventRewardType.IncreaseMaxHealth: Owner.IncreaseMaxHealth(Amount(effect, 5)); break;
            case EventRewardType.GainMaterial: AddMaterials(effect.material != MaterialEnum.None ? effect.material : RandomBasic(), Count(effect, 1), effect.modifierId); break;
            case EventRewardType.GainRandomMaterial:
                for (int i = 0; i < Count(effect, 1); i++) AddMaterials(RandomBasic(), 1, null);
                break;
            case EventRewardType.GainSameRandomMaterials: AddMaterials(RandomBasic(), Count(effect, 1), null); break;
            case EventRewardType.IncreaseDrawCount: Owner.DrawCount += Amount(effect, 1); break;
            case EventRewardType.RemoveMaterial: return OpenRemoval(ChoiceCount(effect, 1));
            case EventRewardType.GainNextBattleStartShield: Owner.AddBuff(BuffEnum.PreparedShield, Amount(effect, 1)); break;
            case EventRewardType.SpendAllGold: if (Owner.Gold > 0) Owner.AddGold(-Owner.Gold); break;
            case EventRewardType.RandomizeDeckBasicMaterials:
                foreach (MaterialModel card in Owner.Deck) if (BasicTarget(card)) card.material = RandomBasic();
                break;
            case EventRewardType.GainRandomSyntaxMaterial:
                string[] syntax = { "period_arrow", "return_arrow", "pack_arrow" };
                AddMaterials(MaterialEnum.Wild, 1, syntax[services.NextRandomInt(0, syntax.Length)]); break;
            case EventRewardType.IncreasePlayLimit: Owner.MaxPlayCount = Math.Max(1, Owner.MaxPlayCount + Amount(effect, 1)); break;
            case EventRewardType.ApplyMaterialModifierToDeck: ApplyDeckModifier(effect); break;
            case EventRewardType.RandomizeRandomMaterials:
                var basics = Owner.Deck.FindAll(BasicTarget);
                for (int i = 0, count = Math.Min(Count(effect, 1), basics.Count); i < count; i++) TakeRandom(basics).material = RandomBasic();
                break;
            case EventRewardType.DecreaseMaxHealth: if (Amount(effect, 1) > 0) Owner.AdjustMaxHealthOnly(-Amount(effect, 1)); break;
            case EventRewardType.LoseGold: if (Amount(effect, 1) > 0 && Owner.Gold > 0) Owner.AddGold(-Amount(effect, 1)); break;
            case EventRewardType.GainMagicById:
                // Fixed packs fill only actual empty slots and never replace owned items.
                for (int i = 0; effect.magicIds != null && i < effect.magicIds.Length; i++)
                {
                    int slot = RewardSession.FirstFreeSlot(Owner, services.MagicSlotCapacity);
                    if (slot < 0) break;
                    MagicData data = services.FindMagic(effect.magicIds[i]);
                    if (data == null) continue;
                    MagicModel magic = MagicFactory.Create(data, slot);
                    if (magic == null) continue;
                    Owner.SetMagicAtSlot(magic, slot);
                    acquired.Add(magic);
                }
                break;
            // None/UpgradeMaterial/RemovePollution/GainRelic had no production switch branch; remain no-op.
        }
        return LastStep = new EventResolutionStep { Kind = EventResolutionStepKind.Effect, Effect = effect, Result = After(receipt), AcquiredMagics = acquired.AsReadOnly() };
    }

    private void AddMaterials(MaterialEnum material, int count, string modifierId)
    {
        if (material == MaterialEnum.None || count <= 0) return;
        MaterialModifierData data = services.FindMaterialModifier(modifierId);
        for (int i = 0; i < count; i++)
        {
            MaterialModel card = Owner.AddDeckMaterial(material);
            MaterialModifierModel modifier = MaterialModifierFactory.Create(data);
            if (card != null && modifier != null) card.AddModifier(modifier);
        }
    }
    private static bool BasicTarget(MaterialModel card) => card != null && card.material != MaterialEnum.None && card.material != MaterialEnum.Wild;
    private T TakeRandom<T>(List<T> candidates)
    {
        int index = services.NextRandomInt(0, candidates.Count); T target = candidates[index];
        candidates[index] = candidates[candidates.Count - 1]; candidates.RemoveAt(candidates.Count - 1); return target;
    }
    private void ApplyDeckModifier(EventEffectData effect)
    {
        MaterialModifierData data = services.FindMaterialModifier(effect.modifierId);
        MaterialModifierModel sample = MaterialModifierFactory.Create(data);
        if (sample == null) return;
        var candidates = Owner.Deck.FindAll(card => ModifierTarget(card) && card.CanAddModifier(sample));
        int count = effect.percent > 0 ? (int)Math.Ceiling(candidates.Count * Math.Min(100, Math.Max(1, effect.percent)) / 100f) : effect.count > 0 ? effect.count : candidates.Count;
        for (int i = 0, targetCount = Math.Min(count, candidates.Count); i < targetCount; i++) TakeRandom(candidates).AddModifier(MaterialModifierFactory.Create(data));
    }

    private EventResolutionStep ChoiceStep() => LastStep = new EventResolutionStep { Kind = EventResolutionStepKind.Choice };
    private PendingChoice MakeChoice(ChoiceKind kind, List<ChoiceCandidate> candidates, int count, bool cancel)
    {
        int bounded = Math.Min(Math.Max(0, count), candidates.Count);
        return new PendingChoice(sessionId + ":" + Event.Id + ":" + Event.CurrentNode?.id + ":" + Option?.id + ":" + choiceIndex++, kind, bounded, bounded, cancel, candidates);
    }
    private EventResolutionStep OpenMagicReward()
    {
        RewardSession reward = RewardSession.MagicOnly(Owner, services.MagicChoices(3), services.MagicSlotCapacity,
            Event.Id + ":magic:" + choiceIndex++);
        PendingChoice = reward.PendingChoice;
        answer = (selected, cancel) =>
        {
            var ids = new string[selected.Count]; for (int i = 0; i < ids.Length; i++) ids[i] = selected[i].Id;
            ChoiceResult result = reward.SubmitChoice(new ChoiceSubmission(PendingChoice.Id, cancel, ids));
            if (result.Accepted) { PendingChoice = null; answer = null; }
            return result;
        };
        return ChoiceStep();
    }
    private EventResolutionStep OpenRemoval(int count)
    {
        var candidates = new List<ChoiceCandidate>();
        foreach (MaterialModel card in Owner.Deck) if (Removable(card)) candidates.Add(new ChoiceCandidate("material:" + card.instanceId, material: card));
        if (candidates.Count == 0) return LastStep = new EventResolutionStep { Kind = EventResolutionStepKind.Effect, Result = After(Before()) };
        PendingChoice = MakeChoice(ChoiceKind.RemoveMaterial, candidates, count, false);
        answer = (selected, cancel) =>
        {
            foreach (ChoiceCandidate target in selected) if (!Removable(target.Material)) return new ChoiceResult(ChoiceFailure.TargetUnavailable);
            ChoiceResult result = Before(); foreach (ChoiceCandidate target in selected) Owner.RemoveCardEverywhere(target.Material);
            return FinishChoice(result);
        };
        return ChoiceStep();
    }
    private EventResolutionStep OpenLegacyChoice()
    {
        var candidates = new List<ChoiceCandidate>();
        foreach (MaterialModel card in legacyTargets) if (card != null && (Owner.Deck.Contains(card) || Owner.DiscardPile.Contains(card) || Owner.ConsumedPile.Contains(card))) candidates.Add(new ChoiceCandidate("material:" + card.instanceId, material: card));
        if (candidates.Count == 0) return LastStep = new EventResolutionStep { Kind = EventResolutionStepKind.Effect, Result = After(Before()) };
        PendingChoice = MakeChoice(ChoiceKind.LegacyMaterial, candidates, Option.choiceCount > 0 ? Option.choiceCount : 1, false);
        answer = (selected, cancel) =>
        {
            foreach (ChoiceCandidate target in selected)
                if (!Owner.Deck.Contains(target.Material) && !Owner.DiscardPile.Contains(target.Material) && !Owner.ConsumedPile.Contains(target.Material)) return new ChoiceResult(ChoiceFailure.TargetUnavailable);
            ChoiceResult result = Before();
            foreach (ChoiceCandidate target in selected)
                if (Option.resultId == 100) Owner.ConsumeCardForBattle(target.Material);
                else target.Material.AddModifier(EventModel.CreateModifierForResult(Option.resultId));
            return FinishChoice(result);
        };
        return ChoiceStep();
    }

    private EventResolutionStep OpenMagicModifier(string specifiedId, int count)
    {
        IReadOnlyList<MagicModifierData> pool;
        MagicModifierData specified = services.FindMagicModifier(specifiedId);
        pool = specified != null ? new[] { specified } : services.MagicModifierChoices(count);
        var candidates = new List<ChoiceCandidate>();
        for (int i = 0; pool != null && i < pool.Count; i++)
        {
            MagicModifierData data = pool[i]; MagicModifierModel sample = MagicModifierFactory.Create(data);
            if (data != null && sample != null && Owner.MagicBook.Exists(magic => magic != null && magic.CanAddModifier(sample)))
                candidates.Add(new ChoiceCandidate("magic_modifier:" + i + ":" + data.id, magicModifier: data));
        }
        if (candidates.Count == 0) return LastStep = new EventResolutionStep { Kind = EventResolutionStepKind.Effect, Result = After(Before()) };
        PendingChoice choices = MakeChoice(ChoiceKind.MagicModifier, candidates, 1, true);
        Func<List<ChoiceCandidate>, bool, ChoiceResult> chooseModifier = null;
        chooseModifier = (selected, cancel) =>
        {
            if (cancel) { ChoiceResult cancelled = Before(); cancelled.Cancelled = true; return FinishChoice(cancelled); }
            MagicModifierData data = selected[0].MagicModifier; MagicModifierModel sample = MagicModifierFactory.Create(data);
            var targets = new List<ChoiceCandidate>();
            foreach (MagicModel magic in Owner.MagicBook) if (magic != null && magic.CanAddModifier(sample)) targets.Add(new ChoiceCandidate("magic_slot:" + magic.SlotIndex + ":" + magic.Id, magic: magic, magicModifier: data));
            if (targets.Count == 0) return new ChoiceResult(ChoiceFailure.TargetUnavailable);
            PendingChoice = MakeChoice(ChoiceKind.MagicTarget, targets, 1, true);
            answer = (selection, back) =>
            {
                if (back) { PendingChoice = choices; answer = chooseModifier; ChoiceResult cancelled = Before(); cancelled.Cancelled = true; return After(cancelled); }
                MagicModel target = selection[0].Magic;
                if (!Owner.MagicBook.Contains(target) || !target.CanAddModifier(MagicModifierFactory.Create(data))) return new ChoiceResult(ChoiceFailure.TargetUnavailable);
                ChoiceResult result = Before();
                if (!target.AddModifier(MagicModifierFactory.Create(data))) return new ChoiceResult(ChoiceFailure.TargetUnavailable);
                return FinishChoice(result);
            };
            return After(Before());
        };
        PendingChoice = choices; answer = chooseModifier;
        return ChoiceStep();
    }

    private EventResolutionStep OpenMaterialModifier(string specifiedId, int choiceCount, int targetCount, bool random)
    {
        IReadOnlyList<MaterialModifierData> pool = random ? services.MaterialModifierChoices(choiceCount) : new[] { services.FindMaterialModifier(specifiedId) };
        var candidates = new List<ChoiceCandidate>();
        for (int i = 0; pool != null && i < pool.Count; i++)
        {
            MaterialModifierModel sample = MaterialModifierFactory.Create(pool[i]);
            if (sample != null && Owner.Deck.Exists(card => ModifierTarget(card) && card.CanAddModifier(sample)))
                candidates.Add(new ChoiceCandidate("material_modifier:" + i + ":" + pool[i].id, materialModifier: pool[i]));
        }
        if (candidates.Count == 0) return LastStep = new EventResolutionStep { Kind = EventResolutionStepKind.Effect, Result = After(Before()) };
        PendingChoice choices = MakeChoice(ChoiceKind.MaterialModifier, candidates, 1, true);
        Func<List<ChoiceCandidate>, bool, ChoiceResult> chooseModifier = null;
        chooseModifier = (selected, cancel) =>
        {
            if (cancel) { ChoiceResult cancelled = Before(); cancelled.Cancelled = true; return FinishChoice(cancelled); }
            MaterialModifierData data = selected[0].MaterialModifier;
            MaterialModifierModel sample = MaterialModifierFactory.Create(data);
            var targets = new List<ChoiceCandidate>();
            foreach (MaterialModel card in Owner.Deck)
                if (ModifierTarget(card) && card.CanAddModifier(sample)) targets.Add(new ChoiceCandidate("material:" + card.instanceId, material: card));
            if (targets.Count == 0) return new ChoiceResult(ChoiceFailure.TargetUnavailable);
            PendingChoice = MakeChoice(ChoiceKind.MaterialTarget, targets, Math.Max(1, targetCount), true);
            answer = (selection, back) =>
            {
                if (back) { PendingChoice = choices; answer = chooseModifier; ChoiceResult cancelled = Before(); cancelled.Cancelled = true; return After(cancelled); }
                foreach (ChoiceCandidate target in selection)
                    if (!ModifierTarget(target.Material) || !target.Material.CanAddModifier(sample)) return new ChoiceResult(ChoiceFailure.TargetUnavailable);
                ChoiceResult result = Before(); foreach (ChoiceCandidate target in selection) target.Material.AddModifier(MaterialModifierFactory.Create(data));
                return FinishChoice(result);
            };
            return After(Before());
        };
        PendingChoice = choices; answer = chooseModifier;
        return ChoiceStep();
    }
}
