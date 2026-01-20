using System.Reflection;
using HarmonyLib;
using Verse;

namespace RGExpandedWorldGeneration;

[StaticConstructorOnStartup]
internal static class HarmonyInit
{
    static HarmonyInit()
    {
        new Harmony("RGExpandedWorldGeneration.Mod").PatchAll(Assembly.GetExecutingAssembly());
    }
}