
using HazardOverhaul.Builders;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Starfield;

public class HazardSystemLevelListPatcher
{
    private readonly HazardSystem hazardSystem;
    private readonly StarfieldMod outputMod;
    private readonly ILinkCache<IStarfieldMod, IStarfieldModGetter> baseGameLinkCache;

    private HazardSystemLevelListPatcher(HazardSystem hazardSystem, StarfieldMod outputMod, ILinkCache<IStarfieldMod, IStarfieldModGetter> baseGameLinkCache)
    {
        this.hazardSystem = hazardSystem;
        this.outputMod = outputMod;
        this.baseGameLinkCache = baseGameLinkCache;
    }

    public static void WritePatch(HazardSystem hazardSystem, StarfieldMod outputMod, ILinkCache<IStarfieldMod, IStarfieldModGetter> baseGameLinkCache)
    {
        var patcher = new HazardSystemLevelListPatcher(hazardSystem, outputMod, baseGameLinkCache);
        patcher.PatchInternal();
    }

    private void PatchInternal()
    {
        var builder = LeveledItemBuilder.Create().CalculateFromAllLevels(true).CalculateForEachItemInCount(true);

        foreach(var item in new[]{"HS_Item_Restore_Radiation", "HS_Item_Restore_Thermal", "HS_Item_Restore_Corrosive","HS_Item_Restore_Airborne"})
        {
            var ingestible = baseGameLinkCache.Resolve<IIngestibleGetter>(item);
            builder.AddEntry(ingestible, 1, 1);
        }

        var randomRestoreLevelItem = builder.Build(outputMod, "HaOS_LLS_Aid_RestoreSoak");
        
        builder = LeveledItemBuilder.Create().CalculateFromAllLevels(true).CalculateForEachItemInCount(true);
        builder.AddEntry(randomRestoreLevelItem);
        

        // Outlands and other stores
        Inject("LL_Vendor_Outfitter_AidChems_50", builder);
        // List that seems to spawn weapons in containers found here and there..
        Inject("LL_Loot_Legendary_Human_Rank_1", builder);
        

        //var radiationRestoreMf = AddSoakRestoreMagicEffect("Radiation", "HS_Restore_Radiation_Soak", "", "Restore <mag> of radiation suit integrity");
        //AddItem("Radiation Mesh Shield", "HS_Item_Restore_Radiation", "A lead-alloy mesh designed to soak up radiation, its nano-crystalline structure keeps excess weight to a minimum.", radiationRestoreMf, new RadiationMesh());
        //var thermalRestoreMf = AddSoakRestoreMagicEffect("Thermal", "HS_Restore_Radiation_Soak", "", "Restore <mag> of thermal suit integrity");
        //AddItem("Cryo Battery Pack", "HS_Item_Restore_Thermal", "A combined coolant and battery replacement for suit thermal regulators. Engineered to keep its charge under extreme heat - so you can keep your cool.", thermalRestoreMf, new ItemBatteryPack());
        //var corrosiveRestoreMf = AddSoakRestoreMagicEffect("Corrosive", "HS_Restore_Corrosive_Soak", "", "Restore <mag> of corrosive suit integrity");
        //AddItem("Deimos CorroGuard", "HS_Item_Restore_Corrosive", "A thick, flowing paste that provides a protective coating. Wear proper protective equipment when applied. May stain materials. Seek nearest medical facility if inhaled.", corrosiveRestoreMf, new BucketOfPaste());
        //var airborneRestoreMf = AddSoakRestoreMagicEffect("Airborne", "HS_Restore_Airborne_Soak", "", "Restore <mag> of airborne suit integrity");
        //AddItem("Outland Airfilter", "HS_Item_Restore_Airborne", "A replacement filter for standard issue spacesuits. Breathe the antartic air of old.", airborneRestoreMf, new AirSieve());
    }

    // Injects the given builder config into an existing record
    void Inject(string editorId, LeveledItemBuilder builder)
    {
        var baseVendorList = baseGameLinkCache.Resolve<ILeveledItemGetter>(editorId);
        var overrideVendorList = outputMod.LeveledItems.GetOrAddAsOverride(baseVendorList);
        builder.Apply(overrideVendorList);
    }
}