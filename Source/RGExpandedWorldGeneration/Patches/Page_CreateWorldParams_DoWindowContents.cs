using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Threading;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Noise;
using Verse.Profile;

namespace RGExpandedWorldGeneration;

[StaticConstructorOnStartup]
[HarmonyPatch(typeof(Page_CreateWorldParams), nameof(Page_CreateWorldParams.DoWindowContents))]
public static class Page_CreateWorldParams_DoWindowContents
{
    private const int WorldCameraHeight = 315;
    private const int WorldCameraWidth = 315;

    private static readonly Color BackgroundColor = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, 15);
    private static readonly Texture2D GeneratePreview = ContentFinder<Texture2D>.Get("UI/GeneratePreview");
    private static readonly Texture2D Visible = ContentFinder<Texture2D>.Get("UI/Visible");
    private static readonly Texture2D InVisible = ContentFinder<Texture2D>.Get("UI/InVisible");
    private static readonly Texture2D saveTexture2D = ContentFinder<Texture2D>.Get("UI/Misc/BarInstantMarkerRotated");
    private static readonly Texture2D loadTexture2D = ContentFinder<Texture2D>.Get("UI/Misc/BarInstantMarker");

    public static WorldGenerationPreset tmpWorldGenerationPreset;

    private static Vector2 scrollPosition;

    public static bool dirty;

    private static Texture2D worldPreview;

    private static World threadedWorld;

    public static Thread thread;

    private static int updatePreviewCounter;

    private static float texSpinAngle;

    public static bool startFresh;

    private static volatile bool shouldCancelGeneration;

    // Preview progress tracking
    private static volatile int previewStepsDone;
    private static volatile int previewStepsTotal;

    private static readonly HashSet<WorldGenStepDef> worldGenStepDefs =
    [
        DefDatabase<WorldGenStepDef>.GetNamed("Tiles"),
        DefDatabase<WorldGenStepDef>.GetNamed("Terrain"),
        DefDatabase<WorldGenStepDef>.GetNamed("Lakes"),
        DefDatabase<WorldGenStepDef>.GetNamed("Rivers"),
        DefDatabase<WorldGenStepDef>.GetNamed("AncientSites"),
        DefDatabase<WorldGenStepDef>.GetNamed("AncientRoads"),
        DefDatabase<WorldGenStepDef>.GetNamed("Roads")
    ];

    public static bool generatingWorld;
    private static List<GameSetupStepDef> cachedGenSteps;

    private static List<GameSetupStepDef> GameSetupStepsInOrder => cachedGenSteps ?? (cachedGenSteps =
        (from x in DefDatabase<GameSetupStepDef>.AllDefs
            orderby x.order, x.index
            select x).ToList());


    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var planetCoverage =
            AccessTools.Field(typeof(Page_CreateWorldParams), nameof(Page_CreateWorldParams.planetCoverage));
        var doGlobeCoverageSliderMethod =
            AccessTools.Method(typeof(Page_CreateWorldParams_DoWindowContents), nameof(DoGlobeCoverageSlider));
        var doGuiMethod = AccessTools.Method(typeof(Page_CreateWorldParams_DoWindowContents), nameof(DoGui));
        var endGroupMethod = AccessTools.Method(typeof(Widgets), nameof(Widgets.EndGroup));

        var codes = instructions.ToList();
        var found = false;

        for (var i = 0; i < codes.Count; i++)
        {
            var code = codes[i];

            if (codes[i].opcode == OpCodes.Ldloc_S && codes[i].operand is LocalBuilder { LocalIndex: 11 } &&
                i + 2 < codes.Count && codes[i + 2].LoadsField(planetCoverage))
            {
                var i1 = i;
                i += codes.FirstIndexOf(x =>
                    x.Calls(AccessTools.Method(typeof(WindowStack), nameof(WindowStack.Add))) &&
                    codes.IndexOf(x) > i1) - i;
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldloc_S, 11);
                yield return new CodeInstruction(OpCodes.Call, doGlobeCoverageSliderMethod);
            }
            else
            {
                yield return code;
            }

            if (found)
            {
                continue;
            }

            // Inject doGuiMethod before EndGroup() call
            // Look backwards from EndGroup to find the proper local variable indices
            if (!codes[i].Calls(endGroupMethod))
            {
                continue;
            }

            // Search backwards for Ldloca_S and Ldloc_S to find num and width2 variable indices
            byte numIndex = 7; // Default fallback
            byte width2Index = 8; // Default fallback

            for (var j = i - 1; j >= Math.Max(0, i - 20); j--)
            {
                if (codes[j].opcode != OpCodes.Ldloc_S || codes[j].operand is not byte locIndex)
                {
                    continue;
                }

                width2Index = locIndex;
                break;
            }

            for (var j = i - 1; j >= Math.Max(0, i - 30); j--)
            {
                if (codes[j].opcode != OpCodes.Ldloca_S || codes[j].operand is not byte refIndex)
                {
                    continue;
                }

                numIndex = refIndex;
                break;
            }

            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Ldloca_S, numIndex);
            yield return new CodeInstruction(OpCodes.Ldloc_S, width2Index);
            yield return new CodeInstruction(OpCodes.Call, doGuiMethod);
            found = true;
        }
    }

    public static void DoBottomButtons(Page_CreateWorldParams window, Rect rect, string nextLabel = null,
        string midLabel = null, Action midAct = null, bool showNext = true, bool doNextOnKeypress = true)
    {
        var y = rect.y + rect.height - 38f;
        Text.Font = GameFont.Small;
        string label = "Back".Translate();
        var canDoBackMethod =
            AccessTools.Method(typeof(Page_CreateWorldParams), nameof(Page_CreateWorldParams.CanDoBack));
        var doBackMethod = AccessTools.Method(typeof(Page_CreateWorldParams), nameof(Page_CreateWorldParams.DoBack));
        var canDoNextMethod =
            AccessTools.Method(typeof(Page_CreateWorldParams), nameof(Page_CreateWorldParams.CanDoNext));
        var doNextMethod = AccessTools.Method(typeof(Page_CreateWorldParams), nameof(Page_CreateWorldParams.DoNext));
        var buttonSpacer = Page.BottomButSize.x + 15;
        var currentX = rect.x;
        var backRect = new Rect(currentX, y, Page.BottomButSize.x, Page.BottomButSize.y);
        if ((Widgets.ButtonText(backRect, label)
             || KeyBindingDefOf.Cancel.KeyDownEvent) && (bool)canDoBackMethod.Invoke(window, []))
        {
            doBackMethod.Invoke(window, []);
        }

        if (showNext)
        {
            if (nextLabel.NullOrEmpty())
            {
                nextLabel = "Next".Translate();
            }

            var rect2 = new Rect(rect.xMax - Page.BottomButSize.x, y, Page.BottomButSize.x, Page.BottomButSize.y);
            if ((Widgets.ButtonText(rect2, nextLabel) || doNextOnKeypress && KeyBindingDefOf.Accept.KeyDownEvent) &&
                (bool)canDoNextMethod.Invoke(window, []))
            {
                Log.Message("[RG] DoBottomButtons: Next button pressed, setting nextPressed to true");
                startFresh = true;
                doNextMethod.Invoke(window, []);
            }

            UIHighlighter.HighlightOpportunity(rect2, "NextPage");
        }

        if (midAct != null)
        {
            currentX += buttonSpacer;
            var midActRect = new Rect(currentX, y, Page.BottomButSize.x, Page.BottomButSize.y);
            if (Widgets.ButtonText(midActRect, midLabel))
            {
                midAct();
            }
        }

        currentX += buttonSpacer;
        var savePresetRect = new Rect(currentX, y, Page.BottomButSize.x / 2, Page.BottomButSize.y);
        string labelSavePreset = "RG.SavePreset".Translate();
        TooltipHandler.TipRegion(savePresetRect, labelSavePreset);
        if (Widgets.ButtonImageFitted(savePresetRect, saveTexture2D))
        {
            var saveWindow = new Dialog_PresetList_Save(window);
            Find.WindowStack.Add(saveWindow);
        }

        currentX += Page.BottomButSize.x / 2;
        var loadPresetRect = new Rect(currentX, y, Page.BottomButSize.x / 2, Page.BottomButSize.y);
        string labelLoadPreset = "RG.LoadPreset".Translate();
        TooltipHandler.TipRegion(loadPresetRect, labelLoadPreset);
        if (Widgets.ButtonImageFitted(loadPresetRect, loadTexture2D))
        {
            var loadWindow = new Dialog_PresetList_Load(window);
            Find.WindowStack.Add(loadWindow);
        }

        var randomizeRect = new Rect(rect.xMax - Page.BottomButSize.x - buttonSpacer, y, Page.BottomButSize.x,
            Page.BottomButSize.y);
        string randomize = "Randomize".Translate();
        if (!Widgets.ButtonText(randomizeRect, randomize))
        {
            return;
        }

        tmpWorldGenerationPreset.RandomizeValues();
        ApplyChanges(window);
    }

    private static void Postfix(Page_CreateWorldParams __instance)
    {
        if (startFresh)
        {
            return;
        }

        doWorldPreviewArea(__instance);
    }

    private static void Prefix()
    {
        // If returning from next page, force a complete preview regeneration
        if (!startFresh)
        {
            return;
        }

        Log.Message("[RG] Postfix: Detected fresh start, forcing preview regeneration");
        startFresh = false;
        dirty = true;
        updatePreviewCounter = 0;
        Find.GameInitData.ResetWorldRelatedMapInitData();
    }

    private static void DoGlobeCoverageSlider(Page_CreateWorldParams window, Rect rect)
    {
        var planetCoverage =
            (float)AccessTools.Field(typeof(Page_CreateWorldParams), nameof(Page_CreateWorldParams.planetCoverage))
                .GetValue(window);
        var value = (double)Widgets.HorizontalSlider(rect, planetCoverage, 0.05f, 1, false,
            $"{planetCoverage * 100}%", "RG.Small".Translate(), "RG.Large".Translate()) * 100;
        AccessTools.Field(typeof(Page_CreateWorldParams), nameof(Page_CreateWorldParams.planetCoverage))
            .SetValue(window, (float)Math.Round(value / 5) * 5 / 100);
    }

    private static void DoGui(Page_CreateWorldParams window, ref float num, float width2)
    {
        updateCurPreset(window);
        num += 40f;
        doSlider(0, ref num, width2, "RG.RiverDensity".Translate(), ref tmpWorldGenerationPreset.riverDensity,
            "None".Translate());

        Rect labelRect;
        if (!ModCompat.MyLittlePlanetActive)
        {
            num += 40f;
            labelRect = new Rect(0, num, 200f, 30f);
            var slider = new Rect(labelRect.xMax, num, width2, 30f);
            Widgets.Label(labelRect, "RG.AxialTilt".Translate());
            tmpWorldGenerationPreset.axialTilt = (AxialTilt)Mathf.RoundToInt(Widgets.HorizontalSlider(slider,
                (float)tmpWorldGenerationPreset.axialTilt, 0f, AxialTiltUtility.EnumValuesCount - 1, true,
                "PlanetRainfall_Normal".Translate(), "PlanetRainfall_Low".Translate(),
                "PlanetRainfall_High".Translate(), 1f));
        }

        if (RGExpandedWorldGenerationSettingsMod.settings.showPreview)
        {
            labelRect = new Rect(0f, num + 40, 80, 30);
            Widgets.Label(labelRect, "RG.Biomes".Translate());
            var outRect = new Rect(labelRect.x, labelRect.yMax - 3, width2 + 195,
                WorldFactionsUIUtility_DoWindowContents.LowerWidgetHeight - 50);
            var viewRect = new Rect(outRect.x, outRect.y, outRect.width - 16f,
                (DefDatabase<BiomeDef>.DefCount * 90) + 10);
            var rect3 = new Rect(outRect.xMax - 200f - 16f, labelRect.y, 200f, Text.LineHeight);


            Widgets.DrawBoxSolid(new Rect(outRect.x, outRect.y, outRect.width - 16f, outRect.height), BackgroundColor);
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            num = outRect.y + 15;
            foreach (var biomeDef in DefDatabase<BiomeDef>.AllDefs.OrderBy(x => x.label ?? x.defName))
            {
                doBiomeSliders(biomeDef, 10, ref num, biomeDef.label?.CapitalizeFirst() ?? biomeDef.defName);
            }

            num -= 50f;
            Widgets.EndScrollView();
            if (tmpWorldGenerationPreset.biomeCommonalities.Any(x => x.Value != 10) ||
                tmpWorldGenerationPreset.biomeScoreOffsets.Any(y => y.Value != 0))
            {
                if (Widgets.ButtonText(rect3, "RG.ResetBiomesToDefault".Translate()))
                {
                    tmpWorldGenerationPreset.ResetBiomeCommonalities();
                    tmpWorldGenerationPreset.ResetBiomeScoreOffsets();
                }
            }
        }
        else
        {
            doSlider(0, ref num, width2, "RG.MountainDensity".Translate(), ref tmpWorldGenerationPreset.mountainDensity,
                "None".Translate());
            doSlider(0, ref num, width2, "RG.SeaLevel".Translate(), ref tmpWorldGenerationPreset.seaLevel,
                "None".Translate());
            doSlider(0, ref num, width2, "RG.AncientRoadDensity".Translate(),
                ref tmpWorldGenerationPreset.ancientRoadDensity, "None".Translate());
            doSlider(0, ref num, width2, "RG.FactionRoadDensity".Translate(),
                ref tmpWorldGenerationPreset.factionRoadDensity, "None".Translate());
            if (!ModCompat.MyLittlePlanetActive)
            {
                return;
            }

            num += 40;
            labelRect = new Rect(0, num, 200f, 30f);
            var slider = new Rect(labelRect.xMax, num, 256, 30f);
            Widgets.Label(labelRect, "RG.AxialTilt".Translate());
            tmpWorldGenerationPreset.axialTilt = (AxialTilt)Mathf.RoundToInt(Widgets.HorizontalSlider(slider,
                (float)tmpWorldGenerationPreset.axialTilt, 0f, AxialTiltUtility.EnumValuesCount - 1, true,
                "PlanetRainfall_Normal".Translate(), "PlanetRainfall_Low".Translate(),
                "PlanetRainfall_High".Translate(), 1f));
        }

        if (RGExpandedWorldGenerationSettings.curWorldGenerationPreset is null)
        {
            RGExpandedWorldGenerationSettings.curWorldGenerationPreset = tmpWorldGenerationPreset.MakeCopy();
        }
        else if (RGExpandedWorldGenerationSettings.curWorldGenerationPreset.IsDifferentFrom(tmpWorldGenerationPreset))
        {
            RGExpandedWorldGenerationSettings.curWorldGenerationPreset = tmpWorldGenerationPreset.MakeCopy();
            updatePreviewCounter = 60;
            if (thread is { IsAlive: true })
            {
                Log.Message("[RG] DoGui: Requesting cancellation of current world generation");
                shouldCancelGeneration = true;
            }
        }

        if (thread is null)
        {
            if (updatePreviewCounter == 0)
            {
                startRefreshWorldPreview(window);
            }
        }
        else if (!thread.IsAlive && shouldCancelGeneration)
        {
            Log.Message("[RG] DoGui: Previous thread finished, starting new generation immediately");
            thread = null;
            shouldCancelGeneration = false;
            updatePreviewCounter = 0;
            startRefreshWorldPreview(window);
        }

        if (updatePreviewCounter > -2)
        {
            updatePreviewCounter--;
        }
    }

    private static void doWorldPreviewArea(Page_CreateWorldParams window)
    {
        var previewAreaRect = new Rect(545, 10, WorldCameraHeight, WorldCameraWidth);
        var generateButtonRect = new Rect(previewAreaRect.xMax - 35, previewAreaRect.y, 35, 35);

        var hideButtonRect = generateButtonRect;
        hideButtonRect.x += generateButtonRect.width * 1.1f;
        drawHidePreviewButton(window, hideButtonRect);
        Rect labelRect;
        if (RGExpandedWorldGenerationSettingsMod.settings.showPreview)
        {
            drawGeneratePreviewButton(window, generateButtonRect);
            var numAttempt = 0;
            if (thread is null && Find.World != null && Find.World.info.name != "DefaultWorldName" ||
                worldPreview != null)
            {
                if (dirty)
                {
                    while (numAttempt < 5)
                    {
                        worldPreview = getWorldCameraPreview(WorldCameraHeight, WorldCameraWidth);
                        if (worldPreview == null || isBlack(worldPreview))
                        {
                            numAttempt++;
                        }
                        else
                        {
                            dirty = false;
                            break;
                        }
                    }
                }

                if (worldPreview != null)
                {
                    GUI.DrawTexture(previewAreaRect, worldPreview);
                }
            }

            var numY = previewAreaRect.yMax - 40;
            if (tmpWorldGenerationPreset is null)
            {
                tmpWorldGenerationPreset = new WorldGenerationPreset();
                tmpWorldGenerationPreset.Init();
            }

            doSlider(previewAreaRect.x - 55, ref numY, 256, "RG.MountainDensity".Translate(),
                ref tmpWorldGenerationPreset.mountainDensity,
                "None".Translate());
            doSlider(previewAreaRect.x - 55, ref numY, 256, "RG.SeaLevel".Translate(),
                ref tmpWorldGenerationPreset.seaLevel,
                "None".Translate());

            doSlider(previewAreaRect.x - 55, ref numY, 256, "RG.AncientRoadDensity".Translate(),
                ref tmpWorldGenerationPreset.ancientRoadDensity, "None".Translate());
            doSlider(previewAreaRect.x - 55, ref numY, 256, "RG.FactionRoadDensity".Translate(),
                ref tmpWorldGenerationPreset.factionRoadDensity, "None".Translate());

            if (!ModCompat.MyLittlePlanetActive)
            {
                return;
            }

            numY += 40;
            labelRect = new Rect(previewAreaRect.x - 55, numY, 200f, 30f);
            var slider = new Rect(labelRect.xMax, numY, 256, 30f);
            Widgets.Label(labelRect, "RG.AxialTilt".Translate());
            tmpWorldGenerationPreset.axialTilt = (AxialTilt)Mathf.RoundToInt(Widgets.HorizontalSlider(slider,
                (float)tmpWorldGenerationPreset.axialTilt, 0f, AxialTiltUtility.EnumValuesCount - 1, true,
                "PlanetRainfall_Normal".Translate(), "PlanetRainfall_Low".Translate(),
                "PlanetRainfall_High".Translate(), 1f));
            return;
        }

        // Ensure tmpWorldGenerationPreset is initialized before using it
        if (tmpWorldGenerationPreset is null)
        {
            tmpWorldGenerationPreset = new WorldGenerationPreset();
            tmpWorldGenerationPreset.Init();
        }

        labelRect = new Rect(previewAreaRect.x - 55, previewAreaRect.y + hideButtonRect.height,
            455, 25);
        Widgets.Label(labelRect, "RG.Biomes".Translate());
        var outRect = new Rect(labelRect.x, labelRect.yMax - 3, labelRect.width,
            previewAreaRect.height);
        var viewRect = new Rect(outRect.x, outRect.y, outRect.width - 16f,
            (DefDatabase<BiomeDef>.DefCount * 90) + 10);
        var rect3 = new Rect(outRect.xMax - 200f - 16f, labelRect.y, 200f, Text.LineHeight);

        Widgets.DrawBoxSolid(new Rect(outRect.x, outRect.y, outRect.width - 16f, outRect.height), BackgroundColor);
        Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
        var num = outRect.y + 15;
        foreach (var biomeDef in DefDatabase<BiomeDef>.AllDefs.OrderBy(x => x.label ?? x.defName))
        {
            doBiomeSliders(biomeDef, labelRect.x + 10, ref num,
                biomeDef.label?.CapitalizeFirst() ?? biomeDef.defName);
        }

        Widgets.EndScrollView();
        if (tmpWorldGenerationPreset.biomeCommonalities.All(x => x.Value == 10) &&
            tmpWorldGenerationPreset.biomeScoreOffsets.All(y => y.Value == 0))
        {
            return;
        }

        if (!Widgets.ButtonText(rect3, "RG.ResetBiomesToDefault".Translate()))
        {
            return;
        }

        tmpWorldGenerationPreset.ResetBiomeCommonalities();
        tmpWorldGenerationPreset.ResetBiomeScoreOffsets();
    }

    public static void ApplyChanges(Page_CreateWorldParams window)
    {
        window.rainfall = tmpWorldGenerationPreset.rainfall;
        window.population = tmpWorldGenerationPreset.population;
        window.planetCoverage = tmpWorldGenerationPreset.planetCoverage;
        window.seedString = tmpWorldGenerationPreset.seedString;
        window.temperature = tmpWorldGenerationPreset.temperature;
        if (ModsConfig.BiotechActive)
        {
            window.pollution = tmpWorldGenerationPreset.pollution;
        }
    }

    private static bool isBlack(Texture2D texture)
    {
        var pixel = texture.GetPixel(texture.width / 2, texture.height / 2);
        return pixel.r <= 0 && pixel is { g: <= 0, b: <= 0 };
    }

    private static void startRefreshWorldPreview(Page_CreateWorldParams window)
    {
        dirty = false;
        updatePreviewCounter = -1;

        if (thread is { IsAlive: true })
        {
            Log.Message("[RG] startRefreshWorldPreview: Thread already running, requesting cancellation");
            shouldCancelGeneration = true;
            return;
        }

        if (!RGExpandedWorldGenerationSettingsMod.settings.showPreview)
        {
            return;
        }

        // reset progress
        previewStepsDone = 0;
        previewStepsTotal = 0;

        Log.Message("[RG] startRefreshWorldPreview: Starting new world generation thread");
        shouldCancelGeneration = false;
        thread = new Thread(delegate() { generateWorld(window); });
        thread.Start();
    }

    private static void drawHidePreviewButton(Page_CreateWorldParams window, Rect hideButtonRect)
    {
        var buttonTexture = Visible;
        if (!RGExpandedWorldGenerationSettingsMod.settings.showPreview)
        {
            buttonTexture = InVisible;
        }

        if (Widgets.ButtonImageFitted(hideButtonRect, buttonTexture))
        {
            RGExpandedWorldGenerationSettingsMod.settings.showPreview =
                !RGExpandedWorldGenerationSettingsMod.settings.showPreview;
            RGExpandedWorldGenerationSettingsMod.settings.Write();
            if (RGExpandedWorldGenerationSettingsMod.settings.showPreview)
            {
                startRefreshWorldPreview(window);
            }
        }

        Widgets.DrawHighlightIfMouseover(hideButtonRect);
        TooltipHandler.TipRegion(hideButtonRect, "RG.HidePreview".Translate());
    }

    private static void drawGeneratePreviewButton(Page_CreateWorldParams window, Rect generateButtonRect)
    {
        if (thread != null)
        {
            if (texSpinAngle > 360f)
            {
                texSpinAngle -= 360f;
            }

            texSpinAngle += 3;
        }

        // Draw progress bar to the left of the spinner when generating
        if (thread != null)
        {
            var pct = 0f;
            var total = previewStepsTotal;
            if (total > 0)
            {
                pct = Mathf.Clamp01((float)previewStepsDone / total);
            }

            Widgets.FillableBar(generateButtonRect, pct);
            TooltipHandler.TipRegion(generateButtonRect, total > 0 ? $"{previewStepsDone}/{previewStepsTotal}" : "");
        }

        if (Prefs.UIScale != 1f)
        {
            GUI.DrawTexture(generateButtonRect, GeneratePreview);
        }
        else
        {
            Widgets.DrawTextureRotated(generateButtonRect, GeneratePreview, texSpinAngle);
        }

        if (Mouse.IsOver(generateButtonRect))
        {
            Widgets.DrawHighlightIfMouseover(generateButtonRect);
            if (Event.current.type == EventType.MouseDown)
            {
                if (Event.current.button == 0)
                {
                    startRefreshWorldPreview(window);
                    Event.current.Use();
                }
            }
        }

        if (thread == null || thread.IsAlive || threadedWorld == null)
        {
            return;
        }

        initializeWorld();
        threadedWorld = null;
        thread = null;
        dirty = true;
        generatingWorld = false;
    }

    private static void initializeWorld()
    {
        // Regenerate critical draw layers if available
        var layers = Find.World.renderer.AllDrawLayers;
        foreach (var layer in layers)
        {
            if (layer is WorldDrawLayer_Hills or WorldDrawLayer_Rivers or WorldDrawLayer_Roads
                or WorldDrawLayer_Terrain)
            {
                layer.RegenerateNow();
            }
        }

        // Safely finalize world components if a world exists
        if (Find.World == null || Find.World.components == null)
        {
            return;
        }

        var comps = Find.World.components.Where(x => x.GetType().Name == "TacticalGroups");
        foreach (var comp in comps)
        {
            try
            {
                comp.FinalizeInit(false);
            }
            catch (Exception ex)
            {
                Log.Warning($"[RG] initializeWorld: Error finalizing component {comp.GetType().Name}: {ex.Message}");
            }
        }
    }

    private static void generateWorld(Page_CreateWorldParams page)
    {
        generatingWorld = true;
        Log.Message("[RG] generateWorld: Starting world generation");

        var prevProgramState = Current.ProgramState;
        var prevFaction = Find.World?.factionManager?.OfPlayer;

        Rand.PushState();

        try
        {
            Log.Message("[RG] generateWorld: Setting seed");
            var seed = Rand.Seed = GenText.StableStringHash(page.seedString);
            Current.ProgramState = ProgramState.Entry;

            Log.Message("[RG] generateWorld: Previous state saved");

            // Clear existing world reference to ensure clean generation
            if (Current.Game.World != null && Current.Game.World != Find.World)
            {
                Log.Message("[RG] generateWorld: Clearing previous preview world from Current.Game.World");
                Current.Game.World = null;
            }

            if (prevFaction is null)
            {
                Log.Message("[RG] generateWorld: Resetting world-related map init data");
                Find.GameInitData.ResetWorldRelatedMapInitData();
            }

            Log.Message("[RG] generateWorld: Creating new world instance");
            Current.CreatingWorld = new World
            {
                renderer = new WorldRenderer(),
                UI = new WorldInterface(),
                factionManager = new FactionManager(),
                info =
                {
                    seedString = page.seedString,
                    planetCoverage = page.planetCoverage,
                    overallRainfall = page.rainfall,
                    overallTemperature = page.temperature,
                    overallPopulation = page.population,
                    pollution = ModsConfig.BiotechActive ? page.pollution : 0f,
                    name = NameGenerator.GenerateName(RulePackDefOf.NamerWorld)
                }
            };

            if (Current.CreatingWorld == null)
            {
                Log.Error("[RG] generateWorld: Failed to create Current.CreatingWorld");
                return;
            }

            Log.Message("[RG] generateWorld: Setting up world properties");
            Current.CreatingWorld.factionManager.ofPlayer = prevFaction;
            Current.CreatingWorld.dynamicDrawManager = new WorldDynamicDrawManager();
            Current.CreatingWorld.ticksAbsCache = new ConfiguredTicksAbsAtGameStartCache();
            Current.Game.InitData.playerFaction = prevFaction;

            Log.Message($"[RG] generateWorld: World name set to {Current.CreatingWorld.info.name}");

            // Run game setup steps - this creates the grid and initializes world structure
            Log.Message("[RG] generateWorld: Running game setup steps");
            previewStepsDone = 0;
            previewStepsTotal = GameSetupStepsInOrder.Count;
            foreach (var item in GameSetupStepsInOrder)
            {
                if (shouldCancelGeneration)
                {
                    Log.Message("[RG] generateWorld: Cancellation requested during setup steps");
                    return;
                }

                Rand.Seed = Gen.HashCombineInt(seed, item.setupStep.SeedPart);
                Log.Message($"[RG] generateWorld: Running setup step: {item.defName}");
                item.setupStep.GenerateFresh();
                previewStepsDone++;
            }

            // Check both Current.CreatingWorld.grid and Find.WorldGrid for layers
            Log.Message("[RG] generateWorld: Starting planet layer generation");
            var tmpGenSteps = new List<WorldGenStepDef>();

            // After setup steps, the grid should be available - use whichever one exists
            var activeGrid = Find.WorldGrid ?? Current.CreatingWorld.grid;

            // Pre-calc total world gen steps for progress
            try
            {
                var totalWorldGenSteps = 0;
                foreach (var layerKvp in activeGrid.PlanetLayers)
                {
                    totalWorldGenSteps += layerKvp.Value.Def.GenStepsInOrder.Count(s => worldGenStepDefs.Contains(s));
                }

                previewStepsTotal += totalWorldGenSteps;
            }
            catch
            {
                // ignore counting issues
            }

            foreach (var planetLayer in activeGrid.PlanetLayers)
            {
                if (shouldCancelGeneration)
                {
                    Log.Message("[RG] generateWorld: Cancellation requested during planet layer processing");
                    return;
                }

                Log.Message($"[RG] generateWorld: Processing planet layer: {planetLayer.Key}");
                tmpGenSteps.Clear();
                tmpGenSteps.AddRange(planetLayer.Value.Def.GenStepsInOrder);

                for (var i = 0; i < tmpGenSteps.Count; i++)
                {
                    if (shouldCancelGeneration)
                    {
                        Log.Message("[RG] generateWorld: Cancellation requested during gen steps");
                        return;
                    }

                    try
                    {
                        Rand.Seed = Gen.HashCombineInt(seed, getSeedPart(tmpGenSteps, i));
                        if (!worldGenStepDefs.Contains(tmpGenSteps[i]))
                        {
                            continue;
                        }

                        Log.Message($"[RG] generateWorld: Executing gen step {i}: {tmpGenSteps[i].defName}");
                        tmpGenSteps[i].worldGenStep.GenerateFresh(page.seedString, planetLayer.Value);

                        if (tmpGenSteps[i].defName == "Tiles" && prevFaction != null)
                        {
                            Current.CreatingWorld.factionManager.ofPlayer = prevFaction;
                        }

                        previewStepsDone++;
                    }
                    catch (Exception ex)
                    {
                        if (ex is ThreadAbortException)
                        {
                            Rand.PopState();
                            Current.CreatingWorld = null;
                            generatingWorld = false;
                            Current.ProgramState = prevProgramState;
                            return;
                        }
                        else
                        {
                            Log.Error($"[RG] generateWorld: Error in WorldGenStep {tmpGenSteps[i].defName}: {ex}");
                        }
                    }
                }
            }

            if (shouldCancelGeneration)
            {
                Log.Message("[RG] generateWorld: Cancellation requested before finalization");
                return;
            }

            // Standardize tile data on the correct grid
            Log.Message("[RG] generateWorld: Standardizing tile data");
            Rand.Seed = seed;
            activeGrid.StandardizeTileData();

            Log.Message("[RG] generateWorld: Finalizing world");
            threadedWorld = Current.CreatingWorld;
            Current.Game.World = threadedWorld;

            if (Current.Game.World != null)
            {
                Current.Game.World.features = new WorldFeatures();
            }

            Log.Message("[RG] generateWorld: Unloading unused assets");
            MemoryUtility.UnloadUnusedUnityAssets();
            Log.Message("[RG] generateWorld: World generation completed successfully");
            // Ensure progress bar completes
            previewStepsDone = previewStepsTotal;
        }
        catch (Exception ex)
        {
            if (ex is ThreadAbortException)
            {
                var stateStack = Rand.stateStack;
                if (stateStack?.Any() == true)
                {
                    Rand.PopState();
                }

                generatingWorld = false;
                Current.ProgramState = prevProgramState;
                Current.CreatingWorld = null;
            }
        }
        finally
        {
            var stateStack = Rand.stateStack;
            if (stateStack?.Any() == true)
            {
                Rand.PopState();
            }

            generatingWorld = false;
            Current.CreatingWorld = null;
            Current.ProgramState = prevProgramState;
            Log.Message("[RG] generateWorld: Cleanup completed");
        }
    }

    private static int getSeedPart(List<WorldGenStepDef> genSteps, int index)
    {
        var seedPart = genSteps[index].worldGenStep.SeedPart;
        var num = 0;
        for (var i = 0; i < index; i++)
        {
            if (genSteps[i].worldGenStep.SeedPart == seedPart)
            {
                num++;
            }
        }

        return seedPart + num;
    }

    private static Texture2D getWorldCameraPreview(int width, int height)
    {
        // Ensure world and camera exist before attempting to render
        if (Find.World == null || Find.World.renderer == null || Find.WorldCamera == null || Find.World.UI == null)
        {
            Log.Warning("[RG] getWorldCameraPreview: World or camera unavailable, skipping preview render");
            return null;
        }

        Find.World.renderer.wantedMode = WorldRenderMode.Planet;
        Find.WorldCamera.gameObject.SetActive(true);
        Find.World.UI.Reset();
        AccessTools.Field(typeof(WorldCameraDriver), nameof(WorldCameraDriver.desiredAltitude))
            .SetValue(Find.WorldCameraDriver, 800);
        Find.WorldCameraDriver.altitude = 800;
        AccessTools.Method(typeof(WorldCameraDriver), nameof(WorldCameraDriver.ApplyPositionToGameObject))
            .Invoke(Find.WorldCameraDriver, []);

        var rect = new Rect(0, 0, width, height);
        var renderTexture = new RenderTexture(width, height, 24);
        var screenShot = new Texture2D(width, height, TextureFormat.RGBA32, false);

        Find.WorldCamera.targetTexture = renderTexture;
        Find.WorldCamera.Render();

        ExpandableWorldObjectsUtility.ExpandableWorldObjectsUpdate();

        // Draw world layers, but skip clouds to avoid null reference errors
        try
        {
            foreach (var layer in Find.World.renderer.AllDrawLayers)
            {
                // Skip clouds layer as it may not be properly initialized in preview
                if (layer is WorldDrawLayer_Clouds)
                {
                    continue;
                }

                try
                {
                    layer.Render();
                }
                catch (Exception ex)
                {
                    Log.Warning($"[RG] Error rendering layer {layer.GetType().Name}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[RG] Error during world layer rendering: {ex.Message}");
        }

        // Draw dynamic objects/features if available
        try
        {
            if (Find.World?.dynamicDrawManager != null)
            {
                Find.World.dynamicDrawManager.DrawDynamicWorldObjects();
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[RG] Error drawing dynamic world objects: {ex.Message}");
        }

        try
        {
            if (Find.World?.features != null)
            {
                Find.World.features.UpdateFeatures();
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[RG] Error updating world features: {ex.Message}");
        }

        NoiseDebugUI.RenderPlanetNoise();

        RenderTexture.active = renderTexture;
        screenShot.ReadPixels(rect, 0, 0);
        screenShot.Apply();
        Find.WorldCamera.targetTexture = null;
        RenderTexture.active = null;

        Find.WorldCamera.gameObject.SetActive(false);
        Find.World?.renderer.wantedMode = WorldRenderMode.None;
        return screenShot;
    }

    private static void updateCurPreset(Page_CreateWorldParams window)
    {
        if (tmpWorldGenerationPreset is null)
        {
            tmpWorldGenerationPreset = new WorldGenerationPreset();
            tmpWorldGenerationPreset.Init();
        }

        tmpWorldGenerationPreset.factionCounts = [];
        window.initialFactions.ForEach(faction => tmpWorldGenerationPreset.factionCounts.Add(faction.defName));
        tmpWorldGenerationPreset.temperature = window.temperature;
        tmpWorldGenerationPreset.seedString = window.seedString;
        tmpWorldGenerationPreset.planetCoverage = window.planetCoverage;
        tmpWorldGenerationPreset.rainfall = window.rainfall;
        tmpWorldGenerationPreset.population = window.population;

        if (ModsConfig.BiotechActive)
        {
            tmpWorldGenerationPreset.pollution = window.pollution;
        }

        // Ensure all biomes have entries in the dictionaries
        foreach (var biomeDef in DefDatabase<BiomeDef>.AllDefs)
        {
            tmpWorldGenerationPreset.biomeCommonalities.TryAdd(biomeDef.defName, 10);

            tmpWorldGenerationPreset.biomeScoreOffsets.TryAdd(biomeDef.defName, 0);
        }
    }

    private static void doSlider(float x, ref float num, float width2, string label, ref float field, string leftLabel)
    {
        num += 40f;
        var labelRect = new Rect(x, num, 200f, 30f);
        Widgets.Label(labelRect, label);
        var slider = new Rect(labelRect.xMax, num, width2, 30f);
        field = Widgets.HorizontalSlider(slider, field, 0, 2f, true,
            "PlanetRainfall_Normal".Translate(), leftLabel, "PlanetRainfall_High".Translate(), 0.1f);
    }

    private static void doBiomeSliders(BiomeDef biomeDef, float x, ref float num, string label)
    {
        // Defensive null check
        if (tmpWorldGenerationPreset is null || tmpWorldGenerationPreset.biomeCommonalities is null ||
            tmpWorldGenerationPreset.biomeScoreOffsets is null)
        {
            return;
        }

        var labelRect = new Rect(x, num - 10, 200f, 30f);
        Widgets.Label(labelRect, label);
        num += 10;

        // Ensure biome entries exist in dictionaries
        tmpWorldGenerationPreset.biomeCommonalities.TryAdd(biomeDef.defName, 10);

        tmpWorldGenerationPreset.biomeScoreOffsets.TryAdd(biomeDef.defName, 0);

        var biomeCommonalityLabel = new Rect(labelRect.x, num + 5, 70, 30);
        var value = tmpWorldGenerationPreset.biomeCommonalities[biomeDef.defName];
        if (value < 10f)
        {
            GUI.color = Color.red;
        }
        else if (value > 10f)
        {
            GUI.color = Color.green;
        }

        Widgets.Label(biomeCommonalityLabel, "RG.Commonality".Translate());
        var biomeCommonalitySlider = new Rect(biomeCommonalityLabel.xMax + 5, num, 340, 30f);
        tmpWorldGenerationPreset.biomeCommonalities[biomeDef.defName] =
            (int)Widgets.HorizontalSlider(biomeCommonalitySlider, value, 0, 20, false, $"{value * 10}%");
        GUI.color = Color.white;
        num += 30f;

        var biomeOffsetLabel = new Rect(labelRect.x, num + 5, 70, 30);
        var value2 = tmpWorldGenerationPreset.biomeScoreOffsets[biomeDef.defName];
        if (value2 < 0f)
        {
            GUI.color = Color.red;
        }
        else if (value2 > 0f)
        {
            GUI.color = Color.green;
        }

        Widgets.Label(biomeOffsetLabel, "RG.ScoreOffset".Translate());
        var scoreOffsetSlider = new Rect(biomeOffsetLabel.xMax + 5, biomeCommonalitySlider.yMax, 340, 30f);
        tmpWorldGenerationPreset.biomeScoreOffsets[biomeDef.defName] =
            (int)Widgets.HorizontalSlider(scoreOffsetSlider, value2, -99, 99, false, value2.ToString());
        GUI.color = Color.white;
        num += 50f;
    }
}