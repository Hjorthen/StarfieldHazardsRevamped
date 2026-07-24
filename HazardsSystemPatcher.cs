using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Records.Fluent;

public class HazardsMapper
{
    public IReadOnlyCollection<string> HazardTypes
    {
        get;
        private set;
    }

    public HazardsMapper(IReadOnlyCollection<string> hazardTypes)
    {
        HazardTypes = hazardTypes;
    }
}


// Rebuilds the HazardsSystem by creating unique Actor-Values for each Hazard type and setting up ConditionForms necessary for the Hazards to be properly utilized
// Basegame has three hazard types: Extreme Environment, Weather and "Traps" such as vents, toxic pools, etc.
// Extreme Environments trigger a SPELL: Env_Suppress_ExtremeEnvironment.
// The spell will damage a suits soak value or players health when empty. It scales based on how many simutainous extreme environment effects that are on the player.
// One condition ENV_DMG_DepleteSoak_ExtremeEnvironment_Effect I'm not sure why triggers if ever?
public class HazardsSystemPatcher
{
    private readonly IReadOnlyCollection<string> hazardTypes;
    private readonly StarfieldMod mod; 
    private readonly BaseGameTypeResolver resolver;

    public static (HazardSystem, RequiredSystemRecords) WritePatch(StarfieldMod mod, IReadOnlyCollection<string> hazardTypes, BaseGameTypeResolver resolver)
    {
        var patcher = new HazardsSystemPatcher(mod, hazardTypes, resolver);
        return patcher.PatchInternal();

    }
    private HazardsSystemPatcher(StarfieldMod mod, IReadOnlyCollection<string> hazardTypes, BaseGameTypeResolver resolver)
    {
        this.hazardTypes = hazardTypes;
        this.mod = mod;
        this.resolver = resolver;
    }

    Dictionary<string, ActorValueInformation> envSoakRecords;
    Dictionary<string, ConditionRecord> envSoakConditions;
    Dictionary<string, ConditionRecord> envApplyEnvDamageCondition;
    ConditionRecord soakDamageTakenCondition;

    private (HazardSystem, RequiredSystemRecords) PatchInternal()
    {
        envSoakRecords = AddSoakValues();
        envSoakConditions = AddSuitIntegritySoakCounter();
        envApplyEnvDamageCondition = AddSoakDepletedCondition();
        soakDamageTakenCondition = CreateSoakDamageTakenConditionRecord();
        PatchSoakRestoreCondition(soakDamageTakenCondition);
        // We've split the suits ability to soak damage into 4 so we need to adjust the damage as well.
        PatchHazardDamage();
        var damageSoakSync = PatchDamageSoakSync();
        var hazardSystem = MakeHazardSystem();
        var soakNotficiation = AddNewNotificationSpell();
        var requiredRecords = new RequiredSystemRecords()
        {
            Spells = [soakNotficiation],
        };
        return (hazardSystem, requiredRecords);
    }

    // Patches Env_Damage_Soak which the base game watches for making the environmental damage icons blink.
    // The value goes from 100->0 making the icons blink more frequently, the lower the number.
    private ISpellGetter PatchDamageSoakSync()
    {
        var thresholdConditions = CreateThresholdConditions();
        var damageSoakEffect = AddSoakSyncDamageEffect();
        var restoreSoakEffect = AddSoakSyncRestoreEffect();
        return AddDamageSoakSyncAbility(thresholdConditions, damageSoakEffect, restoreSoakEffect);
    }

    private Spell AddNewNotificationSpell()
    {
        var notifySpell = mod.Spells.AddNew("HaOS_SoakDamage_Notifier");
        notifySpell.Name = "HaOS Soak Notifier";
        notifySpell.Type = Spell.SpellType.Disease;
        
        foreach (var type in hazardTypes)
        {
            var spellEffects = MakeNotificationEffectsForType([100, 90, 70, 35, 25, 15, 10, 5, 0], type);
            notifySpell.Effects.AddRange(spellEffects);
        }


        return notifySpell;
    }

    // Helper function to create the conditions to invoke the magic effect at given intervals
    private static IEnumerable<Effect> MakeNotificationEffectsForType(float[] thresholds, IActorValueInformationGetter soakAv, IMagicEffectGetter warningEffect)
    {
        var conditionPairs = thresholds
        .Zip(thresholds.Skip(1).Append(0),
            (upper, lower) => new Condition[] {
                GetValueCondition.With(soakAv).LessThanOrEqual().Value(upper),
                GetValueCondition.With(soakAv).GreaterThan().Value(lower)
            }
        ).ToList(); // Zip with itself but have the "other" pair be the next value or 0

        for (int i = 0; i < thresholds.Length; i++)
        {
            float belowMagnitude = thresholds[i];
            yield return new MagicEffectSpellEntryBuilder().WithBaseEffect(warningEffect).WithMagnitude(belowMagnitude).AddConditions(conditionPairs[i]).Build();
        }
    }

    private IEnumerable<Effect> MakeNotificationEffectsForType(float[] thresholds, string hazardType)
    {
        var effect = AddNotificationMagicEffect(hazardType);
        var soakAV = envSoakRecords[hazardType];
        return MakeNotificationEffectsForType(thresholds, soakAV, effect);
    }

    private IMagicEffect AddNotificationMagicEffect(string hazardType)
    {
        string editorId = $"HaOS_ThresholdNotification_{hazardType}";
        var soakAV = envSoakRecords["hazardType"];
        var warningEffect = mod.MagicEffects.AddNew(editorId);

        warningEffect.Description = $"{hazardType} protection at <mag>%";
        warningEffect.DATADataTypeState |= MagicEffect.DATADataType.Break0;

        ScriptAttachment.OfScript("Haos_SoakNotificationScript")
            .SetProperty("HazardTypeName", hazardType)
            .ApplyTo(warningEffect);
        return warningEffect;
    }

    private MagicEffect AddSoakSyncRestoreEffect()
    {
        var magicEffect = mod.MagicEffects.AddNew("HaOS_SoakSync_Restore_MF");
        // We need "Recover" such that the value is restored once the debuff wears off
        magicEffect.Flags = MagicEffect.Flag.HideInUI | MagicEffect.Flag.NoArea;
        magicEffect.CastType = CastType.ConstantEffect;
        magicEffect.ActorValue2.SetTo(resolver.ENV_Damage_Soak_AV);
        magicEffect.Archetype = new MagicEffectArchetype()
        {
            Type = MagicEffectArchetype.TypeEnum.ValueModifier
        };

        magicEffect.DATADataTypeState |= MagicEffect.DATADataType.Break0;
        return magicEffect;
    }
    private MagicEffect AddSoakSyncDamageEffect()
    {
        var magicEffect = mod.MagicEffects.AddNew("HaOS_DamageSoak_Sync_MF");
        // We need "Recover" such that the value is restored once the debuff wears off
        magicEffect.Flags = MagicEffect.Flag.Detrimental | MagicEffect.Flag.HideInUI | MagicEffect.Flag.NoArea;
        magicEffect.CastType = CastType.ConstantEffect;
        magicEffect.ActorValue2.SetTo(resolver.ENV_Damage_Soak_AV);
        magicEffect.Archetype = new MagicEffectArchetype()
        {
            Type = MagicEffectArchetype.TypeEnum.ValueModifier
        };

        magicEffect.DATADataTypeState |= MagicEffect.DATADataType.Break0;
        return magicEffect;
    }

    // Create an ability that damages Env_DamageSoak whenever the new Soak values drop below threshold
    // Each threshold are added as a condition, with the effect being applied with the magnitude of 
    // the threshold.
    private Spell AddDamageSoakSyncAbility(List<(int threshold, ConditionRecord other)> thresholds, IMagicEffectGetter damageEffect, IMagicEffectGetter restoreEffect)
    {
        var spell = mod.Spells.AddNew("HaOS_DamageSoak_Sync_Ability");
        spell.Name = "HaOS DamageSoak Ability";

        for (int i = 0; i < thresholds.Count; i++)
        {
            var (threshold, conditionRecord) = thresholds[i];
            // Previous threshold value, used to calculate the diff between current and past threshold
            int previousThreshold = (i-1)>=0 ? thresholds[i-1].threshold : 100;
            int thresholdDifference = previousThreshold - threshold;
            // We would like to quickly catch up with the sync float
            // Magnitude depends on the difference between the two thresholds
            // Thresholds far apart has greater magnitude than those close
            float magnitude = thresholdDifference / 5.0f;
            // We don't need to be exact so we keep a "slack" to prevent the damage from overshooting the target and then restore undershoot continously.
            float previousThresholdWithSlack = previousThreshold - magnitude;

            // If Env_Damage_Soak is currently greater than any of the soak counters,
            // we damage the value until that is no longer the case
            var thresholdDamageEffect = new MagicEffectSpellEntryBuilder()
                .WithBaseEffect(damageEffect)
                .AddCondition(GetConditionFormCondition.With(conditionRecord).EqualsTo().Value(1))
                .AddCondition(GetValueCondition.With(resolver.ENV_Damage_Soak_AV).GreaterThan().Value(threshold))
                .WithMagnitude(magnitude)
                .Build();

            // If Env_Damage_Soak is currently lower than all the soak counters,
            // we restore the value until that is no longer the case.
            // Jumps can happen in case the player uses a restore item.
            // 
            // We restore back up to the previous value in case the conditions 
            // for the _current_ threshold is no longer valid.
            var thresholdRestoreEffect = new MagicEffectSpellEntryBuilder()
                .WithBaseEffect(restoreEffect)
                .AddCondition(GetConditionFormCondition.With(conditionRecord).EqualsTo().Value(0))
                .AddCondition(GetValueCondition.With(resolver.ENV_Damage_Soak_AV).LessThan().Value(previousThresholdWithSlack))
                .WithMagnitude(magnitude)
                .Build();

            spell.Effects.Add(thresholdDamageEffect);
            spell.Effects.Add(thresholdRestoreEffect);
        }

        spell.CastType = CastType.ConstantEffect;
        spell.Type = Spell.SpellType.Ability;
        spell.Flags = Spell.Flag.IgnoreResistance;

        return spell;
    }

    // Creates a ConditionRecord that evaluates whether any of the Soak values has 
    // been lowered to a certain threshold. 
    private List<(int threshold, ConditionRecord other)> CreateThresholdConditions()
    {
        var thresholdConditions = new List<(int threshold, ConditionRecord other)>();
        foreach(var threshold in new[] {95, 40, 30, 20, 10, 5, 0})
        {
            var record = CreateHazardConditionForThreshold(threshold);    
            thresholdConditions.Add((threshold, record));
        }

        return thresholdConditions;
    }

    /// <summary>
    /// Creates a ConditionRecord that is true if any hazard soak records drop below threshold value. 
    /// eg. if threshold is 95, if any soak record is below 95 the condition is true. 
    /// </summary>
    /// <param name="threshold">Scale from 0..100 for where the treshold should be</param>
    /// <returns></returns>
    private ConditionRecord CreateHazardConditionForThreshold(int threshold)
    {
        ConditionFormBuilder builder = new();
        foreach(var (hazardType, AV) in envSoakRecords)
        {
            builder.AddGetValueCondition(AV, c => c.LessThan().ValueOr(threshold));
        }
        return builder.Build(mod, "HaOS_Soak_Condition_Threshold_" + threshold);
    }

    private void PatchHazardDamage()
    {
        foreach(var value in resolver.GetElementalDamageMagnitudeValues())
        {
            var globalOverride = mod.Globals.GetOrAddAsOverride(value); 
            globalOverride.Data = value.Data!.Value * 4;
        }
    }

    private HazardSystem MakeHazardSystem()
    {
        return new HazardSystem(
            hazardToDamageSoakValue: envSoakRecords.ToDictionary(kv => kv.Key, kv => new EditorId<ActorValueInformation>(kv.Value.EditorID!)),
            hazardToDamageSoakCondition:  envSoakConditions.ToDictionary(kv => kv.Key, kv => new EditorId<ConditionRecord>(kv.Value.EditorID!)),
            hazardToApplyEnvDamageCondition: envApplyEnvDamageCondition.ToDictionary(kv => kv.Key, kv => new EditorId<ConditionRecord>(kv.Value.EditorID!)),
            hazardToResistance: hazardTypes.ToDictionary(hazard => hazard, hazard => new EditorId<ActorValueInformation>(resolver.GetResistanceForHazard(hazard).EditorID!)),
            soakDamageTakenCondition: new EditorId<ConditionRecord>(soakDamageTakenCondition.EditorID!)
        );
    }
    private Dictionary<string, ActorValueInformation> AddSoakValues()
    {
        var newSoakRecords = new Dictionary<string, ActorValueInformation>();
        foreach(var entry in hazardTypes)
        {
            var newValue = mod.ActorValueInformation.AddNew($"ENV_Soak_{entry}");
            Console.WriteLine("Adding new SoakValue: " + newValue.EditorID);
            newValue.Type = ActorValueInformation.Types.Variable;
            newValue.DefaultValue = 100;
            newValue.Flags = ActorValueInformation.Flag.MaximumOneHundred;

            newSoakRecords[entry] = newValue;
        }
        return newSoakRecords;
    }
    // Base game checks if it should deal EnvironmentDamage based on: SoakDamage < 1 and EnvironmentalDamage enabled in settings
    private Dictionary<string, ConditionRecord> AddSoakDepletedCondition()
    {
        var envApplyEnvDamageCondition = new Dictionary<string, ConditionRecord>();
        IConditionRecordGetter basegameEnvironmentDamageCondition = resolver.GetApplyEnvironmentDamageConditionRecord();
        foreach (var entry in hazardTypes)
        {
            envApplyEnvDamageCondition[entry] = CreateApplyEnvironmentDamage(entry, basegameEnvironmentDamageCondition);
        }
        return envApplyEnvDamageCondition;
    }

    // Base game checks if it should deal SoakDamage based on: SoakDamage > 0 and EnvironmentalDamage enabled in settings
    private Dictionary<string, ConditionRecord> AddSuitIntegritySoakCounter()
    {
        var newSoakConditions = new Dictionary<string, ConditionRecord>();
        IConditionRecordGetter basegameSoakDamageCondition = resolver.GetSoakDamageConditionRecord();
        foreach (var entry in hazardTypes)
        {
            newSoakConditions[entry] = CreateSoakConditionRecord(entry, basegameSoakDamageCondition);
        }
        return newSoakConditions;
    }
    private ConditionRecord CreateSoakConditionRecord(string hazardType, IConditionRecordGetter basegameSoakDamageCondition)
    {
        Console.WriteLine("Adding new ConditionRecord for SoakValue for " + hazardType);
        return DuplicateConditionRecordForHazardSoak(hazardType, $"ENV_CND_DamageSoak_{hazardType}", basegameSoakDamageCondition);
    }

    private ConditionRecord CreateApplyEnvironmentDamage(string hazardType, IConditionRecordGetter basegameConditionForm)
    {
        Console.WriteLine("Adding new ConditionRecord for ApplyEnvironmentDamage for " + hazardType);
        return DuplicateConditionRecordForHazardSoak(hazardType, $"ENV_CND_ApplyEnvironmentalDamage_{hazardType}", basegameConditionForm);
    }

    private ConditionRecord DuplicateConditionRecordForHazardSoak(string hazardType, string editorName, IConditionRecordGetter basegameSoakDamageCondition)
    {
        var copy = mod.ConditionRecords.DuplicateInAsNewRecord(basegameSoakDamageCondition, editorName);
        foreach (var condition in copy.Conditions)
        {
            if (resolver.IsConditionTargetingDamageSoak(condition))
            {
                resolver.ReplaceConditionValue(condition, envSoakRecords[hazardType]);
            }
        }
        return copy;
    }

    private void PatchSoakRestoreCondition(ConditionRecord soakDamageTakenConditionRecord)
    {
        // Override the condition used by the engine to target our own ConditionRecord
        var baseConditionRecord = resolver.GetSoakRestoreConditionRecord();
        var newConditionRecord = mod.ConditionRecords.GetOrAddAsOverride(baseConditionRecord);
        foreach (var condition in newConditionRecord.Conditions)
        {
            // We want to replace the condition that checks for damage soak
            // using our own FormCondition with the new Float values
            if (resolver.IsConditionTargetingDamageSoak(condition))
            {
                var conditionFloat = (IConditionFloat)condition;
                conditionFloat.CompareOperator = CompareOperator.EqualTo;
                conditionFloat.ComparisonValue = 1;

                var newConditionData = new IsTrueForConditionFormConditionData();
                newConditionData.FirstParameter = new FormLinkOrIndex<IConditionRecordGetter>(newConditionData, soakDamageTakenConditionRecord.FormKey);

                // Replace the condition with our new data
                condition.Data = newConditionData;
            }
        }
    }

    private ConditionRecord CreateSoakDamageTakenConditionRecord()
    {
        var newRecord = mod.ConditionRecords.AddNew("ENV_CND_SoakDamaged");
        // Add a condition checking if any Soak has taken damage
        foreach (var hazardType in hazardTypes)
        {
            var condition = new ConditionFloat
            {
                CompareOperator = CompareOperator.LessThan,
                ComparisonValue = 100,
                Flags = Condition.Flag.OR,
                Data = new GetValueConditionData()
                {
                    FirstParameter = new FormLink<IActorValueInformationGetter>(envSoakRecords[hazardType]),
                }
            };
            newRecord.Conditions.Add(condition);
        }

        return newRecord;
    }
}