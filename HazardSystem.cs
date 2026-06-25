using System;
using System.Collections.Generic;
using Mutagen.Bethesda.Starfield;

public class HazardSystem
{

    private readonly Dictionary<string, ActorValueInformation> hazardActorValues;
    private readonly Dictionary<string, ConditionRecord> hazardDamageSoakCondition;
    private readonly Dictionary<string, ConditionRecord> applyEnvDamageConditions;
    private readonly ConditionRecord soakDamageTakenCondition;
    public IEnumerable<String> HazardTypes => hazardActorValues.Keys;

    public HazardSystem(Dictionary<string, ActorValueInformation> hazardActorValues, Dictionary<string, ConditionRecord> hazardDamageSoakCondition, Dictionary<string, ConditionRecord> applyEnvDamageConditions, ConditionRecord soakDamageTakenCondition)
    {
        this.hazardActorValues = hazardActorValues;
        this.hazardDamageSoakCondition = hazardDamageSoakCondition;
        this.applyEnvDamageConditions = applyEnvDamageConditions;
        this.soakDamageTakenCondition = soakDamageTakenCondition;
    }

    public IConditionRecordGetter GetApplyEnvDamageCondition(string hazardType)
    {
        return applyEnvDamageConditions[hazardType];
    }

    // Returns a record that is true if ANY Soak values are damaged
    public IConditionRecordGetter GetSoakDamagedCondition()
    {
        return soakDamageTakenCondition;
    }

    public IConditionRecordGetter GetSoakCondition(string hazardType)
    {
        return hazardDamageSoakCondition[hazardType];
    }
    public IActorValueInformationGetter GetSoakAV(string hazardType)
    {
        return hazardActorValues[hazardType];
    }
}