using System;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;


public abstract class ConditionBuilder<T> where T : ConditionBuilder<T>
{
    private CompareOperator compareOperator;
    private Condition.Flag conditionFlag = 0;

    public T GreaterThan()
    {
        compareOperator = CompareOperator.GreaterThan;
        return (T)this;
    }
    public T LessThan()
    {
        compareOperator = CompareOperator.LessThan;
        return (T)this;
    }

    public T EqualsTo()
    {
        compareOperator = CompareOperator.EqualTo;
        return (T)this;
    }

   public Condition ValueOr(float targetValue)
    {
        conditionFlag = Condition.Flag.OR;
        return Value(targetValue);
    } 

    protected abstract ConditionData CreateData();

    public Condition Value(float targetValue)
    {
        return new ConditionFloat()
        {
            CompareOperator = compareOperator,
            Flags = conditionFlag,
            Data = CreateData(),
            ComparisonValue = targetValue
        };
    }
}

public class HasMagicEffect : ConditionBuilder<HasMagicEffect>
{
    private IMagicEffectGetter parameterValue;

    private HasMagicEffect(IMagicEffectGetter parameterValue)
    {
        this.parameterValue = parameterValue;
    }

    public static HasMagicEffect With(IMagicEffectGetter magicEffect)
    {
        return new HasMagicEffect(magicEffect);
    }

    protected override ConditionData CreateData()
    {
        var data = new HasMagicEffectConditionData();
        data.FirstParameter.Link.SetTo(parameterValue.FormKey);
        return data;
    }
}

public class GetConditionFormCondition : ConditionBuilder<GetConditionFormCondition>
{
    private IConditionRecordGetter conditionForm;

    public static GetConditionFormCondition With(IConditionRecordGetter conditionRecord)
    {
        return new GetConditionFormCondition(conditionRecord);
    }

    private GetConditionFormCondition(IConditionRecordGetter targetForm)
    {
        conditionForm = targetForm;
    }

    protected override ConditionData CreateData()
    {
        var data = new IsTrueForConditionFormConditionData();
        data.FirstParameter.Link.SetTo(conditionForm);
        return data;
    }
}

public class GetValueCondition : ConditionBuilder<GetValueCondition>
{
    private FormLink<IActorValueInformationGetter> parameterValue;

    public static GetValueCondition With(IActorValueInformationGetter targetValue)
    {
        return With(new FormLink<IActorValueInformationGetter>(targetValue));
    }

    public static GetValueCondition With(FormLink<IActorValueInformationGetter> targetValue)
    {
        return new GetValueCondition(targetValue);
    }

    protected override ConditionData CreateData()
    {
        return new GetValueConditionData()
        {
            FirstParameter = parameterValue
        };

    }

    private GetValueCondition(FormLink<IActorValueInformationGetter> targetValue)
    {
        this.parameterValue = targetValue;
    }
}