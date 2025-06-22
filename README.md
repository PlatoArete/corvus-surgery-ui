# [COG] Corvus Surgery UI

An advanced, user-friendly interface for managing surgical procedures in RimWorld. Designed for compatibility and ease of use, this mod provides a powerful "Surgery Planner" to streamline your colony's medical operations.

It should be fully compatible with other mods and DLCs. Because it uses the game's own internal logic to find surgeries, it can correctly identify and display procedures from any other mod without needing specific patches.

![Surgery Planner UI](https://i.imgur.com/REPLACE_THIS_WITH_A_REAL_SCREENSHOT.png)  <!-- Replace with an actual screenshot -->

## Features

-   **Dedicated "Plan" Button:** Adds a new "Plan" button to the pawn's Health tab, launching a dedicated, comprehensive UI.
-   **Complete Surgery List:** The planner intelligently generates a list of every possible surgery for the selected pawn by safely using the game's own internal logic, ensuring maximum compatibility.
-   **Advanced Filtering:**
    -   **Search:** Quickly find a procedure by typing in its name.
    -   **Category Filter:** Narrow down the list by type (e.g., Medical, Prosthetics, Implants, Removal, Amputation).
    -   **Mod Filter:** See only surgeries added by a specific mod or DLC.
    -   **Target Body Part Filter:** Display only the procedures that can be performed on a specific body part.
-   **Availability Toggle:** A "Show All" / "Show Available Only" toggle lets you instantly switch between viewing all possible surgeries and only the ones you have the skills, medicine, and materials for right now.
-   **Color-Coded Implant Warnings:** When choosing a procedure that will replace an existing implant, the UI provides an immediate, color-coded warning:
    -   <span style="color:green">**Green:**</span> The new implant is a direct upgrade.
    -   <span style="color:red">**Red:**</span> The new implant is a downgrade.
    -   <span style="color:yellow">**Yellow:**</span> The change is a sidegrade or the difference is unclear.
-   **Detailed Information:** Each list item clearly displays the target body part, the mod it comes from, and a detailed breakdown of requirements.

## Compatibility

This mod is designed for high compatibility and has been tested with a list of over 70 popular mods without any issues, including:
- EPOE and EPOE Forked
- Vanilla Expanded Framework
- Glitter Tech
- Sparkling Worlds
- Questionable Ethics Enhanced

Generally, this mod should be placed after any other mods that add new surgeries or medical procedures. The `loadAfter` list in the `About/About.xml` file is configured to handle many common cases automatically.

## How It Works

This mod uses Harmony to patch the game and C# Reflection to call RimWorld's internal `GenerateSurgeryOption` method. This approach has two key benefits:
1.  **High Compatibility:** By relying on the game's own logic, it can correctly process surgeries from any other mod without needing specific patches or integrations.
2.  **Robustness:** The calls are wrapped in error handling. If a different mod has a bug in one of its surgery recipes, Corvus Surgery UI will gracefully skip that single recipe and log a warning instead of crashing the game.

## Installation

1.  Subscribe to the mod on the Steam Workshop (link pending).
2.  Alternatively, download the latest release from the GitHub repository.
3.  Unzip the contents into your `RimWorld/Mods` folder.
4.  Activate the mod in the in-game mod menu.

## For Developers

To build this mod from the source:
1.  Clone the repository.
2.  Ensure you have the .NET SDK installed.
3.  Run `dotnet build` from within the `Source` directory. The compiled `CorvusSurgeryUI.dll` will be placed in the `Assemblies` folder.

## Development

### Building the Mod

1. Open the `Source/CorvusSurgeryUI.csproj` file in Visual Studio or your preferred C# IDE
2. Build the project to generate the DLL in the `Assemblies/` folder
3. The mod will be ready to use in RimWorld

### Folder Structure

```
corvus-surgery-ui/
├── About/                  # Mod metadata
│   ├── About.xml          # Main mod information
│   └── PublishedFileId.txt # Steam Workshop ID
├── Assemblies/            # Compiled DLLs go here
├── Defs/                  # XML definitions
│   └── ThingDefs_Medical.xml
├── Languages/             # Translations
│   └── English/
│       └── Keyed/
│           └── CorvusSurgeryUI_Keys.xml
├── Patches/               # XML patches
│   └── Surgery_Patches.xml
├── Source/                # C# source code
│   ├── CorvusSurgeryUI.cs
│   └── CorvusSurgeryUI.csproj
├── Textures/              # Graphics and UI textures
│   └── UI/
├── Sounds/                # Audio files
├── LoadFolders.xml        # Version-specific loading
└── README.md             # This file
```

### Installation

1. Subscribe to the mod on Steam Workshop (when published)
2. Or manually copy the mod folder to your RimWorld/Mods directory
3. Enable the mod in RimWorld's mod manager

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Submit a pull request

## License

This mod is released under the MIT License.
