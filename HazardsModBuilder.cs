using System;
using System.Collections.Generic;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Starfield;
// TODO: 
// Split the record ENV_CND_RestoreSoak to allow Soak values to restore - Maybe not? Just the ENV_RestoreSoak should be enough?
// ENV_RestoreSoak_Ability will need changing too
public class HazardsModBuilder
{
    private readonly StarfieldMod mod;
    private readonly IReadOnlyList<String> hazardTypes;
    private readonly BaseGameTypeResolver resolver;

    public HazardsModBuilder(IReadOnlyList<string> hazardTypes, BaseGameTypeResolver resolver)
    {
        this.hazardTypes = hazardTypes;
        this.resolver = resolver;
        this.mod = new StarfieldMod("MyMod.esp", StarfieldRelease.Starfield);
    }

    Dictionary<string, ActorValueInformation> envSoakRecords;
    Dictionary<string, ConditionRecord> envSoakConditions;
    Dictionary<string, ConditionRecord> envApplyEnvDamageCondition;
    public HazardsModBuilder AddSoakValues()
    {
        envSoakRecords = new();
        foreach(var entry in hazardTypes)
        {
            var newValue = mod.ActorValueInformation.AddNew($"ENV_{entry}_Soak");
            newValue.Type = ActorValueInformation.Types.IntValue;
            newValue.Min = 0;
            newValue.Max = 100;
            newValue.DefaultValue = 0;

            envSoakRecords[entry] = newValue;
        }
        return this;
    }

    private ConditionRecord CreateSoakConditionRecord(string hazardType, IConditionRecordGetter basegameSoakDamageCondition)
    {
        return DuplicateConditionRecordForHazardSoak(hazardType, $"ENV_CND_{hazardType}_DamageSoak", basegameSoakDamageCondition);
    }

    private ConditionRecord CreateApplyEnvironmentDamage(string hazardType, IConditionRecordGetter basegameConditionForm)
    {
        return DuplicateConditionRecordForHazardSoak(hazardType, $"ENV_CND_ApplyEnvironmentalDamage_{hazardType}", basegameConditionForm);
    }

    private ConditionRecord DuplicateConditionRecordForHazardSoak(string hazardType, string editorName, IConditionRecordGetter basegameSoakDamageCondition)
    {
        var copy = mod.ConditionRecords.DuplicateInAsNewRecord(basegameSoakDamageCondition, editorName);
        foreach (var condition in copy.Conditions)
        {
            if (resolver.IsConditionDamageSoak(condition))
            {
                resolver.ReplaceConditionValue(condition, envSoakRecords[hazardType]);
            }
        }
        return copy;
    }

    public HazardsModBuilder AddSoakDamageConditionForms()
    {
        AddSuitIntegritySoakCounter();

        AddSuitIntegrityFailureCondition();

        return this;
    }


    // Base game checks if it should deal EnvironmentDamage based on: SoakDamage < 1 and EnvironmentalDamage enabled in settings
    private void AddSuitIntegrityFailureCondition()
    {
        envApplyEnvDamageCondition = new();
        IConditionRecordGetter basegameEnvironmentDamageCondition = resolver.GetApplyEnvironmentDamageConditionRecord();
        foreach (var entry in hazardTypes)
        {
            envApplyEnvDamageCondition[entry] = CreateApplyEnvironmentDamage(entry, basegameEnvironmentDamageCondition);
        }
    }

    // Base game checks if it should deal SoakDamage based on: SoakDamage > 0 and EnvironmentalDamage enabled in settings
    private void AddSuitIntegritySoakCounter()
    {
        envSoakConditions = new();
        IConditionRecordGetter basegameSoakDamageCondition = resolver.GetSoakDamageConditionRecord();
        foreach (var entry in hazardTypes)
        {
            envSoakConditions[entry] = CreateSoakConditionRecord(entry, basegameSoakDamageCondition);
        }
    }
    public HazardsModBuilder PatchRestoreSoak()
    {
        // TODO:
        // Create ConditionForm for evaluating if any of the Soak are below full
        // Patch the new ConditionForm into the existing ENV_CND_RestoreSoak form
        // Create a MagicEffect restoring each of the Hazard type's Soak counters
        // Patch ENV_RestoreSoak_Ability to apply the created magic effects. The Ability is applied by SQ_ENV which we DON'T want to have to modify..
        // Bonus: Add a 5th MagicEffect that displays 'Protection Regenerating' and toggle 'Hide in UI' on the others? That way players won't have 4 of the same spells visible..
        return this;
    }
    public HazardsModBuilder PatchMagicEffects(IEnumerable<IMagicEffectGetter> magicEffects)
    {
        foreach(var record in magicEffects)
        {
            if(record.EditorID.StartsWith("ENV_"))
            {
                if(resolver.IsEnvironmentalDamage(record))
                {
                    PatchTargetingEnvSoak(record);
                }
            }
        }
        return this;
    }

    public HazardsModBuilder PatchSpellHazards(IEnumerable<ISpellGetter> spells)
    {
        foreach(var record in spells)
        {
            // Won't catch some terrormorph ability as well as a reactor POI
            if(!record.EditorID.StartsWith("ENV_"))
                continue;

            bool needsPatching = false; 

            foreach(var effect in record.Effects)
            {
                foreach(var condition in effect.Conditions)
                {
                    if(resolver.IsConditionDamageSoak(condition))
                    {
                        needsPatching = true;
                        break;
                    }
                }
            }

            if(needsPatching)
                PatchSpellHazard(record, "Thermal");
        }
        return this;
    }

    private void PatchSpellHazard(ISpellGetter record, string hazardType)
    {
        Console.WriteLine("Patching spell " + record.EditorID);
        var patch = mod.Spells.GetOrAddAsOverride(record);
        foreach(var effect in patch.Effects)
        {
            foreach(var condition in effect.Conditions)
            {
                // Replace the conditions that check if we should deteriorate the suit integrity
                if (resolver.IsConditionDamageSoak(condition))
                {
                    resolver.ReplaceConditionTarget(condition, envSoakConditions[hazardType]);
                }

                // Replace the conditions that check if we should start applying damage
                if (resolver.IsConditionApplyEnviornmentalDamage(condition))
                {
                    resolver.ReplaceConditionTarget(condition, envApplyEnvDamageCondition[hazardType]);
                }
            }
        }
    }

    private void PatchTargetingEnvSoak(IMagicEffectGetter record)
    {
        Console.WriteLine("Patching magic effect " + record.EditorID);
        var patch = mod.MagicEffects.GetOrAddAsOverride(record);
        patch.ActorValue2.SetTo(GetEnvSoakTypedFor(record));
    }

    private IActorValueInformationGetter GetEnvSoakTypedFor(IMagicEffectGetter record)
    {
        var type = resolver.GetEnvEffectDamageType(record);
        return envSoakRecords[type];
    }
}