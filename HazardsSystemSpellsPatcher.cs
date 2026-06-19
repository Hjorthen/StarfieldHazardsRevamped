using System;
using System.Collections.Generic;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;

public class HazardsSystemSpellsPatcher
{
    private readonly HazardsMapper mapper;
    Dictionary<string, MagicEffect> envExtremeMagicEffects;
    private readonly StarfieldMod outputMod; 
    private readonly BaseGameTypeResolver resolver;
    private readonly HazardSystem hazardSystem;

    public static void WritePatch(StarfieldMod outputMod, HazardSystem hazardSystem, HazardsMapper mapper, BaseGameTypeResolver resolver, IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env)
    {
        var patcher = new HazardsSystemSpellsPatcher(hazardSystem, resolver, outputMod, mapper);
        patcher.PatchInternal(env);
    }

    private HazardsSystemSpellsPatcher(HazardSystem hazardSystem, BaseGameTypeResolver resolver, StarfieldMod outputMod, HazardsMapper mapper)
    {
        this.hazardSystem = hazardSystem;
        this.resolver = resolver;
        this.outputMod = outputMod;
        this.mapper = mapper;
    }

    private void PatchInternal(IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env)
    {
        var restoreSoakMagicEffects = CreateSoakRestoreMagicEffects();
        envExtremeMagicEffects = AddExtremeEnvironmentMagicEffects();
        var winningRecords = env.LoadOrder.PriorityOrder; 
        PatchRestoreSoakAbility(restoreSoakMagicEffects);

        PatchMagicEffects(winningRecords.MagicEffect().WinningOverrides());
        PatchSpellHazards(winningRecords.Spell().WinningOverrides());

    }
    private Dictionary<string, MagicEffect> AddExtremeEnvironmentMagicEffects()
    {
        var effectBase = resolver.GetExtremeEnvironmentEffect();
        var newExtremeMagicEffects = new Dictionary<string, MagicEffect>();
        foreach (var entry in mapper.HazardTypes)
        {
            newExtremeMagicEffects[entry] = CreateExtremeEnvironmentEffect(entry, effectBase);
        }
        return newExtremeMagicEffects;
    }
    private MagicEffect CreateExtremeEnvironmentEffect(string hazardType, IMagicEffectGetter effectBase)
    {
        var effectNew = outputMod.MagicEffects.DuplicateInAsNewRecord(effectBase);
        effectNew.ActorValue2.SetTo(hazardSystem.GetSoakAV(hazardType));

        return effectNew;
    }

    private IEnumerable<MagicEffect> CreateSoakRestoreMagicEffects()
    {
        List<MagicEffect> createdEffects = [];
        var baseMagicEffect = resolver.GetRestoreSoakMagicEffectRecord();
        foreach(var hazardType in mapper.HazardTypes)
        {
            var newMF = outputMod.MagicEffects.DuplicateInAsNewRecord(baseMagicEffect, $"ENV_RestoreSoak_Effect_{hazardType}");
            newMF.Name = "Restore " + hazardType;
            newMF.ActorValue2.SetTo(hazardSystem.GetSoakAV(hazardType));
            createdEffects.Add(newMF);
        }

        return createdEffects;
    }

    // The RestoreSoakAbility is applied by the game whenever a player is in a safe area. 
    // We need it to apply spell effects for all the soak value types.
    private void PatchRestoreSoakAbility(IEnumerable<IMagicEffectGetter> applyEffects)
    {
        var originalRestoreSoakAbility = resolver.GetRestoreSoakAbility();
        var originalEffect = originalRestoreSoakAbility.Effects[0];

        var formOverride = outputMod.Spells.GetOrAddAsOverride(originalRestoreSoakAbility);
        foreach(var newEffect in applyEffects)
        {
            var effect = originalEffect.DeepCopy();
            effect.BaseEffect = new FormLinkNullable<IMagicEffectGetter>(newEffect);
            formOverride.Effects.Add(effect);
        }
    }

    public void PatchMagicEffects(IEnumerable<IMagicEffectGetter> magicEffects)
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
    }

    public void PatchSpellHazards(IEnumerable<ISpellGetter> spells)
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
        var patch = outputMod.Spells.GetOrAddAsOverride(record);
        foreach(var effect in patch.Effects)
        {
            foreach(var condition in effect.Conditions)
            {
                // Replace the conditions that check if we should deteriorate the suit integrity
                if (resolver.IsConditionTargetingDamageSoakForm(condition))
                {
                    resolver.ReplaceConditionTarget(condition, hazardSystem.GetSoakCondition(hazardType));
                }

                // Replace the conditions that check if we should start applying damage
                if (resolver.IsConditionApplyEnviornmentalDamage(condition))
                {
                    resolver.ReplaceConditionTarget(condition, hazardSystem.GetApplyEnvDamageCondition(hazardType));
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
        var patch = outputMod.MagicEffects.GetOrAddAsOverride(record);
        // Some spells has the wrong resistance, we patch that here..
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
        return hazardSystem.GetSoakAV(type);
    }
    public void DebugPrint()
    {
        Console.WriteLine("Patched hazards:");
        var linkCache = outputMod.ToImmutableLinkCache();
        foreach(var spell in outputMod.Spells)
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
}
