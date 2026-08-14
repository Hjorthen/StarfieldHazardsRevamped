
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
        AddHumanEnemyLoot();
    }
    /// <summary>
    /// Adds a chance for human enemies to spawn the minor variants of hazard-aids. They need it to survive too, right?
    /// </summary>
    private void AddHumanEnemyLoot()
    {
        var extremeRadiationForm = baseGameLinkCache.Resolve<IConditionRecordGetter>("PEO_ENV_CND_ExtremeEnvironment_Radiation_Solar");
        var extremeHeatForm = baseGameLinkCache.Resolve<IConditionRecordGetter>("PEO_ENV_CND_ExtremeEnvironment_Heat");
        var extremeCorrosiveForm = baseGameLinkCache.Resolve<IConditionRecordGetter>("PEO_ENV_CND_ExtremeEnvironment_Corrosive");
        var extremeAirborneForm = baseGameLinkCache.Resolve<IConditionRecordGetter>("PEO_ENV_CND_ExtremeEnvironment_Toxic");


        var builder = LeveledItemBuilder.Create().CalculateForEachItemInCount(false).CalculateFromAllLevels(true);
        builder.AddEntry(
            baseGameLinkCache.Resolve<IIngestibleGetter>("HaOS_Item_Restore_Minor_Radiation"),
            entryCondition: GetConditionFormCondition.With(extremeRadiationForm).EqualsTo().Value(1)
        );
        builder.AddEntry(
            baseGameLinkCache.Resolve<IIngestibleGetter>("HaOS_Item_Restore_Minor_Thermal"),
            entryCondition: GetConditionFormCondition.With(extremeHeatForm).EqualsTo().Value(1)
        );
        builder.AddEntry(
            baseGameLinkCache.Resolve<IIngestibleGetter>("HaOS_Item_Restore_Minor_Corrosive"),
            entryCondition: GetConditionFormCondition.With(extremeCorrosiveForm).EqualsTo().Value(1)
        );
        builder.AddEntry(
            baseGameLinkCache.Resolve<IIngestibleGetter>("HaOS_Item_Restore_Minor_Airborne"),
            entryCondition: GetConditionFormCondition.With(extremeAirborneForm).EqualsTo().Value(1)
        );

        var humanLootHazardAid = builder.Build(outputMod, "HaOS_Human_Hazard_Aid");
        
        string[] humanEnemiesLL = [
            "LL_LootTheme_Military",
            "LL_LootTheme_CrimsonFleet",
            "LL_LootTheme_Ecliptic",
            "LL_LootTheme_Spacer",
            "LL_LootTheme_Syndicate",
            "LL_LootTheme_VaruunZealot"
        ];
        var leveledListInjector = LeveledItemBuilder.Create().AddEntry(humanLootHazardAid);
        foreach (var item in humanEnemiesLL)
        {
            Inject(item, leveledListInjector);
        }
    }

    private void AddMinorAidItems(IEnumerable<string> minorAidItemList)
    {
        LeveledItem randomRestoreLevelItem = AddRandomAidLL(minorAidItemList);
        LeveledItem vendorInventoryLL = AddVendorInventoryLL(randomRestoreLevelItem);

        // Inject into vendors
        var patchBuilder = LeveledItemBuilder.Create().SetFlag(LeveledItem.Flag.UseAll, true);
        patchBuilder.AddEntry(vendorInventoryLL, count: 1);
        Inject("LLS_Vendor_ShipRepairKits", patchBuilder);

        // Inject into random containers
        patchBuilder = LeveledItemBuilder.Create();
        patchBuilder.AddEntry(randomRestoreLevelItem, count: 1);
        Inject("LL_Loot_Misc_Medium", patchBuilder);
    }

    private LeveledItem AddVendorInventoryLL(LeveledItem randomRestoreLevelItem)
    {
        // Small chance for lots at the vendor
        var builder = LeveledItemBuilder.Create().CalculateForEachItemInCount(true).CalculateFromAllLevels(true).WithChanceNone(50);
        builder.AddEntry(randomRestoreLevelItem, count: 5);
        var paydayItemList = builder.Build(outputMod, "HaOS_LLS_Aid_BonusItems_50");

        // Chance for extras at a vendor
        builder = LeveledItemBuilder.Create().CalculateForEachItemInCount(true).CalculateFromAllLevels(true).WithChanceNone(40);
        builder.AddEntry(randomRestoreLevelItem, count: 3);
        builder.AddEntry(paydayItemList, count: 1);
        var extraItemsChanceList = builder.Build(outputMod, "HaOS_LLS_Aid_BonusItems_40");

        // The LL to be used by vendors
        builder = LeveledItemBuilder.Create().SetFlag(LeveledItem.Flag.UseAll, true);
        builder.AddEntry(randomRestoreLevelItem, count: 4);
        builder.AddEntry(extraItemsChanceList, count: 1);
        return builder.Build(outputMod, "HaOS_LLS_Aid_Vendor");
    }

    private LeveledItem AddRandomAidLL(IEnumerable<string> minorAidItemList)
    {
        var builder = LeveledItemBuilder.Create().CalculateForEachItemInCount(true).CalculateFromAllLevels(true);
        foreach (var item in minorAidItemList)
        {
            var ingestible = baseGameLinkCache.Resolve<IIngestibleGetter>(item);
            // 1-2 each pick for variation, 1.5 average
            builder.AddEntry(ingestible, 1, 1);
            builder.AddEntry(ingestible, 1, 2);
        }
        return builder.Build(outputMod, "HaOS_LLS_Aid_RestoreSoak_Minor");
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