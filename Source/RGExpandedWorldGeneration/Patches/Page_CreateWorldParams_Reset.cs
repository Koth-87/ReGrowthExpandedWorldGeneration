using HarmonyLib;
using RimWorld;

namespace RGExpandedWorldGeneration;

[HarmonyPatch(typeof(Page_CreateWorldParams), nameof(Page_CreateWorldParams.Reset))]
public static class Page_CreateWorldParams_Reset
{
    public static void Postfix()
    {
        if (Page_CreateWorldParams_DoWindowContents.tmpWorldGenerationPreset != null)
        {
            Page_CreateWorldParams_DoWindowContents.tmpWorldGenerationPreset.Reset();
        }
    }
}