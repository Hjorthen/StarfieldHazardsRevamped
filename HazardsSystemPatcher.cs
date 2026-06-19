using System;
using System.Collections.Generic;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;

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
public class HazardsSystemPatcher
{
    private readonly IReadOnlyCollection<string> hazardTypes;
    private readonly StarfieldMod mod; 
    private readonly BaseGameTypeResolver resolver;

    public static HazardSystem WritePatch(StarfieldMod mod, IReadOnlyCollection<string> hazardTypes, BaseGameTypeResolver resolver)
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

    private HazardSystem PatchInternal()
    {
        envSoakRecords = AddSoakValues();
        envSoakConditions = AddSuitIntegritySoakCounter();
        envApplyEnvDamageCondition = AddSoakDepletedCondition();
        soakDamageTakenCondition = CreateSoakDamageTakenConditionRecord();
        PatchSoakRestoreCondition(soakDamageTakenCondition);
        return new HazardSystem(
            envSoakRecords, 
            envSoakConditions, 
            envApplyEnvDamageCondition, 
            soakDamageTakenCondition
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