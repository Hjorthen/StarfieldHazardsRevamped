using System.IO;
using HazardOverhaul.Extensions;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins.Allocators;
using Mutagen.Bethesda.Starfield;

var hazardMod = new StarfieldMod("HaOS.esp", StarfieldRelease.Starfield);
using var formAllocator = new TextFileFormKeyAllocator(hazardMod, string.Format("FormID_allocations_{0}.txt", hazardMod.ModKey.FileName.NameWithoutExtension));
hazardMod.SetAllocator(formAllocator);

using var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield)
                .WithTargetDataFolder("/home/sehj/.local/share/Steam/steamapps/common/Starfield/Data")
                .WithLoadOrder("Starfield.esm")
                .WithOutputMod(hazardMod)
                .Build();

// The linkCache has to be created from the priority-order it seems
var linkCache = env.LinkCache;
var haosComponents = new BasicTypeRegistry();

var resolver = new BaseGameTypeResolver(linkCache);
var mapper = new HazardsMapper(
    hazardTypes: ["Thermal", "Airborne", "Corrosive", "Radiation"]
);

var (hazardSystem, hazardSystemForms) = HazardsSystemPatcher.WritePatch(hazardMod, mapper.HazardTypes, resolver);
// Apply changes to Globs used by the hazard system
var changedGlobs = EnvDamageSettings.Apply(hazardMod, linkCache);
haosComponents.Add(changedGlobs);

// Allow the hazardSystem to lookup things in the updated cachhe
hazardSystem.SetLinkCache(linkCache);
haosComponents.Add(hazardSystem);

HazardSystemArmorUpgrades.WritePatch(haosComponents, hazardMod, linkCache);
HazardSystemItemsPatcher.WritePatch(haosComponents, hazardMod, linkCache);
HazardSystemLevelListPatcher.WritePatch(haosComponents, hazardMod, linkCache);

HazardsSystemSpellsPatcher.WritePatch(hazardMod, hazardSystem, mapper, resolver, env);


var requiredRecordsScalingResistance = HazardSystemScalingResistancesPatcher.WritePatch(hazardSystem, hazardMod, linkCache);
var requiredRecordsMaxResistance = HazardSystemMaxResistancePerkPatcher.WritePatch(hazardSystem, hazardMod, linkCache);

var requiredRecords = requiredRecordsMaxResistance.Union(requiredRecordsMaxResistance).Union(hazardSystemForms);

hazardMod.RefreshSpellGlobalMagnitudes(env);
new StupidGlobWriter(haosComponents.Resolve<ChangedGlobCollection>()).Write("papyrus/Haos_GlobalOverrides.psc");
HazardSystemModEnablerPatcher.WritePatch(requiredRecords, hazardMod, linkCache, haosComponents);


hazardMod.BeginWrite
.ToPath(Path.Combine("", hazardMod.ModKey.FileName))
.WithLoadOrder(env.LoadOrder)
.Write();