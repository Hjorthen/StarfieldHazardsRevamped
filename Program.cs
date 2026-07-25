using System.IO;
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

var resolver = new BaseGameTypeResolver(linkCache);
var mapper = new HazardsMapper(
    hazardTypes: ["Thermal", "Airborne", "Corrosive", "Radiation"]
);

var (hazardSystem, hazardSystemForms) = HazardsSystemPatcher.WritePatch(hazardMod, mapper.HazardTypes, resolver);

// Allow the hazardSystem to lookup things in the updated cachhe
hazardSystem.SetLinkCache(linkCache);

HazardSystemArmorUpgrades.WritePatch(hazardSystem, hazardMod, linkCache);
HazardSystemItemsPatcher.WritePatch(hazardSystem, hazardMod, linkCache);
HazardSystemLevelListPatcher.WritePatch(hazardSystem, hazardMod, linkCache);

HazardsSystemSpellsPatcher.WritePatch(hazardMod, hazardSystem, mapper, resolver, env);
HazardWorldspacePatcher.WritePatch(hazardMod, hazardSystem, mapper, linkCache);

var requiredRecordsScalingResistance = HazardSystemScalingResistancesPatcher.WritePatch(hazardSystem, hazardMod, linkCache);
var requiredRecordsMaxResistance = HazardSystemMaxResistancePerkPatcher.WritePatch(hazardSystem, hazardMod, linkCache);

var requiredRecords = requiredRecordsMaxResistance.Union(requiredRecordsMaxResistance).Union(hazardSystemForms);

HazardSystemModEnablerPatcher.WritePatch(requiredRecords, hazardMod);

hazardMod.BeginWrite
.ToPath(Path.Combine("", hazardMod.ModKey.FileName))
.WithLoadOrder(env.LoadOrder)
.Write();


