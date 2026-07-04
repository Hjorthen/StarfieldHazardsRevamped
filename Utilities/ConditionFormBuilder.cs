using System;
using System.Collections.Generic;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Starfield;

public class ConditionFormBuilder
{
    private List<Condition> conditions = [];


    public ConditionFormBuilder AddGetValueCondition(IActorValueInformationGetter targetValue, Func<GetValueCondition, Condition> configure)
    {
        var newCondition = configure(GetValueCondition.With(targetValue));
        conditions.Add(newCondition);
        return this;
    }


    public ConditionRecord Build(IStarfieldMod mod, string editorId)
    {
        var newRecord = mod.ConditionRecords.AddNew(editorId);
        newRecord.Conditions.AddRange(conditions);
        return newRecord;
    }
}