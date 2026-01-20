using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RGExpandedWorldGeneration;

public class WorldGenerationPreset : IExposable
{
    public float ancientRoadDensity;
    public AxialTilt axialTilt;
    public Dictionary<string, int> biomeCommonalities;
    public Dictionary<string, int> biomeScoreOffsets;
    public List<string> factionCounts;
    public float factionRoadDensity;
    public float mountainDensity;
    public float planetCoverage;
    public float pollution;
    public OverallPopulation population;
    public OverallRainfall rainfall;
    public float riverDensity;
    public float seaLevel;
    public string seedString;
    public OverallTemperature temperature;

    public void ExposeData()
    {
        Scribe_Collections.Look(ref factionCounts, "factionCountsStrings", LookMode.Value);
        Scribe_Collections.Look(ref biomeCommonalities, "biomeCommonalities", LookMode.Value, LookMode.Value);
        Scribe_Collections.Look(ref biomeScoreOffsets, "biomeScoreOffsets", LookMode.Value, LookMode.Value);
        Scribe_Values.Look(ref seedString, "seedString");
        Scribe_Values.Look(ref planetCoverage, "planetCoverage");
        Scribe_Values.Look(ref rainfall, "rainfall");
        Scribe_Values.Look(ref temperature, "temperature");
        Scribe_Values.Look(ref population, "population");
        Scribe_Values.Look(ref riverDensity, "riverDensity");
        Scribe_Values.Look(ref ancientRoadDensity, "ancientRoadDensity");
        Scribe_Values.Look(ref factionRoadDensity, "settlementRoadDensity");
        Scribe_Values.Look(ref mountainDensity, "mountainDensity");
        Scribe_Values.Look(ref seaLevel, "seaLevel");
        Scribe_Values.Look(ref axialTilt, "axialTilt");
        Scribe_Values.Look(ref pollution, "pollution");
    }

    public void Init()
    {
        seedString = GenText.RandomSeedString();
        planetCoverage = 0.3f;
        rainfall = OverallRainfall.Normal;
        temperature = OverallTemperature.Normal;
        population = OverallPopulation.Normal;
        axialTilt = AxialTilt.Normal;
        if (ModsConfig.BiotechActive)
        {
            pollution = 0.5f;
        }

        ResetFactionCounts();
        Reset();
    }

    public void Reset()
    {
        riverDensity = 1f;
        ancientRoadDensity = 1f;
        factionRoadDensity = 1f;
        mountainDensity = 1f;
        seaLevel = 1f;
        axialTilt = AxialTilt.Normal;
        if (ModsConfig.BiotechActive)
        {
            pollution = 0.5f;
        }

        ResetBiomeCommonalities();
        ResetBiomeScoreOffsets();
    }

    public bool IsDifferentFrom(WorldGenerationPreset other)
    {
        if (seedString != other.seedString || planetCoverage != other.planetCoverage || rainfall != other.rainfall ||
            temperature != other.temperature
            || population != other.population || riverDensity != other.riverDensity ||
            ancientRoadDensity != other.ancientRoadDensity
            || factionRoadDensity != other.factionRoadDensity || mountainDensity != other.mountainDensity ||
            seaLevel != other.seaLevel || axialTilt != other.axialTilt || pollution != other.pollution)
        {
            return true;
        }

        if (factionCounts.Count != other.factionCounts.Count || !factionCounts.SetsEqual(other.factionCounts))
        {
            return true;
        }

        if (biomeCommonalities.Count != other.biomeCommonalities.Count ||
            !biomeCommonalities.ContentEquals(other.biomeCommonalities))
        {
            return true;
        }

        return biomeScoreOffsets.Count != other.biomeScoreOffsets.Count ||
               !biomeScoreOffsets.ContentEquals(other.biomeScoreOffsets);
    }

    public WorldGenerationPreset MakeCopy()
    {
        var copy = new WorldGenerationPreset
        {
            factionCounts = factionCounts,
            biomeCommonalities = biomeCommonalities.ToDictionary(x => x.Key, y => y.Value),
            biomeScoreOffsets = biomeScoreOffsets.ToDictionary(x => x.Key, y => y.Value),
            seedString = seedString,
            planetCoverage = planetCoverage,
            rainfall = rainfall,
            temperature = temperature,
            population = population,
            riverDensity = riverDensity,
            ancientRoadDensity = ancientRoadDensity,
            factionRoadDensity = factionRoadDensity,
            mountainDensity = mountainDensity,
            seaLevel = seaLevel,
            axialTilt = axialTilt,
            pollution = pollution
        };
        return copy;
    }

    private void ResetFactionCounts()
    {
        factionCounts = [];
        foreach (var configurableFaction in FactionGenerator.ConfigurableFactions)
        {
            for (var i = 0; i < configurableFaction.startingCountAtWorldCreation; i++)
            {
                factionCounts.Add(configurableFaction.defName);
            }
        }
    }

    public void ResetBiomeCommonalities()
    {
        biomeCommonalities = new Dictionary<string, int>();
        foreach (var biomeDef in DefDatabase<BiomeDef>.AllDefs)
        {
            biomeCommonalities.Add(biomeDef.defName, 10);
        }
    }

    public void ResetBiomeScoreOffsets()
    {
        biomeScoreOffsets = new Dictionary<string, int>();
        foreach (var biomeDef in DefDatabase<BiomeDef>.AllDefs)
        {
            biomeScoreOffsets.Add(biomeDef.defName, 0);
        }
    }

    private static T RandomEnum<T>()
    {
        var values = (T[])Enum.GetValues(typeof(T));
        return values[new Random(Guid.NewGuid().GetHashCode()).Next(0, values.Length)];
    }

    public void RandomizeValues()
    {
        var biomeDefs = DefDatabase<BiomeDef>.AllDefs;
        biomeCommonalities = new Dictionary<string, int>();
        foreach (var biomeDef in biomeDefs)
        {
            biomeCommonalities.Add(biomeDef.defName, 10 + Rand.RangeInclusive(-2, 2));
        }

        biomeScoreOffsets = new Dictionary<string, int>();
        foreach (var biomeDef in biomeDefs)
        {
            biomeScoreOffsets.Add(biomeDef.defName, 0 + Rand.RangeInclusive(-2, 2));
        }

        seedString = GenText.RandomSeedString();

        riverDensity = 1f + Rand.Range(-0.5f, 0.5f);
        ancientRoadDensity = 1f + Rand.Range(-0.5f, 0.5f);
        factionRoadDensity = 1f + Rand.Range(-0.5f, 0.5f);
        mountainDensity = 1f + Rand.Range(-0.5f, 0.5f);
        seaLevel = 1f + Rand.Range(-0.25f, 0.25f);
        if (ModsConfig.BiotechActive)
        {
            pollution = Rand.Range(0, 1f);
        }

        axialTilt = RandomEnum<AxialTilt>();
        rainfall = RandomEnum<OverallRainfall>();
        temperature = RandomEnum<OverallTemperature>();
        population = RandomEnum<OverallPopulation>();
    }
}