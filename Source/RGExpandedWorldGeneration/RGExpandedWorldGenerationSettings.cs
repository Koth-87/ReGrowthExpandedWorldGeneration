using System.Collections.Generic;
using Verse;

namespace RGExpandedWorldGeneration;

internal class RGExpandedWorldGenerationSettings : ModSettings
{
    public static WorldGenerationPreset curWorldGenerationPreset;

    public Dictionary<string, WorldGenerationPreset> presets = new();
    public bool showPreview = true;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref presets, "presets", LookMode.Value, LookMode.Deep);
        Scribe_Values.Look(ref showPreview, "showPreview", true);
    }
}