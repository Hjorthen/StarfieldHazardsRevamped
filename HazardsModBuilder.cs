using System;
using System.Collections.Generic;
using System.IO;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Order;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;
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
    Dictionary<string, MagicEffect> envExtremeMagicEffects;
    Dictionary<string, ConditionRecord> envSoakConditions;
    Dictionary<string, ConditionRecord> envApplyEnvDamageCondition;
    public HazardsModBuilder AddSoakValues()
    {
        envSoakRecords = new();
        foreach(var entry in hazardTypes)
        {
            var newValue = mod.ActorValueInformation.AddNew($"ENV_Soak_{entry}");
            Console.WriteLine("Adding new SoakValue: " + newValue.EditorID);
            newValue.Type = ActorValueInformation.Types.Variable;
            newValue.DefaultValue = 100;

            envSoakRecords[entry] = newValue;
        }
        return this;
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
    public HazardsModBuilder AddExtremeEnvironmentMagicEffects()
    {
        var effectBase = resolver.GetExtremeEnvironmentEffect();
        envExtremeMagicEffects = new();
        foreach (var entry in hazardTypes)
        {
            envExtremeMagicEffects[entry] = CreateExtremeEnvironmentEffect(entry, effectBase);
        }

        return this;
    }

    private MagicEffect CreateExtremeEnvironmentEffect(string hazardType, IMagicEffectGetter effectBase)
    {
        var effectNew = mod.MagicEffects.DuplicateInAsNewRecord(effectBase);
        effectNew.ActorValue2.SetTo(envSoakRecords[hazardType]);

        return effectNew;
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

        ConditionRecord soakDamageTakenConditionRecord = CreateSoakDamageTakenConditionRecord();
        PatchSoakRestoreCondition(soakDamageTakenConditionRecord);
        var soakRestoreMagicEffects = CreateSoakRestoreMagicEffects();
        PatchRestoreSoakAbility(soakRestoreMagicEffects);
        return this;
    }

    // The RestoreSoakAbility is applied by the game whenever a player is in a safe area. 
    // We need it to apply spell effects for all the soak value types.
    private void PatchRestoreSoakAbility(IEnumerable<IMagicEffectGetter> applyEffects)
    {
        var originalRestoreSoakAbility = resolver.GetRestoreSoakAbility();
        var originalEffect = originalRestoreSoakAbility.Effects[0];

        var formOverride = mod.Spells.GetOrAddAsOverride(originalRestoreSoakAbility);
        foreach(var newEffect in applyEffects)
        {
            var effect = originalEffect.DeepCopy();
            effect.BaseEffect = new FormLinkNullable<IMagicEffectGetter>(newEffect);
            formOverride.Effects.Add(effect);
        }
    }

    private IEnumerable<MagicEffect> CreateSoakRestoreMagicEffects()
    {
        List<MagicEffect> createdEffects = [];
        var baseMagicEffect = resolver.GetRestoreSoakMagicEffectRecord();
        foreach(var hazardType in hazardTypes)
        {
            var newMF = mod.MagicEffects.DuplicateInAsNewRecord(baseMagicEffect, $"ENV_RestoreSoak_Effect_{hazardType}");
            newMF.Name = "Restore " + hazardType;
            newMF.ActorValue2.SetTo(envSoakRecords[hazardType]);
            createdEffects.Add(newMF);
        }

        return createdEffects;
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

    public HazardsModBuilder PatchMagicEffects(IEnumerable<IMagicEffectGetter> magicEffects)
    {
        foreach(var record in magicEffects)
        {
            if(record.EditorID.StartsWith("ENV_"))
            {
                if(resolver.IsEnvironmentalDamage(record) && !record.EditorID.Contains("TEMP") && !record.EditorID.Contains("ENV_DMG_DepleteSoak_ExtremeEnvironment_Effect") && !(record.EditorID == "ENV_ResoreSoak_Effect"))
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
                    if(resolver.IsConditionTargetingDamageSoakForm(condition))
                    {
                        needsPatching = true;
                        break;
                    }
                }
            }

            if(needsPatching)
                PatchSpellHazard(record, GetHazardType(record));
        }
        return this;
    }
    private string GetHazardDamageTypeForExtremeEnvironmentSpell(ISpellGetter spell)
    {
        var editorId = spell.EditorID;
        if(editorId.Contains("Cold"))
            return "Thermal";
        else if (editorId.Contains("Heat"))
            return "Thermal";
        else if (editorId.Contains("Radiation"))
            return "Radiation";
        else if (editorId.Contains("Corrosive"))
            return "Corrosive";
        else if (editorId.Contains("Toxic"))
            return "Airborne";
        else
            throw new Exception("Unknown ExtremeEnvironment Spell: " + editorId);
    }

    private string? GetHazardDamageTypeForEnvironmentSpell(ISpellGetter spell)
    {
        foreach(var effect in spell.Effects)
        {
            var baseEffect = effect.BaseEffect;
            if(!baseEffect.IsNull)
            {
                var effectHazardType = resolver.GetEnvEffectDamageType(baseEffect.FormKey);
                if(effectHazardType != null)
                {
                    return effectHazardType;
                }

            }
        }
        return null;
    }
    private string? GetHazardDamageTypeForSpell(ISpellGetter spell)
    {
        if(spell.EditorID.Contains("ENV_SuppressSoak_Extreme"))
        {
            return GetHazardDamageTypeForExtremeEnvironmentSpell(spell);
        } else
        {
            return GetHazardDamageTypeForEnvironmentSpell(spell);
        }
    }
    private string GetHazardType(ISpellGetter spell)
    {
        string foundHazardType = GetHazardDamageTypeForSpell(spell);
        if(foundHazardType != null) 
            return foundHazardType;
        throw new Exception("Hazard dmg type could not be determined from the spell: Seems no env-hazard magic effects were in place");
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
                if (resolver.IsConditionTargetingDamageSoakForm(condition))
                {
                    resolver.ReplaceConditionTarget(condition, envSoakConditions[hazardType]);
                }

                // Replace the conditions that check if we should start applying damage
                if (resolver.IsConditionApplyEnviornmentalDamage(condition))
                {
                    resolver.ReplaceConditionTarget(condition, envApplyEnvDamageCondition[hazardType]);
                }
            }
            if(resolver.IsExtremeEnvironmentEffect(effect))
            {
                effect.BaseEffect.SetTo(GetExtremeEnvironmentEffectForHazardType(hazardType));
            }
        }
    }

    private FormKey GetExtremeEnvironmentEffectForHazardType(string hazardType)
    {
        return envExtremeMagicEffects[hazardType].FormKey;
    }

    private void PatchTargetingEnvSoak(IMagicEffectGetter record)
    {
        Console.WriteLine("Patching magic effect " + record.EditorID);
        var patch = mod.MagicEffects.GetOrAddAsOverride(record);
        PatchBrokenMagicEffect(patch);
        patch.ActorValue2.SetTo(GetEnvSoakTypedFor(patch));
    }

    private void PatchBrokenMagicEffect(MagicEffect effect)
    {
        if(effect.EditorID == "ENV_DMG_Thermal_Water_Heat_Soak_Effect" || effect.EditorID == "ENV_DMG_Thermal_Weather_Soak_Effect")
        {
            effect.ResistValue.SetTo(resolver.ENV_Resist_Thermal_FormKey);
        }
    }

    private IActorValueInformationGetter GetEnvSoakTypedFor(IMagicEffectGetter record)
    {
        var type = resolver.GetEnvEffectDamageType(record);
        return envSoakRecords[type];
    }
    public void DebugPrint()
    {
        Console.WriteLine("Patched hazards:");
        var linkCache = mod.ToImmutableLinkCache();
        foreach(var spell in mod.Spells)
        {
            Console.WriteLine(spell.EditorID);
            foreach(var spellEffectRef in spell.Effects)
            {
                if(linkCache.TryResolve<IMagicEffectGetter>(spellEffectRef.BaseEffect.FormKey, out var spellEffect)) {
                    var spellEffectTarget = linkCache.Resolve<IActorValueInformationGetter>(spellEffect.ActorValue2.FormKey);

                    Console.WriteLine("\t" + spellEffect.EditorID + " -> " + spellEffectTarget.EditorID);
                }
            }
        }
    }
    public void WriteTo(ILoadOrder<IModListingGetter<IStarfieldModGetter>> loadOrder, string path)
    {
        mod.BeginWrite
        .ToPath(Path.Combine(path, mod.ModKey.FileName))
        .WithLoadOrder(loadOrder)
        .Write();
    }
}