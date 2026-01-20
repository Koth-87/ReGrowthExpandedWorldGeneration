using Verse;

namespace RGExpandedWorldGeneration;

[StaticConstructorOnStartup]
public static class ModCompat
{
    public static readonly bool MyLittlePlanetActive;

    static ModCompat()
    {
        MyLittlePlanetActive = ModLister.GetActiveModWithIdentifier("Oblitus.MyLittlePlanet", true) != null;
    }
}