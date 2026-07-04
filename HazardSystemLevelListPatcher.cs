
using System.Collections;
using System.Collections.Generic;
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
        string[] majorEditorIds = new string[]
        {
            "HaOS_Item_Restore_Major_Radiation",
            "HaOS_Item_Restore_Major_Thermal",
            "HaOS_Item_Restore_Major_Corrosive",
            "HaOS_Item_Restore_Major_Airborne"
        };

        string[] minorEditorIds = new string[]
        {
            "HaOS_Item_Restore_Minor_Radiation",
            "HaOS_Item_Restore_Minor_Thermal",
            "HaOS_Item_Restore_Minor_Corrosive",
            "HaOS_Item_Restore_Minor_Airborne"
        };

        AddMajorAidItems(majorEditorIds);
        AddMinorAidItems(minorEditorIds);
    }

    private void AddMinorAidItems(IEnumerable<string> minorAidItemList)
    {
        var builder = LeveledItemBuilder.Create().CalculateForEachItemInCount(true).CalculateFromAllLevels(true);
        foreach (var item in minorAidItemList)
        {
            var ingestible = baseGameLinkCache.Resolve<IIngestibleGetter>(item);
            // 1-2 each pick for variation, 1.5 average
            builder.AddEntry(ingestible, 1, 1);
            builder.AddEntry(ingestible, 1, 2);
        }
        var randomRestoreLevelItem = builder.Build(outputMod, "HaOS_LLS_Aid_RestoreSoak_Minor");

        // Small chance for lots at the vendor
        builder = LeveledItemBuilder.Create().CalculateForEachItemInCount(true).CalculateFromAllLevels(true).WithChanceNone(50);
        builder.AddEntry(randomRestoreLevelItem, count: 5);
        var paydayItemList = builder.Build(outputMod, "HaOS_LLS_Aid_BonusItems_50");

        // Chance for extras at a vendor
        builder = LeveledItemBuilder.Create().CalculateForEachItemInCount(true).CalculateFromAllLevels(true).WithChanceNone(40);
        builder.AddEntry(randomRestoreLevelItem, count: 3);
        builder.AddEntry(paydayItemList, count: 1);
        var extraItemsChanceList = builder.Build(outputMod, "HaOS_LLS_Aid_BonusItems_40");

        builder = LeveledItemBuilder.Create().SetFlag(LeveledItem.Flag.UseAll, true);
        builder.AddEntry(randomRestoreLevelItem, count: 4);
        builder.AddEntry(extraItemsChanceList, count: 1);
        var vendorList = builder.Build(outputMod, "HaOS_LLS_Aid_Vendor");

        builder = LeveledItemBuilder.Create().SetFlag(LeveledItem.Flag.UseAll, true);
        builder.AddEntry(vendorList, count: 1);

        Inject("LLS_Vendor_ShipRepairKits", builder);

        // Inject into random containers
        
        builder = LeveledItemBuilder.Create().CalculateForEachItemInCount(true).CalculateFromAllLevels(true);
        builder.AddEntry(randomRestoreLevelItem, count: 1);

        Inject("LL_Loot_Mfg_Small_Leveled", builder);
    }

    private void AddMajorAidItems(IEnumerable<string> majorAidItemList)
    {
        var builder = LeveledItemBuilder.Create().CalculateFromAllLevels(true).CalculateForEachItemInCount(true);

        foreach (var item in majorAidItemList)
        {
            var ingestible = baseGameLinkCache.Resolve<IIngestibleGetter>(item);
            builder.AddEntry(ingestible, 1, 1);
        }

        var randomRestoreLevelItem = builder.Build(outputMod, "HaOS_LLS_Aid_RestoreSoak_Major");

        builder = LeveledItemBuilder.Create().CalculateFromAllLevels(true).CalculateForEachItemInCount(true);
        builder.AddEntry(randomRestoreLevelItem, count: 2);


        // Outlands and other stores
        Inject("LL_Vendor_Outfitter_AidChems_50", builder);
        // Add spawns to big containers
        Inject("LL_Loot_Misc_Large_Rare", builder);
    }

    // Injects the given builder config into an existing record
    void Inject(string editorId, LeveledItemBuilder builder)
    {
        var baseVendorList = baseGameLinkCache.Resolve<ILeveledItemGetter>(editorId);
        var overrideVendorList = outputMod.LeveledItems.GetOrAddAsOverride(baseVendorList);
        builder.Apply(overrideVendorList);
    }
}