using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;

public class GetValueCondition
{
    private IActorValueInformationGetter parameterValue;
    private CompareOperator compareOperator;
    private Condition.Flag conditionFlag = 0;


    public static GetValueCondition With(IActorValueInformationGetter targetValue)
    {
        return new GetValueCondition(targetValue);
    }
    private GetValueCondition(IActorValueInformationGetter targetValue)
    {
        this.parameterValue = targetValue;
    }

    public GetValueCondition LessThan()
    {
        compareOperator = CompareOperator.LessThan;
        return this;
    }

    public GetValueCondition EqualsTo()
    {
        compareOperator = CompareOperator.EqualTo;
        return this;
    }

   public Condition ValueOr(float targetValue)
    {
        conditionFlag = Condition.Flag.OR;
        return Value(targetValue);
    } 

    public Condition Value(float targetValue)
    {
        return new ConditionFloat()
        {
            CompareOperator = compareOperator,
            Data = new GetValueConditionData()
            {
                FirstParameter = new FormLink<IActorValueInformationGetter>(parameterValue)
            },
            ComparisonValue = targetValue
        }
    }
}