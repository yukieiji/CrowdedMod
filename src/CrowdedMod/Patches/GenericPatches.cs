using AmongUs.GameOptions;
using AmongUs.Data;
using CrowdedMod.Net;
using HarmonyLib;
using InnerNet;
using Reactor.Networking.Rpc;
using System.Linq;

namespace CrowdedMod.Patches;

internal static class GenericPatches
{
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdCheckColor))]
    public static class PlayerControlCmdCheckColorPatch
    {
        public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] byte colorId)
        {
            Rpc<SetColorRpc>.Instance.Send(__instance, colorId);
            return false;
        }
    }

    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.UpdateCachedClients))]
    public static class InnerNetClientUpdateCachedClientsPatch
    {
        public static void Prefix(
            [HarmonyArgument(0)] ClientData clientData,
            [HarmonyArgument(1)] PlayerControl character)
        {
            var localPlayer = PlayerControl.LocalPlayer;
            if (localPlayer == null ||
                localPlayer == character)
            {
                return;
            }
            Rpc<SetColorRpc>.Instance.Send(
                localPlayer, (byte)localPlayer.Data.DefaultOutfit.ColorId);
        }
    }

    [HarmonyPatch(typeof(PlayerTab), nameof(PlayerTab.Update))]
    public static class PlayerTabIsSelectedItemEquippedPatch
    {
        public static void Postfix(PlayerTab __instance)
        {
            __instance.currentColorIsEquipped = false;
        }
    }

    [HarmonyPatch(typeof(PlayerTab), nameof(PlayerTab.UpdateAvailableColors))]
    public static class PlayerTabUpdateAvailableColorsPatch
    {
        public static bool Prefix(PlayerTab __instance)
        {
            __instance.AvailableColors.Clear();
            for (var i = 0; i < Palette.PlayerColors.Count; i++)
            {
                if (!PlayerControl.LocalPlayer || PlayerControl.LocalPlayer.CurrentOutfit.ColorId != i)
                {
                    __instance.AvailableColors.Add(i);
                }
            }

            return false;
        }
    }

    // I did not find a use of this method, but still patching for future updates
    // maxExpectedPlayers is unknown, looks like server code tbh
    [HarmonyPatch(typeof(HideNSeekGameOptionsV10), nameof(HideNSeekGameOptionsV10.AreInvalid))]
    public static class InvalidHnSOptionsPatches
    {
        public static bool Prefix(HideNSeekGameOptionsV10 __instance, [HarmonyArgument(0)] int maxExpectedPlayers)
            => IsOptionValid(__instance.Cast<IGameOptions>(), maxExpectedPlayers);
    }

    [HarmonyPatch(typeof(NormalGameOptionsV10), nameof(NormalGameOptionsV10.AreInvalid))]
    public static class InvalidNormalOptionsPatches
    {
        public static bool Prefix(NormalGameOptionsV10 __instance, [HarmonyArgument(0)] int maxExpectedPlayers)
            => IsOptionValid(__instance.Cast<IGameOptions>(), maxExpectedPlayers);
    }

    private static bool IsOptionValid(IGameOptions option, int maxExpectedPlayers)
        => option.MaxPlayers > maxExpectedPlayers ||
            option.NumImpostors < 1 ||
            option.NumImpostors + 1 > maxExpectedPlayers / 2 ||
            option.GetInt(Int32OptionNames.KillDistance) is < 0 or > 2 ||
            option.GetFloat(FloatOptionNames.PlayerSpeedMod) is <= 0f or > 3f;

    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Update))]
    public static class GameStartManagerUpdatePatch
    {
        private static string fixDummyCounterColor = string.Empty;
        public static void Prefix(GameStartManager __instance)
        {
            if (GameData.Instance == null || __instance.LastPlayerCount == GameData.Instance.PlayerCount)
            {
                return;
            }

            if (__instance.LastPlayerCount > __instance.MinPlayers)
            {
                fixDummyCounterColor = "<color=#00FF00FF>";
            }
            else if (__instance.LastPlayerCount == __instance.MinPlayers)
            {
                fixDummyCounterColor = "<color=#FFFF00FF>";
            }
            else
            {
                fixDummyCounterColor = "<color=#FF0000FF>";
            }
        }

        public static void Postfix(GameStartManager __instance)
        {
            if (GameData.Instance == null ||
                AmongUsClient.Instance == null ||
                GameManager.Instance == null ||
                GameManager.Instance.LogicOptions == null ||
                string.IsNullOrEmpty(fixDummyCounterColor))
            {
                return;
            }
            int maxPlayerNum = AmongUsClient.Instance.NetworkMode is NetworkModes.LocalGame ?
                CrowdedModPlugin.MaxPlayers : GameManager.Instance.LogicOptions.MaxPlayers;
            __instance.PlayerCounter.text = $"{fixDummyCounterColor}{GameData.Instance.PlayerCount}/{maxPlayerNum}";
            fixDummyCounterColor = string.Empty;
        }
    }

    [HarmonyPatch(typeof(CreateGameOptions), nameof(CreateGameOptions.Show))]
    public static class CreateGameOptionsShowPatch
    {
        public static void Postfix(CreateGameOptions __instance)
        {
            var numberOption = __instance.gameObject.GetComponentInChildren<NumberOption>(true);
            if (numberOption != null)
            {
                numberOption.ValidRange.max = CrowdedModPlugin.MaxPlayers;
            }
        }
    }

    [HarmonyPatch(typeof(PingTracker), nameof(PingTracker.Update))]
    public static class PingShowerPatch
    {
        public static void Postfix(PingTracker __instance)
        {
            __instance.text.text += "\n<color=#FFB793>CrowdedMod</color>";
        }
    }

    // Will be patched with signatures later when BepInEx reveals it
    // [HarmonyPatch(typeof(InnerNetServer), nameof(InnerNetServer.HandleNewGameJoin))]
    // public static class InnerNetSerer_HandleNewGameJoin
    // {
    //     public static bool Prefix(InnerNetServer __instance, [HarmonyArgument(0)] InnerNetServer.Player client)
    //     {
    //         if (__instance.Clients.Count >= 15)
    //         {
    //             __instance.Clients.Add(client);
    //
    //             client.LimboState = LimboStates.PreSpawn;
    //             if (__instance.HostId == -1)
    //             {
    //                 __instance.HostId = __instance.Clients.ToArray()[0].Id;
    //
    //                 if (__instance.HostId == client.Id)
    //                 {
    //                     client.LimboState = LimboStates.NotLimbo;
    //                 }
    //             }
    //
    //             var writer = MessageWriter.Get(SendOption.Reliable);
    //             try
    //             {
    //                 __instance.WriteJoinedMessage(client, writer, true);
    //                 client.Connection.Send(writer);
    //                 __instance.BroadcastJoinMessage(client, writer);
    //             }
    //             catch (Il2CppException exception)
    //             {
    //                 Debug.LogError("[CM] InnerNetServer::HandleNewGameJoin MessageWriter 2 Exception: " +
    //                                exception.Message);
    //                 // ama too stupid for this
    //                 // Debug.LogException(exception.InnerException, __instance);
    //             }
    //             finally
    //             {
    //                 writer.Recycle();
    //             }
    //
    //             return false;
    //         }
    //
    //         return true;
    //     }
    // }

    [HarmonyPatch(typeof(GameOptionsMenu), nameof(GameOptionsMenu.Initialize))]
    public static class GameOptionsMenu_Initialize
    {
        public static void Postfix(GameOptionsMenu __instance)
        {
            var numberOptions = __instance.GetComponentsInChildren<NumberOption>();

            var impostorsOption = numberOptions.FirstOrDefault(o => o.Title == StringNames.GameNumImpostors);
            if (impostorsOption != null)
            {
                impostorsOption.ValidRange = new FloatRange(1, CrowdedModPlugin.MaxImpostors);
            }

        }
    }
}
