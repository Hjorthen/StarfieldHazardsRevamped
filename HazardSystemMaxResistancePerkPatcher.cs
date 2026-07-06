using System;
using System.Collections.Generic;
using System.Linq;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Starfield;
using Noggog;

public class HazardSystemMaxResistancePerkPatcher
{
    private IMagicEffectGetter? removeResistanceCapSpellEffect;
    private readonly HazardSystem hazardSystem;
    private readonly StarfieldMod outputMod;
    private readonly ILinkCache<IStarfieldMod, IStarfieldModGetter> baseGameLinkCache;

    private HazardSystemMaxResistancePerkPatcher(HazardSystem hazardSystem, StarfieldMod outputMod, ILinkCache<IStarfieldMod, IStarfieldModGetter> baseGameLinkCache)
    {
        this.hazardSystem = hazardSystem;
        this.outputMod = outputMod;
        this.baseGameLinkCache = baseGameLinkCache;
    }

    public static RequiredSystemRecords WritePatch(HazardSystem hazardSystem, StarfieldMod outputMod, ILinkCache<IStarfieldMod, IStarfieldModGetter> baseGameLinkCache)
    {
        var patcher = new HazardSystemMaxResistancePerkPatcher(hazardSystem, outputMod, baseGameLinkCache);
        return patcher.PatchInternal();
    }
    private RequiredSystemRecords PatchInternal()
    {
        PatchConditioningPerk();
        var debuffAbilities = AddDebuffAbilities();
        return new RequiredSystemRecords()
        {
            Spells = debuffAbilities
        };
    }


    private List<Spell> AddDebuffAbilities()
    {
        var debuffAbilities = new List<Spell>();
        foreach (string hazardType in hazardSystem.HazardTypes)
        {
            var debuffAbility = AddMaxResistanceAbility(hazardType);
            PatchHazardSoakMax(hazardType);
            debuffAbilities.Add(debuffAbility);
            Console.WriteLine($"Added debuff ability for {hazardType}: {debuffAbility.FormKey}");
        }
        return debuffAbilities;
    }

    private IKeywordGetter GetKeywordEnvironmentalDamage()
    {
        return baseGameLinkCache.Resolve<IKeywordGetter>("ENV_EffectTypeEnvironmentalDamage");
    }

    private IKeywordGetter GetKeywordEnvironmentalDamageSoak()
    {
        return baseGameLinkCache.Resolve<IKeywordGetter>("ENV_EffectTypeEnvironmentalDamageSoak");
    }
    private PerkCondition CreateIsHazardHealthDamage()
    {
        // Index 1 is "Spell"
        const byte CONDITION_INDEX = 1;

        //ENV_EffectTypeEnvironmentalDamage
        //ENV_EffectTypeEnvironmentalDamageSoak
        var isDamageEffectType = new HasKeywordConditionData();
        isDamageEffectType.RunOnType = Condition.RunOnType.Subject;
        isDamageEffectType.FirstParameter.Link.SetTo(GetKeywordEnvironmentalDamage());

        var isSoakEffectType = new HasKeywordConditionData();
        isSoakEffectType.RunOnType = Condition.RunOnType.Subject;
        isSoakEffectType.FirstParameter.Link.SetTo(GetKeywordEnvironmentalDamageSoak());


        return  new PerkCondition()
        {
            RunOnTabIndex = CONDITION_INDEX,
            Conditions = new Noggog.ExtendedList<Condition>
            {
                new ConditionFloat()
                {
                    CompareOperator = CompareOperator.EqualTo,
                    ComparisonValue = 1,
                    Data = isDamageEffectType
                },
                new ConditionFloat()
                {
                    CompareOperator = CompareOperator.EqualTo,
                    ComparisonValue = 0,
                    Data = isSoakEffectType
                }
            }
        };
    }

    private APerkEffect ReducedEnvironmentDamageEntryPerk(float reduction)
    {
        return new PerkEntryPointModifyValue
        {
            EntryPoint = APerkEntryPointEffect.EntryType.ModIncomingSpellMagnitude,
            Modification = PerkEntryPointModifyValue.ModificationType.Multiply,
            Value = (1 - reduction),
            PerkConditionTabCount = 3,


            // Only apply the effect on hazard health-damage
            Conditions = new ExtendedList<PerkCondition>
            {
                CreateIsHazardHealthDamage(),
            }
        };
    }

    private void PatchConditioningPerk()
    {
        // Dictionary for quick access to our resistance boost effects
        var resistances = hazardSystem.HazardTypes.ToDictionary(k => k.ToLower(), CreateResistanceBoostEffect);
        var maxEffectUnlock = CreateUnlockMaxResistanceEffect();

        var perk  = outputMod.Perks.GetOrAddAsOverride(baseGameLinkCache.Resolve<IPerkGetter>("Skill_EnvironmentalConditioning"));
        perk.Ranks.Clear();

        // Apply MagicEffect EnvironmentalConditioning_ReduceChanceAFFL to reduce affliction chance (Same as base elemental affliction)
        var reduceChanceAffl = baseGameLinkCache.Resolve<IMagicEffectGetter>("EnvironmentalConditioning_ReduceChanceAFFL");
        var reduceEnvHealthDmg = ReducedEnvironmentDamageEntryPerk(0.1f);

        perk.Ranks.AddRange(new [] {
            CreateConditioningRank("Gain 10 resistance to thermal and radiation damage.", 1, resistances["thermal"], resistances["radiation"]),
            CreateConditioningRank("Gain 10 resistance to airborne and corrosive damage.", 2, resistances["thermal"], resistances["radiation"], resistances["airborne"], resistances["corrosive"]),
            CreateConditioningRank("Your suit might have given up but your body has not. Take less health damage and reduced chance to gain afflictions.",  3, reduceEnvHealthDmg, resistances["thermal"], resistances["radiation"], resistances["airborne"], resistances["corrosive"], reduceChanceAffl),
            CreateConditioningRank("Gain 10 maximum resistance to all environmental damage.", 4, reduceEnvHealthDmg, resistances["thermal"], resistances["radiation"], resistances["airborne"], resistances["corrosive"], reduceChanceAffl, maxEffectUnlock),
        });

        removeResistanceCapSpellEffect = maxEffectUnlock;
    }

    // Third rank is special as it doesn't just add
    private PerkRank CreateConditioningRank(string description, int rankId, APerkEffect perk, params IMagicEffectGetter[] magicEffects)
    {
        var perkWithMagicEffects = CreateConditioningRank(description, rankId, magicEffects);
        perkWithMagicEffects.Effects.Add(perk);
        return perkWithMagicEffects;
    }
    private IMagicEffect CreateUnlockMaxResistanceEffect()
    {
        var mf = outputMod.MagicEffects.AddNew("HaOS_Resist_Unlock_Max_Resistance_Marker");
        mf.CastType = CastType.ConstantEffect;
        // Creation Engine needs a value to be set. Setting it to first available, won't have an effect (effect is set to 0 magnitude)
        // may change if I figure out how..
        mf.ActorValue2.SetTo(hazardSystem.GetResistanceAV(hazardSystem.HazardTypes.First()));
        mf.Name = "Maximum resistance increased to 95%";
        mf.Description = "You have mastered the elements and are able to push your spacesuit well beyond the manufactorer's specs.";
        mf.Archetype = new MagicEffectArchetype()
        {
            Type = MagicEffectArchetype.TypeEnum.ValueModifier
        };
        mf.Flags = MagicEffect.Flag.NoArea | MagicEffect.Flag.NoHitEffect | MagicEffect.Flag.NoMagnitude;
        mf.DATADataTypeState |= MagicEffect.DATADataType.Break0;
        return mf;
    }
    private IMagicEffect CreateResistanceBoostEffect(string hazardType)
    {
        var mf = outputMod.MagicEffects.AddNew("HaOS_Resist_Buff_" + hazardType);
        mf.CastType = CastType.ConstantEffect;
        mf.ActorValue2.SetTo(hazardSystem.GetResistanceAV(hazardType));
        mf.Archetype = new MagicEffectArchetype()
        {
            Type = MagicEffectArchetype.TypeEnum.ValueModifier
        };
        mf.ResistValue.SetToNull();
        mf.Flags = MagicEffect.Flag.NoArea | MagicEffect.Flag.NoHitEffect | MagicEffect.Flag.Recover | MagicEffect.Flag.HideInUI;

        mf.DATADataTypeState |= MagicEffect.DATADataType.Break0;

        return mf;
    }

    // Creates a perk with <description> and adds the perkEffects as a single spell used by the Perk
    private PerkRank CreateConditioningRank(string description, int rankId, params IMagicEffectGetter[] perkEffects)
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

    private Spell CreatePerkRankSpell(int rankId, IMagicEffectGetter[] perkEffects)
    {
        var spell = outputMod.Spells.AddNew("HaOS_EnvironmentalConditioning_Spell_Rank_" + rankId);
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
        var mf = outputMod.MagicEffects.AddNew("HaOS_Resist_Correction_Tier_Effect_" + hazardType);
        mf.Flags = MagicEffect.Flag.NoArea | MagicEffect.Flag.Recover | MagicEffect.Flag.NoDuration | MagicEffect.Flag.Painless | MagicEffect.Flag.HideInUI | MagicEffect.Flag.NoHitEvent;
        mf.CastType = CastType.ConstantEffect;
        mf.ActorValue2.SetTo(correctionTierAV);
        mf.Archetype = new MagicEffectArchetype()
        {
            Type = MagicEffectArchetype.TypeEnum.ValueModifier
        };

        mf.DATADataTypeState |= MagicEffect.DATADataType.Break0;

        return mf;
    }

    private MagicEffect CreateResistanceDebuffEffect(string hazardType, IActorValueInformationGetter resistanceTierAV)
    {
        var mf = outputMod.MagicEffects.AddNew("HaOS_Resist_Correction_Debuff_Effect_" + hazardType);
        mf.Flags = MagicEffect.Flag.NoArea | MagicEffect.Flag.Recover | MagicEffect.Flag.NoDuration | MagicEffect.Flag.Painless | MagicEffect.Flag.Detrimental;
        mf.CastType = CastType.ConstantEffect;
        mf.ActorValue2.SetTo(resistanceTierAV);
        mf.Archetype = new MagicEffectArchetype()
        {
            Type = MagicEffectArchetype.TypeEnum.PeakValueModifier
        };

        mf.DATADataTypeState |= MagicEffect.DATADataType.Break0;

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
        var correctionAV = outputMod.ActorValueInformation.AddNew("HaOS_Resist_Correction_AV_" + hazardType);
        correctionAV.Type = ActorValueInformation.Types.Variable;
        correctionAV.DefaultValue = 0;
        var setTierMF = CreateCorrectionTierEffect(hazardType, correctionAV);
        var resistanceAV = HazardTypeToResistanceValue(hazardType);
        var resistanceDebuffSpell = CreateResistanceDebuffEffect(hazardType, resistanceAV);


        var debuffSpell = outputMod.Spells.AddNew("HaOS_Resist_Correction_Debuff_Spell_" + hazardType);
        debuffSpell.Type = Spell.SpellType.Ability;
        debuffSpell.Flags = Spell.Flag.IgnoreResistance;

        // Add MF to set which debuff "tier" - that is, how much we need to adjust the damage based on its overflow from 85
        debuffSpell.Effects.AddRange([
            CreateDispellEffect(resistanceAV, setTierMF),
            CreateEffectFor(90, 2, resistanceAV, correctionAV, setTierMF),
            CreateEffectFor(95, 3, resistanceAV, correctionAV, setTierMF),
            CreateEffectFor(100, 4, resistanceAV, correctionAV, setTierMF),
            CreateEffectFor(105, 5, resistanceAV, correctionAV, setTierMF),
            CreateEffectFor(110, 6, resistanceAV, correctionAV, setTierMF),
            CreateEffectFor(115, 7, resistanceAV, correctionAV, setTierMF),
        ]);

        // Add MF that actually correct the resistance based on the tier.
        debuffSpell.Effects.AddRange([
            // Tier 1 sets the debuff to 0 and "resets" it
            CreateTierDebuff(1, 0, correctionAV, resistanceDebuffSpell),
            CreateTierDebuff(2, 5, correctionAV, resistanceDebuffSpell),
            CreateTierDebuff(3, 10, correctionAV, resistanceDebuffSpell),
            CreateTierDebuff(4, 15, correctionAV, resistanceDebuffSpell),
            CreateTierDebuff(5, 20, correctionAV, resistanceDebuffSpell),
            CreateTierDebuff(6, 25, correctionAV, resistanceDebuffSpell),
            CreateTierDebuff(7, 30, correctionAV, resistanceDebuffSpell),
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