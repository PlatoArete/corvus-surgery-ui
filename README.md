# Corvus Surgery UI

A RimWorld mod that enhances the surgical interface for better user experience and improved medical procedures.

## Features

- Enhanced surgery user interface
- Improved medical procedure visualization
- Better surgical planning tools
- Customizable UI scaling and options

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
