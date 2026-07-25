using System;
using System.Collections.Generic;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Starfield;

public class HazardsSystemSpellsPatcher
{
    private readonly HazardsMapper mapper;
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
        var winningRecords = env.LoadOrder.PriorityOrder; 


        PatchMagicEffects(winningRecords.MagicEffect().WinningOverrides());
        PatchSpellHazards(winningRecords.Spell().WinningOverrides());

    }

    private IActorValueInformationGetter AddNewEnvironmentalDamageResistanceValue()
    {
        var av = outputMod.ActorValueInformation.AddNew("HaOS_Env_EnvDmg_Resist");
        av.Flags = ActorValueInformation.Flag.Percentage;
        av.DefaultValue = 0.0f;
        av.Type = ActorValueInformation.Types.Resistance;
        av.Min = 0.0f;
        av.Max = 25;

        return av;
    }



    public void PatchMagicEffects(IEnumerable<IMagicEffectGetter> magicEffects)
    {
        var envDmgResist = AddNewEnvironmentalDamageResistanceValue();

        foreach(var record in magicEffects)
        {
            if(IsEnvDmgSoakEffect(record))
            {
                PatchTargetingEnvSoak(record);
            }
            else if (IsEnvDmgHealthEffect(record))
            {
                PatchEffectResistance(record, envDmgResist);
            }
        }
    }

    private void PatchEffectResistance(IMagicEffectGetter record, IActorValueInformationGetter envDmgResist)
    {
        Console.WriteLine("Patching environmental damage effect: " + record.EditorID);
        var patch = outputMod.MagicEffects.GetOrAddAsOverride(record);
        patch.ResistValue = envDmgResist.ToLink();
    }

    private bool IsEnvDmgSoakEffect(IMagicEffectGetter record)
    {
        // Seems Bethesda has been good at naming all environmental effects with ENV_ prefix
        if(record.EditorID.StartsWith("ENV_"))
        {
            if (record.EditorID.Contains("TEMP") || record.EditorID.Contains("ENV_DMG_DepleteSoak_ExtremeEnvironment_Effect") || record.EditorID == "ENV_ResoreSoak_Effect")
                return false;


            if(resolver.IsSoakDamage(record))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsEnvDmgHealthEffect(IMagicEffectGetter record)
    {
        if(record.EditorID.StartsWith("ENV_"))
        {
            if (record.EditorID.Contains("OBSOLETE"))
                return false;

            if(resolver.IsEnvDamage(record))
            {
                return true;
            }
        }
        return false;
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
        List<Effect> environmentalDamageEffects = new();
        Console.WriteLine("Patching spell " + record.EditorID);
        var patch = outputMod.Spells.GetOrAddAsOverride(record);

        // Keyword is needed by the scaling resistance damage perk. 
        patch.Keywords.Add(resolver.GetDamageTypeKeyword(hazardType));
        List<Effect> extremeSoakSuppressEffects = new();
        foreach(var effect in patch.Effects)
        {
            bool effectTypeEnvironmentalDamage = false;
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
                    effectTypeEnvironmentalDamage = true;
                }
            }
            if(effectTypeEnvironmentalDamage)
            {
                environmentalDamageEffects.Add(effect);
            }

            if(patch.EditorID.StartsWith("ENV_SuppressSoak_Extreme"))
            {
                if(resolver.IsExtremeEnvironmentEffect(effect))
                {
                    // ENV_SuppressSoak purpose is to completely remove all soak, ignoring resistances completely. That's no fun so we remove it.
                    extremeSoakSuppressEffects.Add(effect);

                }
            }
        }
        // Remove all suppress soak effects
        foreach (var effect in extremeSoakSuppressEffects)
        {
            patch.Effects.Remove(effect);   
        }
    }

    private void PatchTargetingEnvSoak(IMagicEffectGetter record)
    {
        Console.WriteLine("Patching magic effect " + record.EditorID);
        var patch = outputMod.MagicEffects.GetOrAddAsOverride(record);
        // Some spells has the wrong resistance, we patch that here..
        PatchBrokenMagicEffect(patch);
        patch.ActorValue2.SetTo(GetEnvSoakTypedFor(patch));
    }

    // Some magic effects are using the wrong resistance values
    // we patch them here.
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
}
