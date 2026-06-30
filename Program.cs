using System.IO;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Starfield;

var hazardMod = new StarfieldMod("MyMod.esp", StarfieldRelease.Starfield);
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

var hazardSystem = HazardsSystemPatcher.WritePatch(hazardMod, mapper.HazardTypes, resolver);

// Allow the hazardSystem to lookup things in the updated cachhe
hazardSystem.SetLinkCache(linkCache);

HazardSystemArmorUpgrades.WritePatch(hazardSystem, hazardMod, linkCache);
HazardSystemItemsPatcher.WritePatch(hazardSystem, hazardMod, linkCache);
HazardsSystemSpellsPatcher.WritePatch(hazardMod, hazardSystem, mapper, resolver, env);
HazardSystemScalingResistancesPatcher.WritePatch(hazardSystem, hazardMod, linkCache);
HazardSystemMaxResistancePerkPatcher.WritePatch(hazardSystem, hazardMod, linkCache);


hazardMod.BeginWrite
.ToPath(Path.Combine("", hazardMod.ModKey.FileName))
.WithLoadOrder(env.LoadOrder)
.Write();