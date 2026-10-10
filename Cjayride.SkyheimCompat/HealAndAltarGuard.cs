using System.Reflection;
using HarmonyLib;

namespace Cjayride.SkyheimCompat
{
    [HarmonyPatch(typeof(SkyheimPieceData), nameof(SkyheimPieceData.OnBeforeSerialize))]
    internal static class SkyheimPieceData_OnBeforeSerialize_Patch
    {
        private static readonly FieldInfo Resources =
            AccessTools.Field(typeof(SkyheimPieceData), "_resources");

        private static bool Prefix(SkyheimPieceData __instance)
        {
            return Resources?.GetValue(__instance) != null;
        }
    }
}
