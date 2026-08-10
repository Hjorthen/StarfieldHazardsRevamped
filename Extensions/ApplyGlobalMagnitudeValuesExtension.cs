using System;
using System.Collections.Generic;
using System.Linq;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Starfield;

namespace HazardOverhaul.Extensions;

class ApplyGlobalMagnitudeValues
{
    private readonly IGameEnvironment<IStarfieldMod, IStarfieldModGetter> environment;
    private readonly IStarfieldMod outputMod;
    private ILinkCache<IStarfieldMod, IStarfieldModGetter> linkCache;

    public ApplyGlobalMagnitudeValues(IGameEnvironment<IStarfieldMod, IStarfieldModGetter> environment, IStarfieldMod outputMod)
    {
        this.environment = environment;
        this.outputMod = outputMod;
        this.linkCache = environment.LinkCache;
    }


    public void Patch()
    {
        var winningRecords = environment.LoadOrder.PriorityOrder;
        List<ISpellGetter> spellsOfInterest = SelectSpellsWithGlobalMagnitude(winningRecords);

        PatchChangedMagnitudes(spellsOfInterest);
    }

    private void PatchChangedMagnitudes(List<ISpellGetter> spellsOfInterest)
    {
        foreach(var spell in spellsOfInterest)
        {
            Spell mutableSpellRecord = null;
            for (int i = 0; i < spell.Effects.Count; i++)
            {
               var effect = spell.Effects[i]; 
               if(effect.HasGlobalMagnitudeReference())
               {
                    // We assume the link is valid and don't check for null here.
                    float globalEffectMagnitude = linkCache.Resolve(effect.Magnitude).Data!.Value;
                    // We have to patch as the values differ
                    if(globalEffectMagnitude != effect.Data!.Magnitude)
                    {
                        // Only create an override record if we intend to change the values
                        if(mutableSpellRecord == null)
                            mutableSpellRecord = outputMod.Spells.GetOrAddAsOverride(spell);

                        // Update the data with the value stored in the global
                        var overrideData = mutableSpellRecord.Effects[i].Data!;
                        overrideData.Magnitude = globalEffectMagnitude;
                    }
               }
            }
            if(mutableSpellRecord != null)
                Console.WriteLine("Patch spell: " + mutableSpellRecord.EditorID);
        }
    }


    // Get all spells that references a global value for Magnitude
    private static List<ISpellGetter> SelectSpellsWithGlobalMagnitude(IEnumerable<Mutagen.Bethesda.Plugins.Order.IModListingGetter<IStarfieldModGetter>> winningRecords)
    {
        return winningRecords
            .Spell().WinningOverrides()
            .Where(spell => 
                spell.Effects.Any(e => e.HasGlobalMagnitudeReference())
            ).ToList();
    }
}

public static class ApplyGlobalMagnitudeValuesExtensions
{
    /* 
        Starfield uses a feature where the magnitude of spells can be set using a "Global". Problem is the "Global" isn't loaded at runtime
        but rather set by CreationKit when the record is saved. We run through all spells we've changed and update their value here.
    */
    public static bool HasGlobalMagnitudeReference(this IEffectGetter effect) => effect.Magnitude.IsNull == false;
    // Updates spell magnitudes to match with their set Global Magnitude
    public static void  RefreshSpellGlobalMagnitudes(this IStarfieldMod outputMod, IGameEnvironment<IStarfieldMod, IStarfieldModGetter> environment)
    {
        new ApplyGlobalMagnitudeValues(environment, outputMod).Patch();
    }
}