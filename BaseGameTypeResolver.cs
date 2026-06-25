using System;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;

public class BaseGameTypeResolver
{
    private FormKey ENV_CND_DamageSoak_FormKey
    {
        get
        {
            return GetSoakDamageConditionRecord().FormKey;
        }
    }
    private FormKey ENV_CND_ApplyEnvironmentalDamage_FormKey
    {
        get
        {
            return GetApplyEnvironmentDamageConditionRecord().FormKey;
        }
    }
    private FormKey ENV_Damage_Soak_FormKey
    {
        get
        {
            return new FormKey(new ModKey("Starfield", ModType.Master), 787); //linkCache.ResolveIdentifier<IActorValueInformationGetter>("ENV_Damage_Soak");
        }
    }

    private FormKey ENV_Resist_Corrosive_FormKey
    {
        get
        {
            return linkCache.ResolveIdentifier<IActorValueInformationGetter>("ENV_Resist_Corrosive");
        }
    }
    private FormKey ENV_Resist_Airborne_FormKey
    {
        get
        {
            return linkCache.ResolveIdentifier<IActorValueInformationGetter>("ENV_Resist_Airborne");
        }
    }
    private FormKey ENV_Resist_Radiation_FormKey
    {
        get
        {
            return linkCache.ResolveIdentifier<IActorValueInformationGetter>("ENV_Resist_Radiation");
        }
    }
    private FormKey ENV_DMG_DepleteSoak_ExtremeEnvironment_Effect_FormKey
    {
        get
        {
            return linkCache.ResolveIdentifier<IActorValueInformation>("ENV_DMG_DepleteSoak_ExtremeEnvironment_Effect");
        }
    }
    public FormKey ENV_Resist_Thermal_FormKey
    {
        get
        {
            return linkCache.ResolveIdentifier<IActorValueInformationGetter>("ENV_Resist_Thermal");
        }
    }
    private readonly ILinkCache<IStarfieldMod, IStarfieldModGetter> linkCache;

    public BaseGameTypeResolver(ILinkCache<IStarfieldMod, IStarfieldModGetter> linkCache)
    {
        this.linkCache = linkCache;
    }
    public ISpellGetter GetRestoreSoakAbility()
    {
        return linkCache.Resolve<ISpellGetter>("ENV_RestoreSoak_Ability");
    }
    public IMagicEffectGetter GetExtremeEnvironmentEffect()
    {
        return linkCache.Resolve<IMagicEffectGetter>(ENV_DMG_DepleteSoak_ExtremeEnvironment_Effect_FormKey);
    }
    public IMagicEffectGetter GetRestoreSoakMagicEffectRecord()
    {
        return linkCache.Resolve<IMagicEffectGetter>("ENV_ResoreSoak_Effect");
    }
    public IConditionRecordGetter GetSoakDamageConditionRecord()
    {                                         
        return linkCache.Resolve<IConditionRecordGetter>("ENV_CND_DamageSoak");
    }
    public IConditionRecordGetter GetSoakRestoreConditionRecord()
    {                                         
        return linkCache.Resolve<IConditionRecordGetter>("ENV_CND_RestoreSoak");
    }
    public IConditionRecordGetter GetApplyEnvironmentDamageConditionRecord()
    {
        return linkCache.Resolve<IConditionRecordGetter>("ENV_CND_SPELL_ApplyEnvironmentalDamage");
    }
    public FormKey GetDamageTypeKeyword(string hazardType)
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
    public FormKey GetKeywordRadiation()
    {
        return linkCache.Resolve<IKeywordGetter>("ENV_EnvDamageType_Radiation").FormKey;
    }
    public FormKey GetKeywordAirborne()
    {
        return linkCache.Resolve<IKeywordGetter>("ENV_EnvDamageType_Airborne").FormKey;
    }
    public FormKey GetKeywordCorrosive()
    {
        return linkCache.Resolve<IKeywordGetter>("ENV_EnvDamageType_Corrosive").FormKey;
    }
    public FormKey GetKeywordThermal()
    {
        return linkCache.Resolve<IKeywordGetter>("ENV_EnvDamageType_Radiation").FormKey;
    }

    public bool IsConditionApplyEnviornmentalDamage(IConditionGetter condition)
    {
        return IsConditionTargetingFormKey(condition, ENV_CND_ApplyEnvironmentalDamage_FormKey);
    }

    public bool IsConditionTargetingDamageSoak(IConditionGetter condition)
    {
        return IsConditionTargetingFormKey(condition, ENV_Damage_Soak_FormKey);
    }
    public bool IsConditionTargetingDamageSoakForm(IConditionGetter condition)
    {
        return IsConditionTargetingFormKey(condition, ENV_CND_DamageSoak_FormKey);
    }

    private static bool IsConditionTargetingFormKey(IConditionGetter condition, FormKey key)
    {

        if(condition.Data is IsTrueForConditionFormConditionData conditionForm)
        {
            var conditionFormKey = conditionForm.FirstParameter.Link;
            if(conditionFormKey.FormKey == key)
            {
                return true;
            }
        } else if (condition.Data is GetValueConditionData conditionTyped)
        {
            if(conditionTyped.FirstParameter.FormKey == key) {
                return true;
            }

        } else if (condition.Data is GetValuePercentConditionData percentageCondition)
        {
            if(percentageCondition.FirstParameter.FormKey == key)
            {
                return true;
            }
        }
        return false;
    }

    // Replaces the evaluated actor value for the given condition
    public void ReplaceConditionValue(Condition condition, ActorValueInformation actorValue)
    {
        if(condition.Data is GetValueConditionData conditionTyped)
        {
            conditionTyped.FirstParameter.SetTo(actorValue.FormKey);
        } else
        {
            throw new System.ArgumentException($"{nameof(condition)} was not of type {nameof(IsTrueForConditionFormConditionData)}");
        }
    }

    // Replaces the evaluated ConditionRecord for the given condition
    // Spells targets ConditionForms whereas ConditionForms themselves has Conditions that target values directly (see ReplaceConditionValue overload for actor value)
    public void ReplaceConditionTarget(Condition condition, IConditionRecordGetter newCondition)
    {
        if(condition.Data is IsTrueForConditionFormConditionData conditionForm)
        {
            var conditionFormKey = conditionForm.FirstParameter.Link;
            conditionFormKey.FormKey = newCondition.FormKey;
        } else
        {
            throw new System.ArgumentException($"{nameof(condition)} was not of type {nameof(IsTrueForConditionFormConditionData)}");
        }
    }

    public bool IsEnvironmentalDamage(IMagicEffectGetter magicEffect)
    {
        // Target is stored as ActorValue2
        var actorValue2 = magicEffect.ActorValue2;
        return actorValue2.FormKey == ENV_Damage_Soak_FormKey;
    }

    public string? GetEnvEffectDamageType(IFormKeyGetter link)
    {
        if(linkCache.TryResolve<IMagicEffectGetter>(link.FormKey, out IMagicEffectGetter mf))
            return GetEnvEffectDamageType(mf);
        return null;
    }
    public string? GetEnvEffectDamageType(IMagicEffectGetter record)
    {
        if(!record.ResistValue.IsNull)
            return ResistanceToEnvSoakTyped(record.ResistValue);
        return EditorIdToEnvSoakTyped(record.EditorID);
    }

    private string? EditorIdToEnvSoakTyped(string editorId)
    {
        if(editorId.Contains("Airborne"))
            return "Airborne";
        else if (editorId.Contains("Shock"))
            return "Radiation";
        return null;
    }

    private string ResistanceToEnvSoakTyped(IFormLinkGetter<IActorValueInformationGetter> resistValue)
    {
        if(resistValue.FormKey == ENV_Resist_Corrosive_FormKey)
        {
            return "Corrosive";
        } else if (resistValue.FormKey == ENV_Resist_Airborne_FormKey)
        {
            return "Airborne";
        } else if (resistValue.FormKey == ENV_Resist_Radiation_FormKey)
        {
            return "Radiation";
        } else if (resistValue.FormKey == ENV_Resist_Thermal_FormKey)
        {
            return "Thermal";
        } else if (resistValue.FormKey.IsNull)
        {
            return "Thermal";
        }
        throw new NotImplementedException();
    }

    public bool IsExtremeEnvironmentEffect(IEffectGetter effect)
    {
        return effect.BaseEffect.FormKey == ENV_DMG_DepleteSoak_ExtremeEnvironment_Effect_FormKey;
    }
}