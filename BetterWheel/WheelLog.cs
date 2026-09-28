using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Valheim.UI;

namespace BetterWheel
{
    /// <summary>
    /// Optional log of what the wheel does (Debug / Log): openings, key releases and uses, with what was selected, to
    /// find out why a use didn't happen. Off by default.
    /// </summary>
    public static class WheelLog
    {
        public static ConfigEntry<bool> Enabled;
        private static ManualLogSource _log;

        public static void Bind(ConfigFile cfg, ManualLogSource log)
        {
            _log = log;
            Enabled = cfg.Bind("9 - Debug", "Log", false,
                "Writes what the wheel does (openings, key releases, uses) to the BepInEx log, for bug reports.");
        }

        public static void Write(string text)
        {
            if (Enabled != null && Enabled.Value)
            {
                _log?.LogInfo($"[frame {Time.frameCount}] {text}");
            }
        }

        public static string State(RadialBase radial)
        {
            Traverse t = Traverse.Create(radial);
            Vector2 input = t.Field("m_previousInput").GetValue<Vector2>();
            return $"selected {Describe(radial.Selected)} (index {t.Field("m_index").GetValue<int>()}, layer {t.Field("m_currentLayer").GetValue<int>()} of "
                + $"{t.Field("m_nrOfLayers").GetValue<int>() + 1}), pointer input {(input == Vector2.zero ? "none" : input.ToString("0.00"))}, "
                + $"hover menu {radial.IsHoverMenu}, page {radial.CurrentConfig?.LocalizedName}";
        }

        public static int QueuedActions(Player player)
        {
            return player != null ? Traverse.Create(player).Field("m_actionQueue").Property("Count").GetValue<int>() : 0;
        }

        public static string Describe(RadialMenuElement element)
        {
            if (element == null)
            {
                return "nothing";
            }
            if (element is ItemElement item && item.m_data != null)
            {
                return $"{item.m_data.m_shared.m_name} ({item.m_data.m_shared.m_itemType}{(item.m_data.m_equipped ? ", equipped" : "")})";
            }
            return element.GetType().Name;
        }
    }

    /// <summary>Logs every use the wheel makes, and whether the item was used.</summary>
    [HarmonyPatch(typeof(RadialBase), "OnInteract")]
    internal static class InteractLogPatch
    {
        private static void Prefix(RadialBase __instance)
        {
            WheelLog.Write($"Use: {WheelLog.State(__instance)}");
        }

        private static void Postfix(RadialBase __instance)
        {
            if (WheelLog.Enabled.Value && __instance.Selected is ItemElement item && item.m_data != null)
            {
                WheelLog.Write($"  after use: {WheelLog.Describe(item)}, still in inventory {Player.m_localPlayer?.GetInventory().ContainsItem(item.m_data)}, "
                    + $"queued actions {WheelLog.QueuedActions(Player.m_localPlayer)}");
            }
        }
    }

    /// <summary>Logs the game cancelling queued equips (it does when you sprint, jump or dodge).</summary>
    [HarmonyPatch(typeof(Player), "ClearActionQueue")]
    internal static class ActionQueueLogPatch
    {
        private static void Prefix(Player __instance)
        {
            if (WheelLog.Enabled.Value && __instance == Player.m_localPlayer && WheelLog.QueuedActions(__instance) > 0)
            {
                WheelLog.Write($"The game cancelled {WheelLog.QueuedActions(__instance)} queued action(s) (running {__instance.IsRunning()}, "
                    + $"on ground {__instance.IsOnGround()}, wheel open {Hud.InRadial()})");
            }
        }
    }
}
