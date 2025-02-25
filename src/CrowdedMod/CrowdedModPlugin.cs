using AmongUs.GameOptions;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Reactor;
using Reactor.Networking;
using Reactor.Networking.Attributes;
using System.Linq;

namespace CrowdedMod;

[BepInAutoPlugin("xyz.crowdedmods.crowdedmod")]
[BepInProcess("Among Us.exe")]
[BepInDependency(ReactorPlugin.Id)]
[ReactorModFlags(ModFlags.RequireOnAllClients)]
[BepInDependency("gg.reactor.debugger", BepInDependency.DependencyFlags.SoftDependency)] // fix debugger overwriting MinPlayers
public partial class CrowdedModPlugin : BasePlugin
{
    public const int MaxPlayers = 127;
    public const int MaxImpostors = 127 / 2;

    private Harmony Harmony { get; } = new(Id);

    public override void Load()
    {
        NormalGameOptionsV08.RecommendedImpostors =
            NormalGameOptionsV08.MaxImpostors = Enumerable.Repeat(127, 127).ToArray();
        NormalGameOptionsV08.MinPlayers = Enumerable.Repeat(4, 127).ToArray();
        HideNSeekGameOptionsV08.MinPlayers = Enumerable.Repeat(4, 127).ToArray();

        Harmony.PatchAll();

        RemoveVanillaServer();
    }

    public static void RemoveVanillaServer()
    {
        var sm = ServerManager.Instance;
        var curRegions = sm.AvailableRegions;
        sm.AvailableRegions = curRegions.Where(region => !IsVanillaServer(region)).ToArray();

        var defaultRegion = ServerManager.DefaultRegions;
        ServerManager.DefaultRegions = defaultRegion.Where(region => !IsVanillaServer(region)).ToArray();

        if (IsVanillaServer(sm.CurrentRegion))
        {
            var region = defaultRegion.FirstOrDefault();
            sm.SetRegion(region);
        }
    }

    private static bool IsVanillaServer(IRegionInfo regionInfo)
        => regionInfo.TranslateName is
            StringNames.ServerAS or
            StringNames.ServerEU or
            StringNames.ServerNA;
}