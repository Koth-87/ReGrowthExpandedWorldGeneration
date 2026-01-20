using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RGExpandedWorldGeneration;

[HarmonyPatch(typeof(WorldDrawLayerBase), nameof(WorldDrawLayerBase.RegenerateNow))]
public static class WorldDrawLayerBase_RegenerateNow
{
    public static bool Prefix()
    {
        return !Page_CreateWorldParams_DoWindowContents.dirty ||
               Find.WindowStack.WindowOfType<Page_CreateWorldParams>() == null;
    }
}