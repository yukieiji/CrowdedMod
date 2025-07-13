using AmongUs.GameOptions;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using MonoMod.RuntimeDetour;
using Reactor;
using Reactor.Networking;
using Reactor.Networking.Attributes;
using System;
using System.Linq;
using UnityEngine.SceneManagement;

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
        NormalGameOptionsV09.RecommendedImpostors =
            NormalGameOptionsV09.MaxImpostors = Enumerable.Repeat(127, 127).ToArray();
        NormalGameOptionsV09.MinPlayers = Enumerable.Repeat(4, 127).ToArray();
        HideNSeekGameOptionsV09.MinPlayers = Enumerable.Repeat(4, 127).ToArray();

        Harmony.PatchAll();
        SceneManager.add_sceneLoaded((Action<Scene, LoadSceneMode>)((scene, _) =>
        {
            if (scene.name == "MainMenu")
            {
                RemoveVanillaServer();
            }
        }));
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
        => regionInfo != null &&
            regionInfo.TranslateName is
            StringNames.ServerAS or
            StringNames.ServerEU or
            StringNames.ServerNA;
}