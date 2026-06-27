using System;
using System.Collections.Generic;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Cache.Internals.Implementations;
using Mutagen.Bethesda.Starfield;
public struct EditorId<T>
{
    private string editorId;

    public EditorId(string editorId)
    {
        this.editorId = editorId;
    }
    public static implicit operator string(EditorId<T> id) => id.editorId;
}
public class HazardSystem
{

    private readonly Dictionary<string, EditorId<ActorValueInformation>> hazardToDamageSoakValue;
    private readonly Dictionary<string, EditorId<ConditionRecord>> hazardToDamageSoakCondition;
    private readonly Dictionary<string, EditorId<ConditionRecord>> hazardToApplyEnvDamageCondition;
    private readonly Dictionary<string, EditorId<ActorValueInformation>> hazardToResistance;
    private readonly EditorId<ConditionRecord> soakDamageTakenCondition;
    private ILinkCache<IStarfieldMod, IStarfieldModGetter> linkCache;
    public IEnumerable<String> HazardTypes => hazardToDamageSoakValue.Keys;

    public HazardSystem(Dictionary<string, EditorId<ActorValueInformation>> hazardToDamageSoakValue, Dictionary<string, EditorId<ConditionRecord>> hazardToDamageSoakCondition, Dictionary<string, EditorId<ConditionRecord>> hazardToApplyEnvDamageCondition, Dictionary<string, EditorId<ActorValueInformation>> hazardToResistance, EditorId<ConditionRecord> soakDamageTakenCondition)
    {
        this.hazardToDamageSoakValue = hazardToDamageSoakValue;
        this.hazardToDamageSoakCondition = hazardToDamageSoakCondition;
        this.hazardToApplyEnvDamageCondition = hazardToApplyEnvDamageCondition;
        this.hazardToResistance = hazardToResistance;
        this.soakDamageTakenCondition = soakDamageTakenCondition;
    }

    // Sets the LinkCache that is used for looking up specific types
    public void SetLinkCache(ILinkCache<IStarfieldMod, IStarfieldModGetter> linkCache)
    {
        this.linkCache = linkCache;
    }

    public IConditionRecordGetter GetApplyEnvDamageCondition(string hazardType)
    {
        return linkCache.Resolve<IConditionRecordGetter>(hazardToApplyEnvDamageCondition[hazardType]);
    }

    // Returns a record that is true if ANY Soak values are damaged
    public IConditionRecordGetter GetSoakDamagedCondition()
    {
        return linkCache.Resolve<IConditionRecordGetter>(soakDamageTakenCondition);
    }

    public IConditionRecordGetter GetSoakCondition(string hazardType)
    {
        return linkCache.Resolve<IConditionRecordGetter>(hazardToDamageSoakCondition[hazardType]);
    }

    public IActorValueInformationGetter GetResistanceAV(string hazardType)
    {
        return linkCache.Resolve<IActorValueInformationGetter>(hazardToResistance[hazardType]);
    }

    public IActorValueInformationGetter GetSoakAV(string hazardType)
    {
        return linkCache.Resolve<IActorValueInformationGetter>(hazardToDamageSoakValue[hazardType]);
    }
}