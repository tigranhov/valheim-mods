using System;
using System.Collections.Generic;
using HarmonyLib;
using Valheim.UI;

namespace BetterWheel
{
    /// <summary>
    /// What the wheel opens with. The game opens it with a config that picks emotes (emote key), a hover menu (looking
    /// at a fermenter, item stand, ...) or the category wheel; this keeps the first two and replaces the category wheel
    /// with all your items, or opens the list a wheel key asked for.
    /// </summary>
    [HarmonyPatch(typeof(OpenRadialConfig), nameof(OpenRadialConfig.InitRadialConfig))]
    internal static class OpenWheelPatch
    {
        private static readonly Func<OpenRadialConfig, RadialBase, bool> TryOpenHoverMenu =
            AccessTools.MethodDelegate<Func<OpenRadialConfig, RadialBase, bool>>(AccessTools.Method(typeof(OpenRadialConfig), "TryOpenNonDefaultRadials"));

        private static bool Prefix(OpenRadialConfig __instance, RadialBase radial)
        {
            bool fromWheelKey = Wheels.TryTakeRequest(out ItemGroupConfig requested);
            Wheels.ApplyRelease(fromWheelKey);
            if (!fromWheelKey && WheelConfig.Main.Value == MainWheel.Categories)
            {
                return true;
            }
            // As the game's own opening does.
            radial.OnInteractionDelay = delay => PlayerController.SetTakeInputDelay(delay);
            radial.ShouldAnimateIn = true;
            string opened;
            if (fromWheelKey)
            {
                opened = $"wheel \"{requested.GroupName}\"";
                radial.Open(requested);
            }
            else if (ZInput.GetButton("OpenEmote"))
            {
                opened = "emotes";
                radial.Open(RadialData.SO.EmoteGroupConfig);
            }
            else if (TryOpenHoverMenu(__instance, radial))
            {
                opened = "hover menu";
            }
            else
            {
                opened = "all items";
                radial.Open(Wheels.AllItems());
            }
            WheelLog.Write($"Opened {opened}, release to use {(RadialData.SO.EnableReleaseToUseMode ? "on" : "off")}");
            return false;
        }
    }

    /// <summary>Wheel keys close and switch the wheel, and letting go of the opening key after a hold uses what's selected.</summary>
    [HarmonyPatch(typeof(RadialConfigHelper), nameof(RadialConfigHelper.SetItemInteractionControls))]
    internal static class WheelControlsPatch
    {
        private static void Postfix(RadialBase radial)
        {
            // Release first, so the game's own "held and let go closes" doesn't close the wheel before the use.
            Func<bool> close = radial.GetClose;
            radial.GetClose = () => Wheels.Released(radial) || (close?.Invoke() ?? false) || Wheels.CloseRequested(radial);
        }
    }

    /// <summary>
    /// Several picks per opening: every item keeps the wheel open after use (the game keeps it open only for gear, and for
    /// food while you can still eat). Hover menus keep the game's rule, so a full fermenter still closes it.
    /// </summary>
    [HarmonyPatch(typeof(ItemGroupConfig), "AddElement")]
    internal static class KeepOpenPatch
    {
        private static void Postfix(List<RadialMenuElement> elements, RadialBase radial)
        {
            if (WheelConfig.AfterUse.Value != KeepOpen.UntilClosed || radial.IsHoverMenu || elements.Count == 0)
            {
                return;
            }
            RadialMenuElement element = elements[elements.Count - 1];
            element.AdvancedCloseOnInteract = null;
            element.CloseOnInteract = () => false;
        }
    }
}
