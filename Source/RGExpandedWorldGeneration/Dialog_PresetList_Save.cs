using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RGExpandedWorldGeneration;

public class Dialog_PresetList_Save : Dialog_PresetList
{
    public Dialog_PresetList_Save(Page_CreateWorldParams parent) : base(parent)
    {
        interactButLabel = "OverwriteButton".Translate();
    }

    protected override bool ShouldDoTypeInField => true;

    protected override void DoPresetInteraction(string name)
    {
        RGExpandedWorldGenerationSettingsMod.settings ??= new RGExpandedWorldGenerationSettings();

        RGExpandedWorldGenerationSettingsMod.settings.presets ??= new Dictionary<string, WorldGenerationPreset>();

        if (RGExpandedWorldGenerationSettings.curWorldGenerationPreset == null)
        {
            RGExpandedWorldGenerationSettings.curWorldGenerationPreset = new WorldGenerationPreset();
            RGExpandedWorldGenerationSettings.curWorldGenerationPreset.Init();
        }

        RGExpandedWorldGenerationSettingsMod.settings.presets[name] =
            RGExpandedWorldGenerationSettings.curWorldGenerationPreset.MakeCopy();
        Messages.Message("SavedAs".Translate(name), MessageTypeDefOf.SilentInput, false);
        RGExpandedWorldGenerationSettingsMod.settings.Write();
        Close();
    }
}