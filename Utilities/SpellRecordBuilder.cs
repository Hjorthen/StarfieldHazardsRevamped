using System.Collections.Generic;
using ICSharpCode.SharpZipLib;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;

public class SpellRecordBuilder
{

}


public class MagicEffectSpellEntryBuilder
{
    private List<Condition> effectConditions = new();
    private float? effectMagnitude;
    private IMagicEffectGetter? baseEffect;

    public MagicEffectSpellEntryBuilder AddCondition(Condition condition)
    {
        effectConditions.Add(condition);
        return this;
    }

    public MagicEffectSpellEntryBuilder WithBaseEffect(IMagicEffectGetter effect)
    {
        baseEffect = effect;
        return this;
    }

    public MagicEffectSpellEntryBuilder WithMagnitude(float value)
    {
        effectMagnitude = value; 
        return this;
    }

    public Effect Build()
    {
        System.ArgumentNullException.ThrowIfNull(effectMagnitude);
        System.ArgumentNullException.ThrowIfNull(baseEffect);

        return new Effect()
        {
            Conditions = new Noggog.ExtendedList<Condition>(effectConditions),
            BaseEffect = new FormLinkNullable<IMagicEffectGetter>(baseEffect),
            Data = new EffectData()
            {
                Magnitude = (float)effectMagnitude
            }
        };
    }
}
