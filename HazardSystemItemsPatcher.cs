using System;
using System.Collections.Generic;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Starfield;

record RestoreItemEffectData(float Mass, uint Value, float Magnitude);
record RestoreItemAppearenceData(string Name, string Description, IItemAssets Assets);

class AidItemsData
{
    private Dictionary<string, IFormLinkGetter<Ingestible>> minorAid = [];
    private Dictionary<string, IFormLinkGetter<Ingestible>> majorAid = [];

    public IEnumerable<IFormLinkGetter<Ingestible>> MajorAidItems => majorAid.Values;
    public IEnumerable<IFormLinkGetter<Ingestible>> MinorAidItems => minorAid.Values;

    public void RegisterMinorAid(Ingestible aidItem, string hazardType)
    {
        minorAid.Add(hazardType, aidItem.ToLinkGetter());
    }

    public void RegisterMajorAid(Ingestible aidItem, string hazardType)
    {
        majorAid.Add(hazardType, aidItem.ToLinkGetter());
    }

    public IFormLinkGetter<Ingestible> LookupMinorAidFor(string hazardType) => minorAid[hazardType];

    public IFormLinkGetter<Ingestible> LookupMajorAidFor(string hazardType) => majorAid[hazardType];
}


public class HazardSystemItemsPatcher
{
    private readonly BasicTypeRegistry haosComponents;
    private readonly HazardSystem hazardSystem;
    private readonly StarfieldMod outputMod;
    private readonly ILinkCache<IStarfieldMod, IStarfieldModGetter> baseGameLinkCache;

    private HazardSystemItemsPatcher(BasicTypeRegistry haosComponents, StarfieldMod outputMod, ILinkCache<IStarfieldMod, IStarfieldModGetter> baseGameLinkCache)
    {
        this.haosComponents = haosComponents;
        this.hazardSystem = haosComponents.Resolve<HazardSystem>();

        this.outputMod = outputMod;
        this.baseGameLinkCache = baseGameLinkCache;
    }

    public static void WritePatch(BasicTypeRegistry haosComponents, StarfieldMod outputMod, ILinkCache<IStarfieldMod, IStarfieldModGetter> baseGameLinkCache)
    {
        var patcher = new HazardSystemItemsPatcher(haosComponents, outputMod, baseGameLinkCache);
        patcher.PatchInternal();
    }

    private void PatchInternal()
    {
        var aidItemsData = AddMajorRestoreItems();
        haosComponents.Add(aidItemsData);
    }
    private AidItemsData AddMajorRestoreItems()
    {
        var majorAidItem = new RestoreItemEffectData(Mass: 1.8f, Value: 850, Magnitude: 100);
        var minorAidItem = new RestoreItemEffectData(Mass: 0.8f, Value: 340, Magnitude: 35);
        var aidItemData = new AidItemsData();


        var radiationRestoreMf = AddSoakRestoreMagicEffect("Radiation", "HS_Restore_Radiation_Soak", "", "Restore <mag> of radiation suit integrity");

        aidItemData.RegisterMajorAid(
                AddItem(new RestoreItemAppearenceData(
                            Name: "Radiation Mesh Shield",
                            Description: "A lead-alloy mesh designed to soak up radiation, its nano-crystalline structure keeps excess weight to a minimum.",
                            Assets: new RadiationMesh()),
                            itemData: majorAidItem,
                            effect: radiationRestoreMf,
                            editorId: "HaOS_Item_Restore_Major_Radiation"
                        ),
                "Radiation"
        );

        aidItemData.RegisterMinorAid(
            AddItem(new RestoreItemAppearenceData(
                Name: "Rad-X Aerosol",
                Description: "Apply a thin layer of lead to temporarily block out radiation. Warning: Inhaling might cause discomfort, hallucination and seizure. Not for tanning.",
                Assets: new Injector()),
                itemData: minorAidItem,
                effect: radiationRestoreMf,
                editorId: "HaOS_Item_Restore_Minor_Radiation"
            ),
            "Radiation"
        );

        var thermalRestoreMf = AddSoakRestoreMagicEffect("Thermal", "HS_Restore_Thermal_Soak", "", "Restore <mag> of thermal suit integrity");
        aidItemData.RegisterMajorAid(
            AddItem(new RestoreItemAppearenceData(
                Name: "Cryo Battery Pack",
                Description: "A combined coolant and battery replacement for suit thermal regulators. Engineered for extreme heat - so you can keep your cool.",
                Assets: new ItemBatteryPack()),
                itemData: majorAidItem,
                effect: thermalRestoreMf,
                editorId: "HaOS_Item_Restore_Major_Thermal"
            ),
            "Thermal"
        );

        aidItemData.RegisterMinorAid(
            AddItem(new RestoreItemAppearenceData(
                Name: "Pocket Heatleech",
                Description: "A pocket heatleech, dormant in a sealed pocket case. Apply directly to suit's thermal regulator. Warning: Max feeding time 15 minutes. Exceeding this threshold may result in bodily injury or death.",
                Assets: new XenoVial()),
                itemData: minorAidItem,
                effect: thermalRestoreMf,
                editorId: "HaOS_Item_Restore_Minor_Thermal"
            ), 
            "Thermal"
        );

        var corrosiveRestoreMf = AddSoakRestoreMagicEffect("Corrosive", "HS_Restore_Corrosive_Soak", "", "Restore <mag> of corrosive suit integrity");
       
       
        aidItemData.RegisterMajorAid(
            AddItem(new RestoreItemAppearenceData(
                Name: "Deimos CorroGuard",
                Description: "A thick, flowing paste that provides a protective coating. Wear proper protective equipment when applied. May stain materials. Seek nearest medical facility if inhaled.",
                Assets: new BucketOfPaste()),
                itemData: majorAidItem,
                effect: corrosiveRestoreMf,
                editorId: "HaOS_Item_Restore_Major_Corrosive"
            ),
            "Corrosive"
        );
        aidItemData.RegisterMinorAid(
            AddItem(new RestoreItemAppearenceData(
                Name: "WD-80",
                Description: "Universal Rust remover. Apply directly to the affected area and it will remove rust and other residue while leaving behind a small protective coating.",
                Assets: new SprayLikeObject()),
                itemData: minorAidItem,
                effect: corrosiveRestoreMf,
                editorId: "HaOS_Item_Restore_Minor_Corrosive"
            ),
            "Corrosive"
        );

        var airborneRestoreMf = AddSoakRestoreMagicEffect("Airborne", "HS_Restore_Airborne_Soak", "", "Restore <mag> of airborne suit integrity");
        aidItemData.RegisterMajorAid(
            AddItem(new RestoreItemAppearenceData(
                Name: "Outland Airfilter",
                Description: "A replacement filter for standard issue spacesuits. Breathe the antartic air of old.",
                Assets: new AirSieve()),
                itemData: majorAidItem,
                effect: airborneRestoreMf,
                editorId: "HaOS_Item_Restore_Major_Airborne"
            ),
            "Airborne"
        );

        aidItemData.RegisterMinorAid(
            AddItem(new RestoreItemAppearenceData(
                Name: "Prophylactic Canister",
                Description: "A canister of compressed air that dilutes toxic buildup. Buys time for the suit's life support systems to purify the air, before you suffocate.",
                Assets: new GasCanister()),
                itemData: minorAidItem,
                effect: airborneRestoreMf,
                editorId: "HaOS_Item_Restore_Minor_Airborne"
            ),
            "Airborne"
        );


        return aidItemData;
    }

    private Ingestible AddItem(RestoreItemAppearenceData appearance, RestoreItemEffectData itemData, IMagicEffectGetter effect, string editorId)
    {
        var newItem = outputMod.Ingestibles.AddNew(editorId);
        newItem.Name = appearance.Name;
        newItem.Description = appearance.Description;
        newItem.Weight = itemData.Mass;
        newItem.Value = itemData.Value;
        newItem.Flags = Ingestible.Flag.NoAutoCalc | Ingestible.Flag.FoodItem;
        
        newItem.ConsumeSound = appearance.Assets.GetUseSound(baseGameLinkCache);

        newItem.Model = new Model()
        {
           File = new Mutagen.Bethesda.Plugins.Assets.AssetLink<Mutagen.Bethesda.Starfield.Assets.StarfieldModelAssetType>(appearance.Assets.Model) ,
        };

        var itemEffect = new Effect();
        itemEffect.BaseEffect.SetTo(effect);
        itemEffect.Data = new EffectData()
        {
            Area = 0,
            Magnitude = itemData.Magnitude,
            Duration = 0
        };

        newItem.Effects.Add(itemEffect);

        return newItem;
    }
    private Ingestible AddItem(string itemName, string editorId, string description, MagicEffect effect, IItemAssets assets)
    {
        var newItem = outputMod.Ingestibles.AddNew(editorId);
        newItem.Name = itemName;
        newItem.Description = description;
        newItem.Weight = 5;
        newItem.Value = 4000;
        
        newItem.ConsumeSound = assets.GetUseSound(baseGameLinkCache);

        newItem.Model = new Model()
        {
           File = new Mutagen.Bethesda.Plugins.Assets.AssetLink<Mutagen.Bethesda.Starfield.Assets.StarfieldModelAssetType>(assets.Model) ,
        };

        var itemEffect = new Effect();
        itemEffect.BaseEffect.SetTo(effect);
        itemEffect.Data = new EffectData()
        {
            Area = 0,
            Magnitude = 100,
            Duration = 0
        };

        newItem.Effects.Add(itemEffect);

        return newItem;
    }

    private MagicEffect AddSoakRestoreMagicEffect(string hazardType, string editorId, string name, string description)
    {
        var mf = outputMod.MagicEffects.AddNew(editorId);                                                                                
        mf.Archetype = new MagicEffectArchetype()
        {
            Type = MagicEffectArchetype.TypeEnum.ValueModifier
        };
        mf.CastType = CastType.FireAndForget;
        mf.Flags = MagicEffect.Flag.NoDuration | MagicEffect.Flag.Painless | MagicEffect.Flag.NoArea | MagicEffect.Flag.NoDuration;
        mf.ActorValue2.SetTo(hazardSystem.GetSoakAV(hazardType));
        mf.Name = name;
        mf.Description = description;

        mf.DATADataTypeState |= MagicEffect.DATADataType.Break0;
        return mf;
    }
}

interface IItemAssets
{
    string Model { get; } 
    SoundReference GetUseSound(ILinkCache<IStarfieldMod, IStarfieldModGetter> baseGameLinkCache);
}
class Injector : IItemAssets
{
    public string Model => @"Items\Drugs_Or_Medical\crafted_inhailer_junk_flush_01.nif";

    public SoundReference GetUseSound(ILinkCache<IStarfieldMod, IStarfieldModGetter> baseGameLinkCache)
    {
        return baseGameLinkCache.Resolve<IIngestibleGetter>("Aid_EmergencyKit").ConsumeSound.DeepCopy();
    }
}
class GasCanister : IItemAssets
{
    public string Model => @"SetDressing\OxygenRescueMask\OxygenRescueMask01.nif";
    public SoundReference GetUseSound(ILinkCache<IStarfieldMod, IStarfieldModGetter> baseGameLinkCache)
    {
        return baseGameLinkCache.Resolve<IIngestibleGetter>("Aid_EmergencyKit").ConsumeSound.DeepCopy();
    }
}
class XenoVial : IItemAssets
{
    public string Model => "SetDressing\\ScienceGlass\\scienceglass_vial03closed01full04.nif";
    public SoundReference GetUseSound(ILinkCache<IStarfieldMod, IStarfieldModGetter> baseGameLinkCache)
    {
        return baseGameLinkCache.Resolve<IIngestibleGetter>("Food_Craft_AlienStew").ConsumeSound.DeepCopy();
    }
}
class SprayLikeObject : IItemAssets
{
    public string Model => "SetDressing\\manufactured_goods\\Mfg_Neutral_Capacitor.nif";
    public SoundReference GetUseSound(ILinkCache<IStarfieldMod, IStarfieldModGetter> baseGameLinkCache)
    {
        return baseGameLinkCache.Resolve<IIngestibleGetter>("Aid_EmergencyKit").ConsumeSound.DeepCopy();
    }
}
class AirSieve : IItemAssets
{
    public string Model => "SetDressing\\MemorySubstrate\\MemorySubstrate01.nif";

    public string PickupSound => null;

    public string UseSound => "ITEM_USE_Aid_MedStim";

    public SoundReference GetUseSound(ILinkCache<IStarfieldMod, IStarfieldModGetter> baseGameLinkCache)
    {
        return baseGameLinkCache.Resolve<IIngestibleGetter>("Aid_EmergencyKit").ConsumeSound.DeepCopy();
    }
}

class BucketOfPaste : IItemAssets
{
    public string Model => @"Landscape\Flora\Ingredients\FloraIngredientSap01.nif";


    public SoundReference GetUseSound(ILinkCache<IStarfieldMod, IStarfieldModGetter> baseGameLinkCache)
    {
        return baseGameLinkCache.Resolve<IIngestibleGetter>("Food_Craft_Meatloaf").ConsumeSound.DeepCopy();
    }
}

class RadiationMesh : IItemAssets
{
    public string Model => @"SetDressing\manufactured_goods\Mfg_Polytextile.nif";

    public SoundReference GetUseSound(ILinkCache<IStarfieldMod, IStarfieldModGetter> baseGameLinkCache)
    {
        return baseGameLinkCache.Resolve<IIngestibleGetter>("Aid_Affl_Bandages_02").ConsumeSound.DeepCopy();
    }
}

class ItemBatteryPack : IItemAssets
{
    public string Model => @"Items\FoodDrink_Set\FoodDrink_Set_PortableKit_RoundTinStack01.nif";

    public SoundReference GetUseSound(ILinkCache<IStarfieldMod, IStarfieldModGetter> baseGameLinkCache)
    {
        return baseGameLinkCache.Resolve<IMiscItemGetter>("UC07_Microcell").PickupSound!.DeepCopy();
    }
}