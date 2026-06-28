// CreationKit Armor Workbench upgrade path:
// ConstructableObject(co_mod) -> Object Mod(mod_) -> Object Mod(_Template_mod)
using System;
using System.Collections.Generic;
using System.Linq;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Starfield;

public class HazardSystemArmorUpgrades
{
    private readonly HazardSystem hazardSystem;
    private readonly StarfieldMod outputMod;
    private readonly ILinkCache<IStarfieldMod, IStarfieldModGetter> baseGameLinkCache;

    private HazardSystemArmorUpgrades(HazardSystem hazardSystem, StarfieldMod outputMod, ILinkCache<IStarfieldMod, IStarfieldModGetter> baseGameLinkCache)
    {
        this.hazardSystem = hazardSystem;
        this.outputMod = outputMod;
        this.baseGameLinkCache = baseGameLinkCache;
    }

    public static void WritePatch(HazardSystem hazardSystem, StarfieldMod outputMod, ILinkCache<IStarfieldMod, IStarfieldModGetter> baseGameLinkCache)
    {
        var patcher = new HazardSystemArmorUpgrades(hazardSystem, outputMod, baseGameLinkCache);
        patcher.PatchInternal();
    }

    private void PatchInternal()
    {
        ArmorUpgradePatcher
        .Create("co_mod_Armor_Spacesuit_Slot04_CarryWeight", baseGameLinkCache)
        .WithUpgradeDisplayDescription("Dust scrubbers", "Radiation resistance increased")
        .WithWorkbenchUpgradeDisplay("Increase radiation resistance by 15%")
        .WithUpgradePropertyIncreaseAV(hazardSystem.GetResistanceAV("Radiation"), 15)
        .Build(outputMod);
    }
}

public class ArmorUpgradePatcher
{
    private readonly ILinkCache<IStarfieldMod, IStarfieldModGetter> linkCache;
    private readonly string templateConstructibleObjectEditorId;

    private string armorModDisplayName = string.Empty;
    private string armorModDisplayDescription = string.Empty;
    private string armorWorkbenchUpgradeDescription = string.Empty;

    private readonly List<ConstructibleObjectComponent> craftingComponents = new();
    private readonly List<AObjectModProperty<Armor.Property>> upgradeProperties = new();

    private ArmorUpgradePatcher(
        ILinkCache<IStarfieldMod, IStarfieldModGetter> linkCache,
        string templateConstructibleObjectEditorId)
    {
        this.linkCache = linkCache;
        this.templateConstructibleObjectEditorId = templateConstructibleObjectEditorId;
    }

    /// <summary>
    /// Starts a new armor upgrade definition, cloned from an existing base-game ConstructibleObject
    /// (i.e. an existing workbench recipe that already targets an Armor or ArmorModification).
    /// </summary>
    public static ArmorUpgradePatcher Create(
        string templateConstructibleObjectEditorId,
        ILinkCache<IStarfieldMod, IStarfieldModGetter> baseGameLinkCache)
    {
        return new ArmorUpgradePatcher(
            baseGameLinkCache,
            templateConstructibleObjectEditorId);
    }

    /// <summary>
    /// Adds a required crafting resource (e.g. Aluminum x3) to the workbench recipe.
    /// </summary>
    public ArmorUpgradePatcher WithCraftingComponent(string componentMiscItemEditorName, uint requiredCount)
    {
        if(!linkCache.TryResolveIdentifier<MiscItem>(componentMiscItemEditorName, out var miscItem))
            throw new ArgumentException($"The given MiscItem component could not be found using editor id '{componentMiscItemEditorName}'. Its either the wrong form type or doesn't exist.");

        var entry = new ConstructibleObjectComponent
        {
            Component = new FormLink<IItemGetter>(miscItem),
            RequiredCount = requiredCount,
        };
        craftingComponents.Add(entry);
        return this;
    }

    /// <summary>
    /// Sets the display-facing OMOD's name/description (what the player sees after the upgrade has been added to an item).
    /// </summary>
    public ArmorUpgradePatcher WithUpgradeDisplayDescription(string name, string description)
    {
        armorModDisplayName = name;
        armorModDisplayDescription = description;
        return this;
    }

    /// <summary>
    /// Sets the text displayed in the Armor Workbench
    /// </summary>
    public ArmorUpgradePatcher WithWorkbenchUpgradeDisplay(string description)
    {
        armorWorkbenchUpgradeDescription = description;
        return this;
    }

    /// <summary>
    /// Adds a stat-modifying property to the underlying property-granting OMOD
    /// (e.g. an ObjectModFormLinkFloatProperty targeting Armor.Property.ActorValue
    /// to grant/boost an oxygen Actor Value). Assumes the caller has constructed
    /// a property whose concrete type is valid for the targeted Armor.Property.
    /// </summary>
    public ArmorUpgradePatcher WithUpgradeProperty(AObjectModProperty<Armor.Property> property)
    {
        upgradeProperties.Add(property);
        return this;
    }

    public IConstructibleObjectGetter Build(IStarfieldMod targetMod)
    {
        var templateConstructibleObject = linkCache.Resolve<IConstructibleObjectGetter>(templateConstructibleObjectEditorId);
        var createdModFormKey = templateConstructibleObject.CreatedObject.FormKey;

        // The displayed OMOD: defines name/description/attach point and points to the property-granting OMOD.
        var displayedObjectMod = linkCache.Resolve<IArmorModificationGetter>(createdModFormKey);
        // The property-granting OMOD is the first and only member of 'Includes'
        var propertyGivingObjectModLink = displayedObjectMod.Includes.First().Mod;
        // The property-granting OMOD: Uses Properties to define which buffs to give
        var propertyGivingObjectMod = linkCache.Resolve<IArmorModificationGetter>(propertyGivingObjectModLink.FormKey);

        var newPropertyMod = CreateNewPropertyModFrom(propertyGivingObjectMod, targetMod);
        var newDisplayedObjectMod = CreateNewDisplayObjectModFrom(displayedObjectMod, newPropertyMod, targetMod);
        return CreateNewConstructibleObject(templateConstructibleObject, newDisplayedObjectMod, targetMod);
    }

    private ArmorModification CreateNewPropertyModFrom(IArmorModificationGetter baseMod, IStarfieldMod targetMod)
    {
        var clonedMod = (ArmorModification)targetMod.ObjectModifications.DuplicateInAsNewRecord<AObjectModification, IArmorModificationGetter, IAObjectModificationGetter>(baseMod);

        clonedMod.Properties.Clear();
        clonedMod.Properties.AddRange(upgradeProperties);

        return clonedMod;
    }

    private ArmorModification CreateNewDisplayObjectModFrom(IArmorModificationGetter baseMod, ArmorModification targetPropertyMod, IStarfieldMod targetMod)
    {
        var clonedMod = (ArmorModification)targetMod.ObjectModifications.DuplicateInAsNewRecord<AObjectModification, IArmorModificationGetter, IAObjectModificationGetter>(baseMod);

        clonedMod.Name = armorModDisplayName;
        clonedMod.Description = armorModDisplayDescription;

        clonedMod.Includes.Clear();
        clonedMod.Includes.Add(new ObjectModInclude
        {
            Mod = new FormLink<IAObjectModificationGetter>(targetPropertyMod.FormKey),
            Optional = false,
            MinimumLevel = 0,
        });

        return clonedMod;
    }

    private ConstructibleObject CreateNewConstructibleObject(IConstructibleObjectGetter baseObject, ArmorModification newDisplayedObjectMod, IStarfieldMod targetMod)
    {
        var clonedObject = targetMod.ConstructibleObjects.DuplicateInAsNewRecord(baseObject);
        clonedObject.CreatedObject.SetTo(newDisplayedObjectMod.FormKey);
        clonedObject.Description = armorWorkbenchUpgradeDescription;

        clonedObject.ConstructableComponents.Clear();
        clonedObject.ConstructableComponents.AddRange(craftingComponents);

        return clonedObject;
    }
}


public static class ArmorUpgradePatcherExtensions
{
    public static ArmorUpgradePatcher WithUpgradePropertyIncreaseAV(this ArmorUpgradePatcher patcher, IActorValueInformationGetter AV, float amount)
    {
        patcher.WithUpgradeProperty(new ObjectModFormLinkFloatProperty<Armor.Property>
        {
            Property = Armor.Property.ActorValue,
            Record = new FormLink<IStarfieldMajorRecordGetter>(AV.FormKey),
            FunctionType = ObjectModProperty.FloatFunctionType.Add,
            Value = amount
        });
        return patcher;
    }
}
