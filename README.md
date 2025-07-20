# [COG] Corvus Surgery UI

An advanced, user-friendly interface for managing surgical procedures in RimWorld. Designed for compatibility and ease of use, this mod provides a powerful "Surgery Planner" to streamline your colony's medical operations.

It should be fully compatible with other mods and DLCs. Because it uses the game's own internal logic to find surgeries, it can correctly identify and display procedures from any other mod without needing specific patches.

![Surgery Planner UI](https://i.imgur.com/REPLACE_THIS_WITH_A_REAL_SCREENSHOT.png)  <!-- Replace with an actual screenshot -->

# CORVUS LIFE SCIENCES
## Advanced Surgical Interface System™ v2.7

**Revolutionizing Medical Precision Across the Rim**

At Corvus Life Sciences, we understand that successful surgery requires more than just steady hands—it demands superior information management. That's why our Advanced Surgical Interface System represents the pinnacle of medical UI technology, trusted by colony doctors from the inner worlds to the furthest rim settlements.

**Key Features:**
- **Streamlined Procedure Selection**: Our proprietary interface reduces surgical prep time by 73%*, allowing medical professionals to focus on what matters most—keeping colonists operational.
- **Enhanced Risk Assessment**: Clear, intuitive displays help minimize "unexpected outcomes" during complex procedures.
- **Optimized Workflow Management**: Because every second counts when you're extracting that kidney—we mean, performing life-saving surgery.

*"The Corvus interface helped me perform my best work yet. I barely noticed the screaming."* 
— Dr. █████, Outer Rim Medical Facility

**Corvus Life Sciences: Your Partner in Ethical Medical Excellence**

*Results may vary. Corvus Life Sciences is not responsible for organ harvesting operations, black market medical procedures, or any activities that may or may not involve the Corvus Operations Group's "Alternative Revenue Streams" division. Side effects may include increased surgical confidence, reduced patient mortality, and an inexplicable urge to install more medical beds.*

---
*Corvus Operations Group Subsidiary | Building Tomorrow's Rim, Today™*

## Features

### Quick Access
- **Keyboard Shortcut**: Press 'O' to instantly open the surgery planner for your selected pawn (or the first available colonist if none is selected)
- **Dedicated "Plan" Button**: Adds a new "Plan" button to the pawn's Health tab for quick access

### Comprehensive Interface
- **Complete Surgery List**: Intelligently generates a list of every possible surgery for the selected pawn
- **Quick Pawn Switching**: Easily switch between different colonists and animals without closing the planner
- **Surgery Queue Management**: 
  - Drag and drop to reorder surgeries
  - Suspend/activate individual surgeries or all at once
  - Clear visual feedback for suspended operations

### Advanced Filtering
- **Search**: Quickly find procedures by name or description
- **Category Filter**: Narrow down by type (Medical, Prosthetics, Implants, Removal, Amputation)
- **Mod Filter**: See only surgeries from a specific mod or DLC
- **Target Body Part Filter**: Display procedures for specific body parts
- **Availability Toggle**: Switch between all possible surgeries and currently available ones

### Smart Features
- **Surgery Presets**: Save and load common surgery combinations
- **Auto-Suspension**: Automatically suspends surgeries that can't be performed yet
- **Queue Non-Allowed**: Option to queue surgeries even when requirements aren't met
- **Color-Coded Implant Warnings**:
  - <span style="color:green">**Green:**</span> Direct upgrade
  - <span style="color:red">**Red:**</span> Downgrade
  - <span style="color:yellow">**Yellow:**</span> Sidegrade or unclear difference

### Detailed Information
- **Requirements Display**: Clear breakdown of needed research, skills, and materials
- **Mod Source**: See which mod added each surgery
- **Compatibility Info**: Visual indicators for surgery conflicts and replacements

### Localization Support
- **Full Translation Support**: All UI text is localizable
- **Clear Structure**: Organized translation files for easy community contributions

## Compatibility

This mod is designed for high compatibility and has been tested (in 1.5 as not all mods are available in 1.6 yet) with a list of over 70 popular mods without any issues, including:
- EPOE and EPOE Forked
- Vanilla Expanded Framework
- Glitter Tech
- Sparkling Worlds
- Questionable Ethics Enhanced

Generally, this mod should be placed after any other mods that add new surgeries or medical procedures. The `loadAfter` list in the `About/About.xml` file is configured to handle many common cases automatically.

## How It Works

This mod uses Harmony to patch the game and C# Reflection to call RimWorld's internal `GenerateSurgeryOption` method. This approach has two key benefits:
1. **High Compatibility**: By relying on the game's own logic, it can correctly process surgeries from any other mod without needing specific patches or integrations.
2. **Robustness**: The calls are wrapped in error handling. If a different mod has a bug in one of its surgery recipes, Corvus Surgery UI will gracefully skip that single recipe and log a warning instead of crashing the game.

## Installation

1. Subscribe to the mod on the Steam Workshop (link pending)
2. Alternatively, download the latest release from the GitHub repository
3. Unzip the contents into your `RimWorld/Mods` folder
4. Activate the mod in the in-game mod menu

## For Developers

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

### Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Submit a pull request

### Translation
Want to add a translation? It's easy:
1. Copy the `Languages/English/Keyed/CorvusSurgeryUI_Keys.xml` file
2. Create a new folder for your language under `Languages/`
3. Translate the text between the XML tags
4. Submit a pull request!

## License

This mod is released under the MIT License.
