using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Starfield;

// In the base game, damaging magic effects are affected by resistances
public class EnvDamageSettings
{
    public float? ENV_Weather_Mag_NumConcurrentEffects_1 { get; init; } = 0.75f;
    public float? ENV_Weather_Mag_NumConcurrentEffects_2 { get; init; } = 0.4875f;
    public float? ENV_Weather_Mag_NumConcurrentEffects_3 { get; init; } = 0.3625f;
    public float? ENV_Weather_Mag_NumConcurrentEffects_4 { get; init; } = 0.3f;
    public float? ENV_Weather_Mag_Soak_NumConcurrentEffects_1 { get; init; } = 1f;
    public float? ENV_Weather_Mag_Soak_NumConcurrentEffects_2 { get; init; } = 0.65f;
    public float? ENV_Weather_Mag_Soak_NumConcurrentEffects_3 { get; init; } = 0.48333f;
    public float? ENV_Weather_Mag_Soak_NumConcurrentEffects_4 { get; init; } = 0.4f;
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

    private EnvDamageSettings() {}

    public static void Apply(IStarfieldMod mod, ILinkCache linkCache)
    {   
        var instance = new EnvDamageSettings();

        // Using a bit of reflection to iterate through the properties and apply them to the mod
        foreach(var global in typeof(EnvDamageSettings).GetProperties())
        {
            var formKey = linkCache.Resolve<IGlobalGetter>(global.Name);
            var valueOverride = mod.Globals.GetOrAddAsOverride(formKey);
            valueOverride.Data = (float)global.GetValue(instance)!;
        }
    }
}


