using HarmonyLib;
using RimWorld;
using Verse;

namespace RGExpandedWorldGeneration;

[HarmonyPatch(typeof(Page_CreateWorldParams), nameof(Page_CreateWorldParams.PreOpen))]
public static class Page_CreateWorldParams_PreOpen
{
    private static void Prefix()
    {
        Log.Message("[RG] PreOpen: Page is opening, initializing preview generation");
        Page_CreateWorldParams_DoWindowContents.startFresh = true;
    }
}