using System;
using System.Collections.Generic;
using System.Linq;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Starfield;
using Noggog;

public class HazardWorldspacePatcher
{
    private readonly HazardsMapper mapper;
    private readonly StarfieldMod outputMod; 
    private readonly HazardSystem hazardSystem;
    private readonly ILinkCache<IStarfieldMod, IStarfieldModGetter> linkCache;

    private HazardWorldspacePatcher(HazardSystem hazardSystem, StarfieldMod outputMod, HazardsMapper mapper, ILinkCache<IStarfieldMod, IStarfieldModGetter> baseGameLinkCache)
    {
        this.hazardSystem = hazardSystem;
        this.outputMod = outputMod;
        this.mapper = mapper;
        this.linkCache = baseGameLinkCache;
    }

    public static void WritePatch(StarfieldMod outputMod, HazardSystem hazardSystem, HazardsMapper mapper, ILinkCache<IStarfieldMod, IStarfieldModGetter> linkCache)
    {
        var patcher = new HazardWorldspacePatcher(hazardSystem, outputMod, mapper, linkCache);
        patcher.Patch();
    }

    private void Patch()
    {
        var hazards = linkCache.WinningOverrides<IHazardGetter>();
        PatchGasVents(hazards);

    }

    private void PatchGasVents(IEnumerable<IHazardGetter> hazards)
    {
        string[] gasVentHazardIds = [
            "ENV_Hazard_PK_Vent_Gas_Corrosive_HazardAmbient_01",
            "ENV_Hazard_PK_Vent_Gas_ToxicGas_HazardAmbient_01",
            "ENV_Hazard_PK_Vent_Gas_Radioactive_HazardAmbient_01"
        ];

        foreach (var ventPakin in gasVentHazardIds)
        {
            var ventPrefab = linkCache.Resolve<IPackInGetter>(ventPakin);
            var prefabCell = linkCache.Resolve(ventPrefab.Cell);
            foreach(var placed in prefabCell.Temporary)
            {
                if(placed is IPlacedHazardGetter hazardObj) {
                    var baseHazard = linkCache.Resolve(hazardObj.Hazard);
                    PatchIncreaseGasVentRange(outputMod.Hazards.GetOrAddAsOverride(baseHazard));
                    
                } 
                else if (placed is IPlacedObjectGetter placedObj) {
                    // There can be more than one placed object, so we check if we got the right one..
                    if (linkCache.TryResolve<IMoveableStaticGetter>(placedObj.Base.FormKey, out var baseObject) && baseObject.EditorID.StartsWith("Hazard_")) {
                        // We got the right object, now all we need to do is move it
                        var context = linkCache.ResolveContext<IPlacedObject, IPlacedObjectGetter>(placedObj.FormKey);
                        var newPlaced = context.GetOrAddAsOverride(outputMod);
                        var basePosition = newPlaced.Position;
                        // Move it a bit higher such it becomes more visible
                        basePosition.Z = 1;
                        newPlaced.Position = basePosition;
                    }
                }
            }
        }
    }

    private void PatchIncreaseGasVentRange(Hazard hazard)
    {
        // Damage too strong, n ot sure if "Taper" actually works..
        // Maybe reduce "Target Interval" such that the spell is applied a bit less frequently?
        hazard.Radius = 22;
        hazard.Flags |= Hazard.Flag.TaperEffectivenessByProximity;
        hazard.TaperFullEffectRadius = 5;
        hazard.TaperCurse = 4;
        hazard.TaperWeight = 1;
    }
}