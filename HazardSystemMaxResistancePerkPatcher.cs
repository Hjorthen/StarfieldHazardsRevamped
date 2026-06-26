using System;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache.Internals.Implementations;
using Mutagen.Bethesda.Starfield;
using Noggog;

public class HazardSystemMaxResistancePerkPatcher
{
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
        foreach(string hazardType in hazardSystem.HazardTypes)
        {
            var debuffAbility = AddMaxResistanceAbility(hazardType);
            PatchHazardSoakMax(hazardType);
            Console.WriteLine($"Added debuff ability for {hazardType}: {debuffAbility.FormKey}");
        }
    }

    private void PatchHazardSoakMax(string hazardType)
    {
        var resistAV = outputMod.ActorValueInformation.GetOrAddAsOverride(HazardTypeToResistanceValue(hazardType));
        resistAV.Max = 95;
    }

    private IActorValueInformationGetter HazardTypeToResistanceValue(string hazardType)
    {
        return baseGameLinkCache.Resolve<IActorValueInformationGetter>("Env_Resist_Radiation");
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

    private Spell AddMaxResistanceAbility(string hazardType)
    {
        var correctionAV = outputMod.ActorValueInformation.AddNew("Resist_Correction_AV_" + hazardType);
        var setTierMF = CreateCorrectionTierEffect(hazardType, correctionAV);
        var resistanceAV = HazardTypeToResistanceValue(hazardType);
        var resistanceDebuffSpell = CreateResistanceDebuffEffect(hazardType, resistanceAV);


        var debuffSpell = outputMod.Spells.AddNew("Resist_Correction_Debuff_Spell_" + hazardType);
        debuffSpell.Type = Spell.SpellType.Ability;
        debuffSpell.Flags = Spell.Flag.IgnoreResistance;

        // Add MF to set which "tier", that is how much we need to adjust the damage
        debuffSpell.Effects.AddRange([
            CreateDispellEffect(resistanceAV, setTierMF),
            CreateEffectFor(90, 2, resistanceAV, correctionAV, setTierMF),
            CreateEffectFor(95, 3, resistanceAV, correctionAV, setTierMF),
        ]);

        debuffSpell.Effects.AddRange([
            CreateTierDebuff(1, 0, correctionAV, resistanceDebuffSpell),
            CreateTierDebuff(2, 5, correctionAV, resistanceDebuffSpell),
            CreateTierDebuff(3, 10, correctionAV, resistanceDebuffSpell),
        ]);

        return debuffSpell;
    }

    private Effect CreateDispellEffect(IActorValueInformationGetter resistanceAV, IMagicEffectGetter mf)
    {

        return new Effect()
        {
            Conditions = new ExtendedList<Condition>()
            {
                new ConditionFloat()
                {
                    CompareOperator = CompareOperator.LessThan,
                    Data = new GetValueConditionData()
                    {
                        FirstParameter = new FormLink<IActorValueInformationGetter>(resistanceAV),
                    },
                    ComparisonValue = 85,
                },
            },
            BaseEffect = new FormLinkNullable<IMagicEffectGetter>(mf),
            Data = new EffectData()
            {
                Magnitude = 1
            }
        };
    }
    private Effect CreateTierDebuff(int tierValue, int effectMagnitude, IActorValueInformationGetter tierAV, IMagicEffectGetter mf)
    {
            return new Effect()
            {
                Conditions = new ExtendedList<Condition>()
                {
                    new ConditionFloat()
                    {
                        CompareOperator = CompareOperator.EqualTo,
                        Data = new GetValueConditionData()
                        {
                            FirstParameter = new FormLink<IActorValueInformationGetter>(tierAV),
                        },
                        ComparisonValue = tierValue,
                    },
                },
                BaseEffect = new FormLinkNullable<IMagicEffectGetter>(mf),
                Data = new EffectData()
                {
                    Magnitude = effectMagnitude
                }
            };
    }

    private Effect CreateEffectFor( int resistanceValue, int tierValue, IActorValueInformationGetter resistanceAV, IActorValueInformationGetter tierAV, IMagicEffectGetter mf)
    {
            return new Effect()
            {
                Conditions = new ExtendedList<Condition>()
                {
                    new ConditionFloat()
                    {
                        CompareOperator = CompareOperator.EqualTo,
                        Data = new GetValueConditionData()
                        {
                            FirstParameter = new FormLink<IActorValueInformationGetter>(resistanceAV),
                        },
                        ComparisonValue = resistanceValue,
                        Flags = Condition.Flag.OR
                    },
                    new ConditionFloat()
                    {
                        CompareOperator = CompareOperator.EqualTo,
                        Data = new GetValueConditionData()
                        {
                            FirstParameter = new FormLink<IActorValueInformationGetter>(tierAV),
                        },
                        ComparisonValue = tierValue,
                        Flags = Condition.Flag.OR
                    }
                },
                BaseEffect = new FormLinkNullable<IMagicEffectGetter>(mf),
                Data = new EffectData()
                {
                    Magnitude = tierValue
                }
            };

    }
}