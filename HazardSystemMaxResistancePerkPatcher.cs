using System;
using System.Linq;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache.Internals.Implementations;
using Mutagen.Bethesda.Starfield;
using Noggog;

public class HazardSystemMaxResistancePerkPatcher
{
    private IMagicEffectGetter removeResistanceCapSpellEffect;
    private readonly HazardSystem hazardSystem;
    private readonly StarfieldMod outputMod;
    private readonly ImmutableLoadOrderLinkCache<IStarfieldMod, IStarfieldModGetter> baseGameLinkCache;

    private HazardSystemMaxResistancePerkPatcher(HazardSystem hazardSystem, StarfieldMod outputMod, ImmutableLoadOrderLinkCache<IStarfieldMod, IStarfieldModGetter> baseGameLinkCache)
    {
        this.hazardSystem = hazardSystem;
        this.outputMod = outputMod;
        this.baseGameLinkCache = baseGameLinkCache;
    }

    public static void WritePatch(HazardSystem hazardSystem, StarfieldMod outputMod, ImmutableLoadOrderLinkCache<IStarfieldMod, IStarfieldModGetter> baseGameLinkCache)
    {
        var patcher = new HazardSystemMaxResistancePerkPatcher(hazardSystem, outputMod, baseGameLinkCache);
        patcher.PatchInternal();
    }
    private void PatchInternal()
    {
        PatchConditioningPerk();
        foreach(string hazardType in hazardSystem.HazardTypes)
        {
            var debuffAbility = AddMaxResistanceAbility(hazardType);
            PatchHazardSoakMax(hazardType);
            Console.WriteLine($"Added debuff ability for {hazardType}: {debuffAbility.FormKey}");
        }
    }
    private void PatchConditioningPerk()
    {
        // Dictionary for quick access to our resistance boost effects
        var resistances = hazardSystem.HazardTypes.ToDictionary(k => k, CreateResistanceBoostEffect);
        var maxEffectUnlock = CreateUnlockMaxResistanceEffect();

        var perk  = outputMod.Perks.GetOrAddAsOverride(baseGameLinkCache.Resolve<IPerkGetter>("Skill_EnvironmentalConditioning"));
        perk.Ranks.Clear();
        perk.Ranks.AddRange(new [] {
            CreateConditioningRank("Gain 10 resistance to thermal and radiation damage.", 1, resistances["thermal"], resistances["radiation"]),
            CreateConditioningRank("Gain 10 resistance to airborne and corrosive damage.", 1, resistances["thermal"], resistances["radiation"], resistances["airborne"], resistances["corrosive"]),
            CreateConditioningRank("Gain 10 maximum resistance to all environmental damage.", 1, resistances["thermal"], resistances["radiation"], resistances["airborne"], resistances["corrosive"], maxEffectUnlock),
        });

        removeResistanceCapSpellEffect = maxEffectUnlock;
    }
    private IMagicEffect CreateUnlockMaxResistanceEffect()
    {
        var mf = outputMod.MagicEffects.AddNew("Resist_Unlock_Max_Resistance_Marker");
        mf.CastType = CastType.ConstantEffect;
        mf.ActorValue1.SetToNull();
        mf.Name = "Maximum resistance increased to 95%";
        mf.Description = "You have mastered the elements and are able to push your spacesuit well beyond the manufactorer's specs.";
        mf.Archetype = new MagicEffectArchetype()
        {
            Type = MagicEffectArchetype.TypeEnum.ValueModifier
        };
        mf.Flags = MagicEffect.Flag.NoArea | MagicEffect.Flag.NoHitEffect;
        return mf;
    }
    private IMagicEffect CreateResistanceBoostEffect(string hazardType)
    {
        var mf = outputMod.MagicEffects.AddNew("Resist_Boost_" + hazardType);
        mf.CastType = CastType.ConstantEffect;
        mf.ActorValue1.SetTo(hazardSystem.GetResistanceAV(hazardType));
        mf.Archetype = new MagicEffectArchetype()
        {
            Type = MagicEffectArchetype.TypeEnum.ValueModifier
        };
        mf.ResistValue.SetToNull();
        mf.Flags = MagicEffect.Flag.NoArea | MagicEffect.Flag.NoHitEffect | MagicEffect.Flag.Recover | MagicEffect.Flag.HideInUI;

        return mf;
    }

    private PerkRank CreateConditioningRank(string description, int rankId, params IMagicEffect[] perkEffects)
    {
        var perkRank = new PerkRank()
        {
            Description = description,
        }; 

        perkRank.Effects.Add(new PerkAbilityEffect()
        {
            Ability = new FormLink<ISpellGetter>(CreatePerkRankSpell(rankId, perkEffects))
        });

        return perkRank;
    }

    private Spell CreatePerkRankSpell(int rankId, IMagicEffect[] perkEffects)
    {
        var spell = outputMod.Spells.AddNew("HaOv_EnvironmentalConditioning_Spell_Rank_" + rankId);
        spell.Name = "Environmental Conditioning Spell " + rankId;
        spell.Type = Spell.SpellType.Ability;
        
        foreach(var mf in perkEffects)
        {
            spell.Effects.Add(new MagicEffectSpellEntryBuilder()
                .WithBaseEffect(mf)
                .WithMagnitude(10)
                .Build()
            );
        }
        return spell;
    }
    private void PatchHazardSoakMax(string hazardType)
    {
        var resistAV = outputMod.ActorValueInformation.GetOrAddAsOverride(HazardTypeToResistanceValue(hazardType));
        resistAV.Max = 95;
    }

    private IActorValueInformationGetter HazardTypeToResistanceValue(string hazardType)
    {
        return hazardSystem.GetResistanceAV(hazardType);
    }

    // Creates a MagicEffect which sets the value of the actor value tracking which resistance "correction tier" we need to apply
    private MagicEffect CreateCorrectionTierEffect(string hazardType, IActorValueInformationGetter correctionTierAV)
    {
        var mf = outputMod.MagicEffects.AddNew("Resist_Correction_Tier_Effect_" + hazardType);
        mf.CastType = CastType.ConstantEffect;
        mf.ActorValue1.SetTo(correctionTierAV);
        mf.Archetype = new MagicEffectArchetype()
        {
            Type = MagicEffectArchetype.TypeEnum.ValueModifier
        };
        // Ensure we don't have some random resistance on this effect
        mf.ResistValue.SetToNull();
        mf.Flags = MagicEffect.Flag.NoArea | MagicEffect.Flag.NoHitEvent | MagicEffect.Flag.Recover;

        return mf;
    }

    private MagicEffect CreateResistanceDebuffEffect(string hazardType, IActorValueInformationGetter resistanceTierAV)
    {
        var mf = outputMod.MagicEffects.AddNew("Resist_Correction_Debuff_Effect_" + hazardType);
        mf.CastType = CastType.ConstantEffect;
        mf.ActorValue1.SetTo(resistanceTierAV);
        mf.Archetype = new MagicEffectArchetype()
        {
            Type = MagicEffectArchetype.TypeEnum.PeakValueModifier
        };
        // Ensure we don't have some random resistance on this effect
        mf.ResistValue.SetToNull();
        mf.Flags = MagicEffect.Flag.NoArea | MagicEffect.Flag.NoHitEvent | MagicEffect.Flag.Recover | MagicEffect.Flag.Detrimental;

        return mf;
    }
    /// <summary>
    /// Adds an ability able to reduce the player's resistance, if their resistance goes above the allowed maximum (eg. 85).
    /// It uses two MagicEffects: One for storing which "tier" of debuff to apply and one for applying the debuff to resistance.
    /// The "tier" determines how big the debuff should be. If we apply the debuff to resistance directly, we have no way of knowing
    /// what the previous selection was, with "tier" we can apply the same debuff if the tier remains the same.
    /// If setting debuff without using a tier, the ability would detect that the resistance is now at an acceptable level and remove the debuff. 
    /// Next time the debuff is applied it will then detect the resistance needs to be reduced and apply the debuff - Leading to an infinite loop
    /// "Tier" allows us to lock the selection as long as there aren't any changes to the players values.
    /// </summary>
    private Spell AddMaxResistanceAbility(string hazardType)
    {
        var correctionAV = outputMod.ActorValueInformation.AddNew("Resist_Correction_AV_" + hazardType);
        var setTierMF = CreateCorrectionTierEffect(hazardType, correctionAV);
        var resistanceAV = HazardTypeToResistanceValue(hazardType);
        var resistanceDebuffSpell = CreateResistanceDebuffEffect(hazardType, resistanceAV);


        var debuffSpell = outputMod.Spells.AddNew("Resist_Correction_Debuff_Spell_" + hazardType);
        debuffSpell.Type = Spell.SpellType.Ability;
        debuffSpell.Flags = Spell.Flag.IgnoreResistance;

        // Add MF to set which debuff "tier" - that is, how much we need to adjust the damage based on its overflow from 85
        debuffSpell.Effects.AddRange([
            CreateDispellEffect(resistanceAV, setTierMF),
            CreateEffectFor(90, 2, resistanceAV, correctionAV, setTierMF),
            CreateEffectFor(95, 3, resistanceAV, correctionAV, setTierMF),
        ]);

        // Add MF that actually correct the resistance based on the tier.
        debuffSpell.Effects.AddRange([
            // Tier 1 sets the debuff to 0 and "resets" it
            CreateTierDebuff(1, 0, correctionAV, resistanceDebuffSpell),
            CreateTierDebuff(2, 5, correctionAV, resistanceDebuffSpell),
            CreateTierDebuff(3, 10, correctionAV, resistanceDebuffSpell),
        ]);

        return debuffSpell;
    }

    private Effect CreateDispellEffect(IActorValueInformationGetter resistanceAV, IMagicEffectGetter mf)
    {
        return new MagicEffectSpellEntryBuilder()
            .WithBaseEffect(mf)
            .WithMagnitude(1)
            .AddCondition(GetValueCondition.With(resistanceAV).LessThan().Value(85))
            .Build();
    }
    private Effect CreateTierDebuff(int tierValue, int effectMagnitude, IActorValueInformationGetter tierAV, IMagicEffectGetter mf)
    {
        return new MagicEffectSpellEntryBuilder()
            .WithBaseEffect(mf)
            .WithMagnitude(effectMagnitude)
            .AddCondition(GetValueCondition.With(tierAV).EqualsTo().Value(tierValue))
            // Check we don't have the effect given by the perk that unlocks 95% resistance
            .AddCondition(HasMagicEffect.With(removeResistanceCapSpellEffect).EqualsTo().Value(0))
            .Build();
    }

    private Effect CreateEffectFor( int resistanceValue, int tierValue, IActorValueInformationGetter resistanceAV, IActorValueInformationGetter tierAV, IMagicEffectGetter mf)
    {
        return new MagicEffectSpellEntryBuilder()
            .WithBaseEffect(mf)
            .WithMagnitude(tierValue)
            .AddCondition(GetValueCondition.With(resistanceAV).EqualsTo().ValueOr(resistanceValue))
            .AddCondition(GetValueCondition.With(tierAV).EqualsTo().ValueOr(tierValue))
            .Build();
    }
}