# GitHub Copilot Instructions for ReGrowth: Expanded World Generation (Continued)

## Mod Overview and Purpose

**ReGrowth: Expanded World Generation (Continued)** is a mod for RimWorld that allows players to customize world generation settings in unprecedented ways. It offers flexibility and control over various world parameters, catering to players' unique preferences whether it's creating a sea world, a Mars-like desert, or a mountainous landscape. Although officially discontinued, the mod is open-source, inviting contributions and expansions by the community.

## Key Features and Systems

- **Global Coverage:** Adjustable slider for world coverage percentage.
- **River, Mountain, and Road Density:** Customize the density of rivers, mountains, and roads.
- **Sea Level and Axial Tilt:** Alter the sea level and axial tilt to impact seasonal temperature shifts.
- **Biome Control:** Modify spawn values of vanilla and modded biomes using multipliers and offsets.
- **Presets:** Save and load custom world presets for future use or sharing (upcoming feature).
- **Planet Preview:** Visualize potential outcomes of world settings, albeit with performance considerations.
- **Compatibility:** Designed to work with various mods like Realistic Planets and My Little Planet.

## Coding Patterns and Conventions

- **C# Standards:** Follow standard C# conventions for naming and organizing code.
- **Modular Code Structure:** Utilize static classes for patches and extensions, maintaining modularity.
- **Accessibility:** Majority of the core functionality is encapsulated in public static classes and methods.
- **Consistent Naming:** Prefix classes with file or feature name for ease of navigation.

## XML Integration

- Extend XML files for RimWorld to integrate with mod settings via `Defs`.
- Define mod settings, such as sliders and buttons, in XML to reflect changes within the UI.
- Use the `ExposeData()` method for serialization of settings into RimWorld's save system.

## Harmony Patching

- **HarmonyInit:** Essential for initializing Harmony patches that modify or extend game behavior.
- **Targeted Patching:** Use Harmony to patch specific game methods affecting world generation post.
- **Compatibility Layers:** Implement specific patches for integration with other mods (e.g., Realistic Planets, RimWar).

## Suggestions for Copilot

- **Automation of Repetitive Tasks:** Use Copilot to scaffold new patch classes and methods by providing method signatures and basic logical structure.
- **XML Template Generation:** Leverage Copilot to generate XML templates for new mod-specific settings.
- **Harmony Integration Helper:** Develop context-aware suggestions for common patching patterns, aiding in swift patch development.
- **Error Resolution Assistance:** Provide context-based resolutions for potential runtime errors, particularly for XML parsing and Harmony conflicts.
- **Debugging Enhancements:** Suggest augmentations to debug log output, aiding in troubleshooting and performance optimization.

---

### Additional Considerations

- **Version Compatibility:** As RimWorld updates, ensure Copilot suggestions align with the latest game version (currently supports RimWorld 1.3).
- **Community Contributions:** Encourage utilizing Copilot for collaborative contributions, streamlining community-driven enhancements.
  
Before diving into contributions or expansions, refer to the latest changelog and mod documentation for updates or recent issues. For best practices, adhere to the existing code and documentation style to maintain consistency across the project.
