using HarmonyLib;
using ImmersiveMapper.Shared;

namespace ImmersiveMapper.CartLashing
{
    // Every cart can be lashed, modded ones too: anything with the vanilla cart script. Not build ghosts (no ZDO).
    [HarmonyPatch(typeof(Vagon), "Awake")]
    internal static class CartAwakePatch
    {
        private static void Postfix(Vagon __instance)
        {
            if (__instance.m_nview == null || __instance.m_nview.GetZDO() == null || __instance.GetComponent<CartLash>() != null)
            {
                return;
            }
            __instance.gameObject.AddComponent<ShipPassenger>();
            __instance.gameObject.AddComponent<CartLash>();
        }
    }

    [HarmonyPatch(typeof(Vagon), nameof(Vagon.GetHoverText))]
    internal static class CartHoverPatch
    {
        private static void Postfix(Vagon __instance, ref string __result)
        {
            CartLash lash = __instance.GetComponent<CartLash>();
            if (lash != null)
            {
                __result += lash.HoverSuffix();
            }
        }
    }

    // Shift+E lashes or unties; a lashed cart can't be pulled.
    [HarmonyPatch(typeof(Vagon), nameof(Vagon.Interact))]
    internal static class CartInteractPatch
    {
        private static bool Prefix(Vagon __instance, Humanoid character, bool hold, bool alt, ref bool __result)
        {
            CartLash lash = __instance.GetComponent<CartLash>();
            if (lash == null || hold)
            {
                return true;
            }
            if (alt)
            {
                lash.Toggle(character);
                __result = true;
                return false;
            }
            if (lash.IsLashed)
            {
                character.Message(MessageHud.MessageType.Center, "Untie the cart first ($KEY_AltPlace + $KEY_Use)");
                __result = false;
                return false;
            }
            return true;
        }
    }

    // The cart's storage is a part of its own: Shift+E there works the same, and E still opens it when lashed.
    [HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
    internal static class CartStorageHoverPatch
    {
        private static void Postfix(Container __instance, ref string __result)
        {
            CartLash lash = __instance.GetComponentInParent<CartLash>();
            if (lash != null)
            {
                __result += lash.HoverSuffix();
            }
        }
    }

    [HarmonyPatch(typeof(Container), nameof(Container.Interact))]
    internal static class CartStorageInteractPatch
    {
        private static bool Prefix(Container __instance, Humanoid character, bool hold, bool alt, ref bool __result)
        {
            CartLash lash = alt && !hold ? __instance.GetComponentInParent<CartLash>() : null;
            if (lash == null)
            {
                return true;
            }
            lash.Toggle(character);
            __result = true;
            return false;
        }
    }
}
