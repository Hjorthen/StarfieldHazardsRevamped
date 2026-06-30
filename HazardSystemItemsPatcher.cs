using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Cache.Internals.Implementations;
using Mutagen.Bethesda.Starfield;

interface IItemAssets
{
    string Model { get; } 
    SoundReference GetUseSound(ILinkCache<IStarfieldMod, IStarfieldModGetter> baseGameLinkCache);
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

//  Energy Weapon Dissipation 
public class HazardSystemItemsPatcher
{
    private readonly HazardSystem hazardSystem;
    private readonly StarfieldMod outputMod;
    private readonly ILinkCache<IStarfieldMod, IStarfieldModGetter> baseGameLinkCache;

    private HazardSystemItemsPatcher(HazardSystem hazardSystem, StarfieldMod outputMod, ILinkCache<IStarfieldMod, IStarfieldModGetter> baseGameLinkCache)
    {
        this.hazardSystem = hazardSystem;
        this.outputMod = outputMod;
        this.baseGameLinkCache = baseGameLinkCache;
    }

    public static void WritePatch(HazardSystem hazardSystem, StarfieldMod outputMod, ILinkCache<IStarfieldMod, IStarfieldModGetter> baseGameLinkCache)
    {
        var patcher = new HazardSystemItemsPatcher(hazardSystem, outputMod, baseGameLinkCache);
        patcher.PatchInternal();
    }

    private void PatchInternal()
    {
        var radiationRestoreMf = AddSoakRestoreMagicEffect("Radiation", "HS_Restore_Radiation_Soak", "", "Restore <mag> of radiation suit integrity");
        AddItem("Radiation Mesh Shield", "HS_Item_Restore_Radiation", "A lead-alloy mesh designed to soak up radiation, its nano-crystalline structure keeps excess weight to a minimum.", radiationRestoreMf, new RadiationMesh());
        var thermalRestoreMf = AddSoakRestoreMagicEffect("Thermal", "HS_Restore_Radiation_Soak", "", "Restore <mag> of thermal suit integrity");
        AddItem("Cryo Battery Pack", "HS_Item_Restore_Thermal", "A combined coolant and battery replacement for suit thermal regulators. Engineered to keep its charge under extreme heat - so you can keep your cool.", thermalRestoreMf, new ItemBatteryPack());
        var corrosiveRestoreMf = AddSoakRestoreMagicEffect("Corrosive", "HS_Restore_Corrosive_Soak", "", "Restore <mag> of corrosive suit integrity");
        AddItem("Deimos CorroGuard", "HS_Item_Restore_Corrosive", "A thick, flowing paste that provides a protective coating. Wear proper protective equipment when applied. May stain materials. Seek nearest medical facility if inhaled.", corrosiveRestoreMf, new BucketOfPaste());
        var airborneRestoreMf = AddSoakRestoreMagicEffect("Airborne", "HS_Restore_Airborne_Soak", "", "Restore <mag> of airborne suit integrity");
        AddItem("Outland Airfilter", "HS_Item_Restore_Airborne", "A replacement filter for standard issue spacesuits. Breathe the antartic air of old.", airborneRestoreMf, new AirSieve());
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
            Magnitude = 75,
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
        return mf;
    }
}