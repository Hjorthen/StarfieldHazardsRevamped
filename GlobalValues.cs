using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Starfield;

public record GlobChange (IFormLinkGetter<Global> formLink, string editorId, float globalValue);


public class ChangedGlobCollection : IEnumerable<GlobChange>
{
    private List<GlobChange> collection = [];


    public void Add(GlobChange change)
    {
        collection.Add(change);
    }

    public IEnumerator<GlobChange> GetEnumerator()
    {
        return collection.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

// In the base game, damaging magic effects are affected by resistances
public class EnvDamageSettings
{
    public float? ENV_Weather_Mag_NumConcurrentEffects_1 { get; init; } = (0.75f / 1) * MathF.Pow(1.2f, 0);
    public float? ENV_Weather_Mag_NumConcurrentEffects_2 { get; init; } = (0.75f / 2) * MathF.Pow(1.2f, 1);
    public float? ENV_Weather_Mag_NumConcurrentEffects_3 { get; init; } = (0.75f / 3) * MathF.Pow(1.2f, 2);
    public float? ENV_Weather_Mag_NumConcurrentEffects_4 { get; init; } = (0.75f / 4) * MathF.Pow(1.2f, 3);

    // Is this for the soak or health damage? I dont remember..
    public float? ENV_Weather_Mag_Soak_NumConcurrentEffects_1 { get; init; } = (3.5f / 1) * MathF.Pow(1.2f, 0);
    public float? ENV_Weather_Mag_Soak_NumConcurrentEffects_2 { get; init; } = (3.5f / 2) * MathF.Pow(1.2f, 1);
    public float? ENV_Weather_Mag_Soak_NumConcurrentEffects_3 { get; init; } = (3.5f / 3) * MathF.Pow(1.2f, 2);
    public float? ENV_Weather_Mag_Soak_NumConcurrentEffects_4 { get; init; } = (3.5f / 4) * MathF.Pow(1.2f, 3);

    // Used for environmental hazards like gas vents. Spell type: Spell and is applied with a frequency defined on the Hazard records (3 times /s)
    public float? ENV_Hazard_Mag_Dmg_Standard { get; init; } = 4f;
    public float? ENV_Hazard_Mag_Soak_Standard { get; init; } = 1.5f;

    // AppliedSpell dont seem to be used?
    public float? ENV_AppliedSpell_Dur_Momentary_Soak { get; init; } = 0;
    public float? ENV_AppliedSpell_Mag_Momentary_Soak_RATIO { get; init; } = 2.0f;
    public float? ENV_AppliedSpell_Dur_Momentary { get; init; } = 1.0f;
    public float? ENV_AppliedSpell_Mag_Momentary { get; init; } = 10f;
    public float? ENV_AppliedSpell_Dur_Lingering_Soak { get; init; } = 3.0f;
    public float? ENV_AppliedSpell_Dur_Lingering { get; init; } = 5.0f;
    public float? ENV_AppliedSpell_Mag_Lingering { get; init; } = 20.0f;
    public float? ENV_AppliedSpell_Mag_Lingering_Soak_RATIO { get; init; } = 1.0f;


    //  Values for Extreme Hazards Soak Damage
    public float? PEO_EnvironmentalDamage_Mag_NumConcurrentEffects_1 { get; init; } = (0.2f / 1);
    public float? PEO_EnvironmentalDamage_Mag_NumConcurrentEffects_2 { get; init; } = (0.2f / 2) * MathF.Pow(1.2f, 1);
    public float? PEO_EnvironmentalDamage_Mag_NumConcurrentEffects_3 { get; init; } = (0.2f / 3) * MathF.Pow(1.2f, 2);
    public float? PEO_EnvironmentalDamage_Mag_NumConcurrentEffects_4 { get; init; } = (0.2f / 4) * MathF.Pow(1.2f, 3);

    public static ChangedGlobCollection Apply(IStarfieldMod mod, ILinkCache linkCache)
    {   
        var instance = new EnvDamageSettings();
        var changeCollection = new ChangedGlobCollection();

        // Using a bit of reflection to iterate through the properties and apply them to the mod
        foreach(var global in typeof(EnvDamageSettings).GetProperties())
        {
            var formLink = linkCache.Resolve<IGlobalGetter>(global.Name);
            var globOverride = mod.Globals.GetOrAddAsOverride(formLink);
            var newValue =  (float)global.GetValue(instance)!;

            globOverride.Data = newValue;
            changeCollection.Add(new GlobChange(globOverride.ToLinkGetter(), global.Name, newValue));
        }

        return changeCollection;
    }
}


