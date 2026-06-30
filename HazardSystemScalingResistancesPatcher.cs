using System;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Cache.Internals.Implementations;
using Mutagen.Bethesda.Starfield;
using Noggog;

public class HazardSystemScalingResistancesPatcher
{
    //835
    private readonly HazardSystem hazardSystem;
    private readonly StarfieldMod outputMod;
    private readonly ILinkCache<IStarfieldMod, IStarfieldModGetter> baseGameLinkCache;

    private HazardSystemScalingResistancesPatcher(HazardSystem hazardSystem, StarfieldMod outputMod, ILinkCache<IStarfieldMod, IStarfieldModGetter> baseGameLinkCache)
    {
        this.hazardSystem = hazardSystem;
        this.outputMod = outputMod;
        this.baseGameLinkCache = baseGameLinkCache;
    }

    public static void WritePatch(HazardSystem hazardSystem, StarfieldMod outputMod, ILinkCache<IStarfieldMod, IStarfieldModGetter> baseGameLinkCache)
    {
        var patcher = new HazardSystemScalingResistancesPatcher(hazardSystem, outputMod, baseGameLinkCache);
        patcher.PatchInternal();
    }
    private void PatchInternal()
    {
        var perk = outputMod.Perks.AddNew("EnvResistanceBooster");
        perk.Description = "Provides a hidden bonus to hazard resistance";
        perk.Categroy = PerkCategory.None;
        perk.Flags = Perk.Flag.PcPlayable;

        string hazardType = "Radiation";

        var perkRank = new PerkRank()
        {
            Description = "Provides a hidden bonus to hazard resistance",
            Effects = new ExtendedList<APerkEffect>()
            {
                MakePerkEffect(hazardType,1 - 0.125f,             20f,  25f),
                MakePerkEffect(hazardType,1 - 0.133333333333333f, 25f,  30f),
                MakePerkEffect(hazardType,1 - 0.171428571428571f, 30f,  40f),
                MakePerkEffect(hazardType,1 - 0.3f,                40f,  50f),
                MakePerkEffect(hazardType,1 - 0.5f,                50f,  70f),
                MakePerkEffect(hazardType,1 - 0.5f,                70f,  85f),
                MakePerkEffect(hazardType,1 - 0.333333333333333f, 85f,  90f),
                MakePerkEffect(hazardType,1 - 0.3f,                90f,  95f),
                MakePerkEffect(hazardType,1 - 0.4f,                95f, 100f),
            }
        };

        perk.Ranks.Add(perkRank);
        Console.WriteLine("Resistance booster perk: " + perk.FormKey);
    }



    private  APerkEffect MakePerkEffect(string hazardType, float modifer, float resistanceMin, float resistanceMax)
    {
        return new PerkEntryPointModifyValue
        {
            EntryPoint = APerkEntryPointEffect.EntryType.ModIncomingSpellMagnitude,
            Modification = PerkEntryPointModifyValue.ModificationType.Multiply,
            Value = modifer,
            PerkConditionTabCount = 3,

            // Conditions
            Conditions = new ExtendedList<PerkCondition>
            {
                CreateIsHazardEffectOfType(hazardType),
                CreateStepLadderPerkCondition(resistanceMin, resistanceMax, hazardType) // Only apply effect if resistances lands on the specific stepladder
            }
        };
    }
    public IKeywordGetter GetDamageTypeKeyword(string hazardType)
    {
        switch(hazardType)
        {
            case "Corrosive":
                return GetKeywordCorrosive();
            case "Airborne":
                return GetKeywordAirborne();
            case "Radiation":
                return GetKeywordRadiation();
            case "Thermal":
                return GetKeywordThermal();
        }
        throw new NotImplementedException();
    }
    public IKeywordGetter GetKeywordRadiation()
    {
        return baseGameLinkCache.Resolve<IKeywordGetter>("ENV_EnvDamageType_Radiation");
    }
    public IKeywordGetter GetKeywordAirborne()
    {
        return baseGameLinkCache.Resolve<IKeywordGetter>("ENV_EnvDamageType_Airborne");
    }
    public IKeywordGetter GetKeywordCorrosive()
    {
        return baseGameLinkCache.Resolve<IKeywordGetter>("ENV_EnvDamageType_Corrosive");
    }
    public IKeywordGetter GetKeywordThermal()
    {
        return baseGameLinkCache.Resolve<IKeywordGetter>("ENV_EnvDamageType_Radiation");
    }
    private PerkCondition CreateIsHazardEffectOfType(string hazardType)
    {
        // Index 1 is "Spell"
        const byte CONDITION_INDEX = 1;

        var conditionData = new HasKeywordConditionData();
        conditionData.RunOnType = Condition.RunOnType.Subject;
        conditionData.FirstParameter.Link.SetTo(GetDamageTypeKeyword(hazardType));

        return  new PerkCondition()
        {
            RunOnTabIndex = CONDITION_INDEX,
            Conditions = new Noggog.ExtendedList<Condition>
            {
                new ConditionFloat()
                {
                    CompareOperator = CompareOperator.EqualTo,
                    ComparisonValue = 1,
                    Data = conditionData
                }
            }
        };
    }

    private PerkCondition CreateStepLadderPerkCondition(float requiredMin, float requiredMax, string hazardType)
    {
        byte _TAB_INDEX = 0;
        var conditionData = new GetValueConditionData();
        conditionData.FirstParameter.SetTo(baseGameLinkCache.Resolve<IActorValueInformationGetter>("Env_Resist_Radiation"));

        return new PerkCondition()
        {
            RunOnTabIndex = _TAB_INDEX,
            Conditions = new ExtendedList<Condition>
            {
                new ConditionFloat()
                {
                    CompareOperator = CompareOperator.LessThan,
                    ComparisonValue = requiredMax,
                    Data = conditionData
                },
                new ConditionFloat()
                {
                    CompareOperator = CompareOperator.GreaterThanOrEqualTo,
                    ComparisonValue = requiredMin,
                    Data = conditionData
                }
            }

        };
    }
}