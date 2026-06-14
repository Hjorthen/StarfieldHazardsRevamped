using System;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
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
            return new FormKey(new ModKey("Starfield", ModType.Master), 313); //linkCache.ResolveIdentifier<IActorValueInformationGetter>("ENV_Damage_Soak");
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
    private FormKey ENV_Resist_Thermal_FormKey
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
    public IConditionRecordGetter GetSoakDamageConditionRecord()
    {                                         
        return linkCache.Resolve<IConditionRecordGetter>("ENV_CND_DamageSoak");
    }
    public IConditionRecordGetter GetApplyEnvironmentDamageConditionRecord()
    {
        return linkCache.Resolve<IConditionRecordGetter>("ENV_CND_SPELL_ApplyEnvironmentalDamage");
    }

    public bool IsConditionApplyEnviornmentalDamage(IConditionGetter condition)
    {
        return IsConditionOnFormKey(condition, ENV_CND_ApplyEnvironmentalDamage_FormKey);
    }

    public bool IsConditionDamageSoak(IConditionGetter condition)
    {
        return IsConditionOnFormKey(condition, ENV_CND_DamageSoak_FormKey);
    }

    private static bool IsConditionOnFormKey(IConditionGetter condition, FormKey key)
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
            if((int)conditionTyped.FirstParameter == key.ID) {
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
            conditionTyped.FirstParameter = (ActorValue)actorValue.FormKey.ID;
        } else
        {
            throw new System.ArgumentException($"{nameof(condition)} was not of type {nameof(IsTrueForConditionFormConditionData)}");
        }
    }

    // Replaces the evaluated ConditionRecord for the given condition
    // Spells targets ConditionForms whereas ConditionForms themselves has Conditions that target values directly (see ReplaceConditionValue overload for actor value)
    public void ReplaceConditionTarget(Condition condition, ConditionRecord newCondition)
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
        }
        throw new NotImplementedException();
    }
    public string GetEnvEffectDamageType(IMagicEffectGetter record)
    {
        return ResistanceToEnvSoakTyped(record.ResistValue);
    }

}