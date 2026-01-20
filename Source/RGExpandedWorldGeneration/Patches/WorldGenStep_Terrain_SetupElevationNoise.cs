using HarmonyLib;
using RimWorld.Planet;
using Verse;

namespace RGExpandedWorldGeneration;

[HarmonyPatch(typeof(WorldGenStep_Terrain), nameof(WorldGenStep_Terrain.SetupElevationNoise))]
public static class WorldGenStep_Terrain_SetupElevationNoise
{
    public static void Prefix(ref FloatRange ___ElevationRange)
    {
        if (Page_CreateWorldParams_DoWindowContents.tmpWorldGenerationPreset is null)
        {
            Page_CreateWorldParams_DoWindowContents.tmpWorldGenerationPreset = new WorldGenerationPreset();
            Page_CreateWorldParams_DoWindowContents.tmpWorldGenerationPreset.Init();
        }

        ___ElevationRange =
            new FloatRange(-500f * Page_CreateWorldParams_DoWindowContents.tmpWorldGenerationPreset.seaLevel, 5000f);
    }
}