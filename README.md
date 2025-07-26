# [COG] Corvus Surgery UI

The ultimate surgery management interface for RimWorld! This mod revolutionizes medical operations with a powerful 3-tab system featuring comprehensive surgery lists, interactive body diagrams, and customizable presets. Designed for maximum compatibility and ease of use.

Works seamlessly with all mods and DLCs by leveraging RimWorld's internal surgery logic - no patches required!

![Surgery Planner UI](https://i.imgur.com/REPLACE_THIS_WITH_A_REAL_SCREENSHOT.png)  <!-- Replace with an actual screenshot -->

# CORVUS LIFE SCIENCES
## Advanced Surgical Interface System™ v3.0

**Revolutionizing Medical Precision Across the Rim**

At Corvus Life Sciences, we understand that successful surgery requires more than just steady hands—it demands superior information management. Our latest Advanced Surgical Interface System represents the pinnacle of medical UI technology, now featuring revolutionary visual planning capabilities trusted by colony doctors from the inner worlds to the furthest rim settlements.

**Revolutionary New Features:**
- **Interactive Body Diagrams**: Click directly on body parts for instant surgery access
- **3-Tab Interface**: Overview, Visual Planner, and Presets for maximum efficiency
- **Smart Force-Queuing**: Queue unavailable surgeries that auto-enable when ready
- **Enhanced Visual Feedback**: Color-coded health status and availability indicators

*"The new body diagram changed everything. I can plan complex reconstructive surgeries with a few clicks. My patients barely have time to scream before I'm done."* 
— Dr. █████, Outer Rim Trauma Center

**Corvus Life Sciences: Your Partner in Ethical Medical Excellence**

*Results may vary. Corvus Life Sciences is not responsible for organ harvesting operations, black market medical procedures, or any activities that may or may not involve the Corvus Operations Group's "Alternative Revenue Streams" division.*

---
*Corvus Operations Group Subsidiary | Building Tomorrow's Rim, Today™*

## Quick Start

### Instant Access
- **Keyboard Shortcut**: Press **'O'** to instantly open the surgery planner
- **Health Tab Button**: New "Plan" button in every pawn's Health tab
- **Auto-Selection**: Automatically selects your colonist or selected pawn

### The 3-Tab System

#### 1. **Overview Tab** - Comprehensive Surgery Management
- Complete list of all possible surgeries for the selected pawn
- Advanced filtering and search capabilities
- Detailed surgery information with requirements and warnings
- Full queue management with drag-and-drop reordering

#### 2. **Visual Planner Tab** - Interactive Body Diagram
- **SVG Body Diagram**: Click any body part for instant surgery options
- **Health Visualization**: Color-coded parts show health status
- **Floating Menus**: Context-sensitive surgery options appear on click
- **Humanoid Focus**: Optimized for human-like pawns
- **Direct Queuing**: Add surgeries without navigating menus

#### 3. **Presets Tab** - Saved Surgery Templates
- Save common surgery combinations
- Quick-load for repeated procedures
- Perfect for standardized implant installations

## Core Features

### Interactive Visual Planner
The crown jewel of the interface - click directly on body parts to see available surgeries:

- **Body Part Health Visualization**:
  - 🟢 **Green**: Healthy parts
  - 🟡 **Yellow**: Injured/scarred parts  
  - 🔴 **Red**: Severely damaged parts
  - 🔵 **Blue**: Prosthetic/bionic parts

- **Smart Context Menus**:
  - **Current Parts**: Shows installed prosthetics/bionics with removal options
  - **Available Surgeries**: Install/replacement options for the clicked part
  - **Natural Part Options**: Amputation choices for natural body parts

- **Internal Organ Support**: Click torso for heart, liver, kidney, lung surgeries

### Advanced Filtering System

#### Availability Filter
- **Show All**: Every possible surgery
- **Available Only**: Surgeries you can perform right now
- **Missing Item**: Surgeries blocked by missing materials
- **Missing Skill**: Surgeries requiring higher medical skill

#### Additional Filters
- **Category Filter**: Medical, Prosthetics, Implants, Removal, Amputation
- **Mod Filter**: See surgeries by specific mod or DLC
- **Target Filter**: Filter by specific body parts
- **Pawn Filter**: Quick switching between colonists and animals
- **Search**: Real-time text search of surgery names and descriptions

### Smart Queue Management

#### Drag & Drop Interface
- **Visual Reordering**: Drag surgeries to change priority
- **Live Feedback**: See drop positions as you drag
- **Priority Indicators**: Clear numbering shows surgery order

#### Bulk Operations
- **Suspend All**: Pause all queued surgeries
- **Activate All**: Resume all suspended surgeries
- **Individual Control**: Toggle suspension per surgery

#### Force Queue System
- **"Queue Non Allowed" Toggle**: Queue surgeries even when unavailable
- **Auto-Suspension**: Force-queued surgeries start suspended
- **Smart Activation**: Auto-enables when requirements are met
- **Background Monitoring**: Checks availability every game hour

### Enhanced Surgery Information

#### Detailed Requirements Display
- **Research Prerequisites**: Missing research projects
- **Skill Requirements**: Required medical skill levels with colonist availability
- **Material Needs**: Ingredient requirements with current stock levels
- **Availability Status**: Clear indicators for surgery readiness

#### Visual Feedback System
- **Status Indicators**: 
  - 🟢 Available and ready
  - 🟡 Available but can be force-queued
  - 🔴 Unavailable and blocked
- **Implant Warnings**: Color-coded replacement notifications
  - 🟢 **Green**: Upgrade (better efficiency/value)
  - 🔴 **Red**: Downgrade (worse efficiency/value)  
  - 🟡 **Yellow**: Sidegrade (similar/unclear)

#### Comprehensive Tooltips
- **Surgery Descriptions**: Full details from the original mod
- **Mod Attribution**: See which mod added each surgery
- **Requirement Breakdown**: Detailed list of what's needed
- **Status Explanations**: Clear text for availability states

### Surgery Presets System

#### Save & Load Templates
- **Custom Names**: Create descriptive preset names
- **Surgery Combinations**: Save entire surgery queues
- **Suspension States**: Remembers which surgeries were suspended
- **Body Part Mapping**: Preserves target body parts

#### Preset Management  
- **Easy Selection**: Dropdown menu with surgery counts
- **Quick Loading**: One-click preset application
- **Overwrite Protection**: Clear feedback when loading presets

### Automatic Surgery Management

#### Auto-Suspend System
- **Smart Monitoring**: Tracks force-queued surgeries in background
- **Requirement Checking**: Monitors research, materials, and skills
- **Auto-Activation**: Enables surgeries when requirements are met
- **Player Notifications**: Messages when surgeries become available

#### Intelligent Filtering
- **Context Awareness**: Different filters for different tabs
- **Humanoid Focus**: Visual Planner shows only human-like pawns
- **Animal Support**: Overview tab includes all creature types
- **Dynamic Updates**: Filters update as game state changes

## Compatibility & Technical Details

### Universal Mod Compatibility
This mod achieves exceptional compatibility through smart technical design:

- **Reflection-Based**: Uses RimWorld's internal `GenerateSurgeryOption` method
- **No Patches Required**: Works with any mod that adds surgeries
- **Error Resilience**: Gracefully handles broken surgery recipes
- **Vanilla Integration**: Leverages existing game systems

### Tested Mod Compatibility
Verified with 70+ popular mods including:
- **EPOE & EPOE Forked**: Full prosthetic and bionic support
- **Vanilla Expanded Framework**: All medical additions
- **Glitter Tech**: Advanced implants and procedures  
- **Sparkling Worlds**: Exotic medical technologies
- **Questionable Ethics Enhanced**: Controversial procedures
- **Royalty/Ideology/Biotech/Anomaly**: All DLC content

### Load Order
Generally place after mods that add surgeries. The mod's `loadAfter` configuration handles most cases automatically.

## Installation

### Steam Workshop (Recommended)
1. Subscribe to the mod on Steam Workshop
2. Activate in RimWorld's mod menu
3. Load after surgery-adding mods

### Manual Installation
1. Download from GitHub releases
2. Extract to `RimWorld/Mods/corvus-surgery-ui/`
3. Activate in mod menu

### Updating
- Steam subscribers get automatic updates
- Manual users should replace the entire mod folder

## Usage Tips

### Getting Started
1. **Press 'O'** or click the "Plan" button in any pawn's Health tab
2. **Explore the tabs**: Overview for lists, Visual Planner for body diagrams
3. **Try the filters**: Find exactly what you need quickly
4. **Use presets**: Save common surgery combinations

### Pro Tips
- **Right-click body parts** in Visual Planner for context menus
- **Enable "Queue Non Allowed"** to prepare for future surgeries  
- **Use drag-and-drop** to reorder surgery priorities
- **Save presets** for standardized implant procedures
- **Check tooltips** for detailed information about any surgery

### Troubleshooting
- **Missing surgeries?** Check if the required research is completed
- **Can't queue surgery?** Enable "Queue Non Allowed" to force-queue
- **Mod conflict?** Check the load order - place after surgery mods
- **Visual issues?** Restart RimWorld after adding/removing mods

## For Developers

### Project Structure
```
corvus-surgery-ui/
├── About/                    # Mod metadata
├── Assemblies/              # Compiled DLLs
├── Languages/               # Localization files  
│   └── English/Keyed/      # English translations
├── Source/                  # C# source code
│   ├── CorvusSurgeryUI.cs  # Main mod file
│   └── *.csproj            # Project configuration
└── README.md               # Documentation
```

### Building
1. Open `Source/CorvusSurgeryUI.csproj` in Visual Studio
2. Build to generate DLL in `Assemblies/`
3. Test in RimWorld

### Contributing
1. Fork the repository
2. Create feature branch: `git checkout -b feature-name`
3. Make changes with proper testing
4. Submit pull request with detailed description

### Localization
Add new languages by:
1. Copying `Languages/English/` folder
2. Renaming to your language code
3. Translating XML content (keep XML tags intact)
4. Testing in-game
5. Submitting pull request

### API Integration
Other mods can check for Corvus Surgery UI presence:
```csharp
bool hasCorvusSurgeryUI = ModsConfig.IsActive("corvus.surgery.ui");
```

## Performance & Optimization

### Efficient Design
- **Cached Surgery Lists**: Built once per pawn, reused across tabs
- **Lazy Loading**: Complex calculations only when displaying
- **Smart Updates**: Only rebuilds when necessary
- **Memory Management**: Proper cleanup of UI elements

### Scalability
- **Large Mod Lists**: Handles hundreds of surgery types efficiently  
- **Multiple Pawns**: Quick switching without performance loss
- **Colony Growth**: Scales with any number of colonists

## Version History

### v3.0 - Visual Revolution
- Added 3-tab interface system
- Interactive SVG body diagrams with clickable parts
- Enhanced filtering with availability states
- Force queue system with auto-suspension
- Improved visual feedback and tooltips

### v2.x - Enhanced Management  
- Surgery presets system
- Drag-and-drop queue reordering
- Bulk suspend/activate operations
- Enhanced mod compatibility

### v1.x - Foundation
- Initial surgery planner interface
- Basic filtering and search
- Keyboard shortcuts
- Core compatibility framework

## Support & Community

### Getting Help
- **GitHub Issues**: Bug reports and feature requests
- **Steam Comments**: Quick questions and feedback  
- **RimWorld Discord**: Community discussion

### Reporting Bugs
Please include:
- RimWorld version
- Mod list (especially medical mods)
- Steps to reproduce
- Log files if available

### Feature Requests
We love suggestions! Consider:
- How it improves workflow
- Compatibility implications  
- UI/UX impact
- Implementation complexity

## License

Released under MIT License - free for all uses including commercial and derivative works.

---

**Corvus Life Sciences - Building Tomorrow's Rim, Today™**

*Advanced medical interfaces for the discerning colony doctor*
