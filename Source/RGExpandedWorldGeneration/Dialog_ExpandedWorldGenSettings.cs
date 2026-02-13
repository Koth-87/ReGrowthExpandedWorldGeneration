using RimWorld;
using UnityEngine;
using Verse;

namespace RGExpandedWorldGeneration;

public class Dialog_ExpandedWorldGenSettings : Window
{
    private const float LabelWidth = 200f;
    private const float SliderWidth = 300f;
    private const float RowHeight = 40f;

    private readonly Page_CreateWorldParams parentWindow;
    private Vector2 scrollPosition;

    public Dialog_ExpandedWorldGenSettings(Page_CreateWorldParams parent)
    {
        parentWindow = parent;
        doCloseX = true;
        forcePause = true;
        absorbInputAroundWindow = true;
        closeOnClickedOutside = false;
    }

    public override Vector2 InitialSize => new(650f, 700f);

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Small;

        var titleRect = new Rect(0f, 0f, inRect.width, 35f);
        Text.Font = GameFont.Medium;
        Widgets.Label(titleRect, "RG.ExpandedWorldGenSettings".Translate());
        Text.Font = GameFont.Small;

        // Button bar at top
        var buttonY = 40f;
        var buttonWidth = 120f;
        var buttonSpacing = 10f;
        var currentX = 0f;

        // Save Preset button
        var savePresetRect = new Rect(currentX, buttonY, buttonWidth, 30f);
        if (Widgets.ButtonText(savePresetRect, "RG.SavePreset".Translate()))
        {
            var saveWindow = new Dialog_PresetList_Save(parentWindow);
            Find.WindowStack.Add(saveWindow);
        }

        currentX += buttonWidth + buttonSpacing;

        // Load Preset button
        var loadPresetRect = new Rect(currentX, buttonY, buttonWidth, 30f);
        if (Widgets.ButtonText(loadPresetRect, "RG.LoadPreset".Translate()))
        {
            var loadWindow = new Dialog_PresetList_Load(parentWindow);
            Find.WindowStack.Add(loadWindow);
        }

        // Content area with scrollview
        var contentRect = new Rect(0f, buttonY + 40f, inRect.width, inRect.height - 125f);
        var viewRect = new Rect(0f, 0f, contentRect.width - 16f, RowHeight * 10);

        Widgets.BeginScrollView(contentRect, ref scrollPosition, viewRect);

        var num = 0f;

        // River Density
        DoSlider(ref num, "RG.RiverDensity".Translate(),
            ref Page_CreateWorldParams_DoWindowContents.tmpWorldGenerationPreset.riverDensity,
            "None".Translate());

        // Mountain Density
        DoSlider(ref num, "RG.MountainDensity".Translate(),
            ref Page_CreateWorldParams_DoWindowContents.tmpWorldGenerationPreset.mountainDensity,
            "None".Translate());

        // Sea Level
        DoSlider(ref num, "RG.SeaLevel".Translate(),
            ref Page_CreateWorldParams_DoWindowContents.tmpWorldGenerationPreset.seaLevel,
            "None".Translate());

        // Ancient Road Density
        DoSlider(ref num, "RG.AncientRoadDensity".Translate(),
            ref Page_CreateWorldParams_DoWindowContents.tmpWorldGenerationPreset.ancientRoadDensity,
            "None".Translate());

        // Faction Road Density
        DoSlider(ref num, "RG.FactionRoadDensity".Translate(),
            ref Page_CreateWorldParams_DoWindowContents.tmpWorldGenerationPreset.factionRoadDensity,
            "None".Translate());

        // Axial Tilt (only if not using My Little Planet)
        if (!ModCompat.MyLittlePlanetActive)
        {
            num += RowHeight;
            var labelRect = new Rect(0, num, LabelWidth, 30f);
            var slider = new Rect(labelRect.xMax, num, SliderWidth, 30f);
            Widgets.Label(labelRect, "RG.AxialTilt".Translate());
            Page_CreateWorldParams_DoWindowContents.tmpWorldGenerationPreset.axialTilt =
                (AxialTilt)Mathf.RoundToInt(Widgets.HorizontalSlider(slider,
                    (float)Page_CreateWorldParams_DoWindowContents.tmpWorldGenerationPreset.axialTilt,
                    0f, AxialTiltUtility.EnumValuesCount - 1, true,
                    "PlanetRainfall_Normal".Translate(),
                    "PlanetRainfall_Low".Translate(),
                    "PlanetRainfall_High".Translate(), 1f));
        }

        Widgets.EndScrollView();

        // Close button at the bottom
        var closeButtonRect = new Rect((inRect.width / 2) - 75f, inRect.height - 35f, 150f, 35f);
        if (Widgets.ButtonText(closeButtonRect, "CloseButton".Translate()))
        {
            Close();
        }
    }

    private static void DoSlider(ref float yPos, string label, ref float field, string leftLabel)
    {
        yPos += RowHeight;
        var labelRect = new Rect(0, yPos, LabelWidth, 30f);
        Widgets.Label(labelRect, label);
        var slider = new Rect(labelRect.xMax, yPos, SliderWidth, 30f);
        field = Widgets.HorizontalSlider(slider, field, 0, 2f, true,
            "PlanetRainfall_Normal".Translate(), leftLabel, "PlanetRainfall_High".Translate(), 0.1f);
    }
}