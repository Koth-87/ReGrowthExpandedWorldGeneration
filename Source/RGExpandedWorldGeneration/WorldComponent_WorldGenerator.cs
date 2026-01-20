using System.Collections.Generic;
using RimWorld.Planet;
using Verse;

namespace RGExpandedWorldGeneration;

public class WorldComponent_WorldGenerator : WorldComponent
{
    public static readonly Dictionary<AxialTilt, SimpleCurve> mappedValues = new()
    {
        {
            AxialTilt.VeryLow, [
                new CurvePoint(0f, 0.75f),
                new CurvePoint(0.1f, 1f),
                new CurvePoint(1f, 7f)
            ]
        },
        {
            AxialTilt.Low, [
                new CurvePoint(0f, 1.5f),
                new CurvePoint(0.1f, 2f),
                new CurvePoint(1f, 14f)
            ]
        },
        {
            AxialTilt.Normal, [
                new CurvePoint(0f, 3f),
                new CurvePoint(0.1f, 4f),
                new CurvePoint(1f, 28f)
            ]
        },
        {
            AxialTilt.High, [
                new CurvePoint(0f, 4.5f),
                new CurvePoint(0.1f, 6f),
                new CurvePoint(1f, 42f)
            ]
        },
        {
            AxialTilt.VeryHigh, [
                new CurvePoint(0f, 6f),
                new CurvePoint(0.1f, 8f),
                new CurvePoint(1f, 56f)
            ]
        }
    };

    public static WorldComponent_WorldGenerator Instance;
    public AxialTilt axialTilt = AxialTilt.Normal;

    private bool worldGenerated;

    public WorldComponent_WorldGenerator(World world) : base(world)
    {
        Instance = this;
    }

    public override void FinalizeInit(bool fromLoad)
    {
        base.FinalizeInit(fromLoad);
        if (worldGenerated || RGExpandedWorldGenerationSettings.curWorldGenerationPreset == null)
        {
            return;
        }

        axialTilt = RGExpandedWorldGenerationSettings.curWorldGenerationPreset.axialTilt;
        worldGenerated = true;
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref worldGenerated, "worldGenerated");
        Scribe_Values.Look(ref axialTilt, "axialTilt", AxialTilt.Normal, true);
        Instance = this;
    }
}