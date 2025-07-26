using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using System.Reflection;
using System.IO;
using Newtonsoft.Json;

namespace CorvusSurgeryUI
{
    // New persistent preset system
    public class PersistentSurgeryPreset
    {
        public string Name { get; set; }
        public string SaveIdentifier { get; set; }
        public string SaveDisplayName { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<SurgeryPresetItem> Items { get; set; }
        public bool IsValid { get; set; } = true;
        public List<string> ValidationErrors { get; set; } = new List<string>();
        
        public PersistentSurgeryPreset()
        {
            Items = new List<SurgeryPresetItem>();
            CreatedAt = DateTime.Now;
        }
    }

    public class SurgeryPresetManager
    {
        private static SurgeryPresetManager instance;
        private static readonly string presetsFolder = Path.Combine(GenFilePaths.ConfigFolderPath, "CorvusSurgeryUI", "Presets");
        private static readonly string presetsFile = Path.Combine(presetsFolder, "SurgeryPresets.json");
        
        private Dictionary<string, PersistentSurgeryPreset> allPresets = new Dictionary<string, PersistentSurgeryPreset>();
        private Dictionary<string, PersistentSurgeryPreset> currentSavePresets = new Dictionary<string, PersistentSurgeryPreset>();
        private string currentSaveId = "";
        private bool presetsLoaded = false;

        public static SurgeryPresetManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new SurgeryPresetManager();
                }
                return instance;
            }
        }

        private SurgeryPresetManager()
        {
            EnsureDirectoryExists();
        }

        private void EnsureDirectoryExists()
        {
            try
            {
                if (!Directory.Exists(presetsFolder))
                {
                    Directory.CreateDirectory(presetsFolder);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Corvus Surgery UI: Failed to create presets directory: {ex}");
            }
        }

        public void LoadPresets()
        {
            if (presetsLoaded) return;
            
            try
            {
                if (File.Exists(presetsFile))
                {
                    var json = File.ReadAllText(presetsFile);
                    var presetList = JsonConvert.DeserializeObject<List<PersistentSurgeryPreset>>(json) ?? new List<PersistentSurgeryPreset>();
                    
                    allPresets.Clear();
                    foreach (var preset in presetList)
                    {
                        allPresets[GetPresetKey(preset.Name, preset.SaveIdentifier)] = preset;
                    }
                    
                    Log.Message($"Corvus Surgery UI: Loaded {allPresets.Count} presets from disk");
                }
                else
                {
                    allPresets.Clear();
                }
                
                presetsLoaded = true;
                UpdateCurrentSavePresets();
            }
            catch (Exception ex)
            {
                Log.Error($"Corvus Surgery UI: Failed to load presets: {ex}");
                allPresets.Clear();
                presetsLoaded = true;
            }
        }

        public void SavePresets()
        {
            try
            {
                var presetList = allPresets.Values.ToList();
                var json = JsonConvert.SerializeObject(presetList, Formatting.Indented);
                File.WriteAllText(presetsFile, json);
                Log.Message($"Corvus Surgery UI: Saved {presetList.Count} presets to disk");
            }
            catch (Exception ex)
            {
                Log.Error($"Corvus Surgery UI: Failed to save presets: {ex}");
            }
        }

        public string GetCurrentSaveIdentifier()
        {
            try
            {
                if (Current.Game?.World?.info != null)
                {
                    var worldInfo = Current.Game.World.info;
                    // Create a unique identifier using world name and seed
                    return $"{worldInfo.name}_{worldInfo.seedString}";
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"Corvus Surgery UI: Could not get world info for save identifier: {ex}");
            }
            
            // Fallback identifier
            return "unknown_save";
        }

        public string GetCurrentSaveDisplayName()
        {
            try
            {
                if (Current.Game?.World?.info != null)
                {
                    return Current.Game.World.info.name;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"Corvus Surgery UI: Could not get world name: {ex}");
            }
            
            return "Unknown Save";
        }

        private void UpdateCurrentSavePresets()
        {
            currentSaveId = GetCurrentSaveIdentifier();
            currentSavePresets.Clear();
            
            foreach (var kvp in allPresets)
            {
                if (kvp.Value.SaveIdentifier == currentSaveId)
                {
                    currentSavePresets[kvp.Value.Name] = kvp.Value;
                }
            }
            
            Log.Message($"Corvus Surgery UI: Loaded {currentSavePresets.Count} presets for current save");
        }

        public void RefreshCurrentSave()
        {
            var newSaveId = GetCurrentSaveIdentifier();
            if (newSaveId != currentSaveId)
            {
                UpdateCurrentSavePresets();
            }
        }

        public Dictionary<string, PersistentSurgeryPreset> GetCurrentSavePresets()
        {
            if (!presetsLoaded) LoadPresets();
            RefreshCurrentSave();
            return new Dictionary<string, PersistentSurgeryPreset>(currentSavePresets);
        }

        public Dictionary<string, List<PersistentSurgeryPreset>> GetPresetsBySave()
        {
            if (!presetsLoaded) LoadPresets();
            
            var result = new Dictionary<string, List<PersistentSurgeryPreset>>();
            foreach (var preset in allPresets.Values)
            {
                if (!result.ContainsKey(preset.SaveIdentifier))
                {
                    result[preset.SaveIdentifier] = new List<PersistentSurgeryPreset>();
                }
                result[preset.SaveIdentifier].Add(preset);
            }
            
            return result;
        }

        public void SavePreset(string name, List<SurgeryPresetItem> items, Action onConfirm = null)
        {
            if (!presetsLoaded) LoadPresets();
            RefreshCurrentSave();
            
            var newPreset = new PersistentSurgeryPreset
            {
                Name = name,
                SaveIdentifier = currentSaveId,
                SaveDisplayName = GetCurrentSaveDisplayName(),
                Items = new List<SurgeryPresetItem>(items),
                CreatedAt = DateTime.Now
            };
            
            // Check for global duplicates (same name + content)
            var existingDuplicate = FindGlobalDuplicate(newPreset);
            if (existingDuplicate != null)
            {
                // Show confirmation dialog
                Find.WindowStack.Add(new Dialog_OverwriteConfirmation(
                    name, 
                    existingDuplicate.SaveDisplayName,
                    () => {
                        // User confirmed overwrite
                        SavePresetInternal(newPreset);
                        onConfirm?.Invoke();
                    },
                    onConfirm // User cancelled
                ));
                return;
            }
            
            // No duplicate found, save directly
            SavePresetInternal(newPreset);
            onConfirm?.Invoke();
        }
        
        private void SavePresetInternal(PersistentSurgeryPreset preset)
        {
            ValidatePreset(preset);
            
            var key = GetPresetKey(preset.Name, preset.SaveIdentifier);
            allPresets[key] = preset;
            currentSavePresets[preset.Name] = preset;
            
            SavePresets();
            
            string message = preset.IsValid 
                ? $"Preset '{preset.Name}' saved globally (tagged for this save) with {preset.Items.Count} surgeries."
                : $"Preset '{preset.Name}' saved with warnings - some surgeries may be invalid.";
            Messages.Message(message, MessageTypeDefOf.PositiveEvent);
        }
        
        private PersistentSurgeryPreset FindGlobalDuplicate(PersistentSurgeryPreset newPreset)
        {
            foreach (var existingPreset in allPresets.Values)
            {
                if (existingPreset.Name == newPreset.Name && 
                    ArePresetItemsEqual(existingPreset.Items, newPreset.Items))
                {
                    return existingPreset;
                }
            }
            return null;
        }
        
        private bool ArePresetItemsEqual(List<SurgeryPresetItem> items1, List<SurgeryPresetItem> items2)
        {
            if (items1.Count != items2.Count) return false;
            
            // Sort both lists for comparison (by recipe name then body part)
            var sorted1 = items1.OrderBy(i => i.RecipeDefName).ThenBy(i => i.BodyPartLabel).ToList();
            var sorted2 = items2.OrderBy(i => i.RecipeDefName).ThenBy(i => i.BodyPartLabel).ToList();
            
            for (int i = 0; i < sorted1.Count; i++)
            {
                if (sorted1[i].RecipeDefName != sorted2[i].RecipeDefName ||
                    sorted1[i].BodyPartLabel != sorted2[i].BodyPartLabel ||
                    sorted1[i].IsSuspended != sorted2[i].IsSuspended)
                {
                    return false;
                }
            }
            
            return true;
        }

        public bool LoadPreset(string name, Pawn pawn, Thing thingForMedBills)
        {
            if (!presetsLoaded) LoadPresets();
            RefreshCurrentSave();
            
            if (!currentSavePresets.ContainsKey(name))
            {
                Messages.Message("Preset not found.", MessageTypeDefOf.RejectInput);
                return false;
            }
            
            var preset = currentSavePresets[name];
            ValidatePreset(preset); // Re-validate before loading
            
            // Clear current bills
            if (thingForMedBills is IBillGiver billGiver && billGiver.BillStack != null)
            {
                var billsToRemove = billGiver.BillStack.Bills.OfType<Bill_Medical>().ToList();
                foreach (var bill in billsToRemove)
                {
                    billGiver.BillStack.Delete(bill);
                }
            }
            
            // Load valid preset items (skip invalid ones)
            int loadedCount = 0;
            int skippedCount = 0;
            
            foreach (var item in preset.Items)
            {
                var recipe = DefDatabase<RecipeDef>.GetNamedSilentFail(item.RecipeDefName);
                if (recipe == null)
                {
                    skippedCount++;
                    continue; // Skip missing recipes
                }
                
                // Validate if this surgery is applicable to this specific pawn
                if (!IsRecipeValidForPawn(recipe, pawn, item))
                {
                    skippedCount++;
                    continue; // Skip invalid surgeries for this pawn
                }
                
                BodyPartRecord bodyPart = null;
                if (!item.BodyPartLabel.NullOrEmpty())
                {
                    bodyPart = FindBodyPart(item.BodyPartLabel, pawn);
                }
                
                // Create and add bill
                if (thingForMedBills is IBillGiver billGiver2 && billGiver2.BillStack != null)
                {
                    var bill = new Bill_Medical(recipe, null);
                    bill.Part = bodyPart;
                    bill.suspended = item.IsSuspended;
                    billGiver2.BillStack.AddBill(bill);
                    loadedCount++;
                }
            }
            
            string message = skippedCount == 0 
                ? $"Loaded preset '{name}': {loadedCount} surgeries."
                : $"Loaded preset '{name}': {loadedCount} surgeries ({skippedCount} skipped - incompatible with this pawn type).";
            
            var messageType = skippedCount == 0 ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.CautionInput;
            Messages.Message(message, messageType);
            
            return true;
        }

        public void DeletePreset(string name)
        {
            if (!presetsLoaded) LoadPresets();
            RefreshCurrentSave();
            
            if (currentSavePresets.ContainsKey(name))
            {
                var key = GetPresetKey(name, currentSaveId);
                allPresets.Remove(key);
                currentSavePresets.Remove(name);
                SavePresets();
                Messages.Message($"Preset '{name}' deleted.", MessageTypeDefOf.PositiveEvent);
            }
        }

        public void ImportPresetFromOtherSave(string presetName, string sourceSaveId)
        {
            if (!presetsLoaded) LoadPresets();
            
            var sourceKey = GetPresetKey(presetName, sourceSaveId);
            if (allPresets.ContainsKey(sourceKey))
            {
                var sourcePreset = allPresets[sourceKey];
                var newPreset = new PersistentSurgeryPreset
                {
                    Name = presetName,
                    SaveIdentifier = GetCurrentSaveIdentifier(),
                    SaveDisplayName = GetCurrentSaveDisplayName(),
                    Items = new List<SurgeryPresetItem>(sourcePreset.Items),
                    CreatedAt = DateTime.Now
                };
                
                ValidatePreset(newPreset);
                
                var newKey = GetPresetKey(presetName, newPreset.SaveIdentifier);
                allPresets[newKey] = newPreset;
                currentSavePresets[presetName] = newPreset;
                
                SavePresets();
                Messages.Message($"Imported preset '{presetName}' from {sourcePreset.SaveDisplayName}.", MessageTypeDefOf.PositiveEvent);
            }
        }

        public void ExportPreset(string presetName, string filePath)
        {
            if (!presetsLoaded) LoadPresets();
            RefreshCurrentSave();
            
            if (currentSavePresets.ContainsKey(presetName))
            {
                try
                {
                    var preset = currentSavePresets[presetName];
                    var exportData = new
                    {
                        PresetName = preset.Name,
                        CreatedAt = preset.CreatedAt,
                        OriginalSave = preset.SaveDisplayName,
                        Items = preset.Items
                    };
                    
                    var json = JsonConvert.SerializeObject(exportData, Formatting.Indented);
                    File.WriteAllText(filePath, json);
                    Messages.Message($"Preset '{presetName}' exported to {filePath}.", MessageTypeDefOf.PositiveEvent);
                }
                catch (Exception ex)
                {
                    Log.Error($"Corvus Surgery UI: Failed to export preset: {ex}");
                    Messages.Message("Failed to export preset.", MessageTypeDefOf.RejectInput);
                }
            }
        }

        public void ImportPreset(string filePath)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var importData = JsonConvert.DeserializeAnonymousType(json, new
                {
                    PresetName = "",
                    CreatedAt = DateTime.Now,
                    OriginalSave = "",
                    Items = new List<SurgeryPresetItem>()
                });
                
                var preset = new PersistentSurgeryPreset
                {
                    Name = importData.PresetName,
                    SaveIdentifier = GetCurrentSaveIdentifier(),
                    SaveDisplayName = GetCurrentSaveDisplayName(),
                    Items = importData.Items,
                    CreatedAt = DateTime.Now
                };
                
                ValidatePreset(preset);
                
                var key = GetPresetKey(preset.Name, preset.SaveIdentifier);
                allPresets[key] = preset;
                currentSavePresets[preset.Name] = preset;
                
                SavePresets();
                Messages.Message($"Imported preset '{preset.Name}' from {importData.OriginalSave}.", MessageTypeDefOf.PositiveEvent);
            }
            catch (Exception ex)
            {
                Log.Error($"Corvus Surgery UI: Failed to import preset: {ex}");
                Messages.Message("Failed to import preset. Please check file format.", MessageTypeDefOf.RejectInput);
            }
        }

        public void ValidatePreset(PersistentSurgeryPreset preset)
        {
            preset.IsValid = true;
            preset.ValidationErrors.Clear();
            
            foreach (var item in preset.Items)
            {
                var recipe = DefDatabase<RecipeDef>.GetNamedSilentFail(item.RecipeDefName);
                if (recipe == null)
                {
                    preset.IsValid = false;
                    preset.ValidationErrors.Add($"Missing recipe: {item.RecipeDefName}");
                }
            }
        }

        private BodyPartRecord FindBodyPart(string bodyPartLabel, Pawn pawn)
        {
            if (bodyPartLabel.NullOrEmpty()) return null;
            
            var parts = bodyPartLabel.Split('|');
            if (parts.Length != 2) return null;
            
            var defName = parts[0];
            if (!int.TryParse(parts[1], out int index)) return null;
            
            // Find the body part by def name and index
            foreach (var part in pawn.health.hediffSet.GetNotMissingParts())
            {
                if (part.def.defName == defName && part.Index == index)
                {
                    return part;
                }
            }
            
            return null;
        }

        private bool IsRecipeValidForPawn(RecipeDef recipe, Pawn pawn, SurgeryPresetItem item)
        {
            try
            {
                // Check if recipe is a surgery
                if (!recipe.IsSurgery) return false;
                
                // Check research requirements
                if ((recipe.researchPrerequisite != null && !recipe.researchPrerequisite.IsFinished) || 
                    (recipe.researchPrerequisites != null && recipe.researchPrerequisites.Any(r => !r.IsFinished)))
                {
                    return false;
                }
                
                // Check recipe availability report - this is the main check for pawn compatibility
                var report = recipe.Worker.AvailableReport(pawn);
                if (!report.Accepted && !report.Reason.NullOrEmpty()) 
                {
                    return false;
                }
                
                // For targeted surgeries, check if it's available on the specific body part
                if (recipe.targetsBodyPart)
                {
                    BodyPartRecord bodyPart = null;
                    if (!item.BodyPartLabel.NullOrEmpty())
                    {
                        bodyPart = FindBodyPart(item.BodyPartLabel, pawn);
                    }
                    
                    if (bodyPart != null)
                    {
                        // Check if surgery is available on this specific body part
                        if (!recipe.AvailableOnNow(pawn, bodyPart))
                        {
                            return false;
                        }
                    }
                    else if (!item.BodyPartLabel.NullOrEmpty())
                    {
                        // Body part was specified but not found on this pawn
                        return false;
                    }
                }
                
                // For non-targeted surgeries that add hediffs, check if pawn already has the hediff
                if (!recipe.targetsBodyPart && recipe.addsHediff != null)
                {
                    if (pawn.health.hediffSet.HasHediff(recipe.addsHediff))
                    {
                        return false; // Already has this hediff
                    }
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning($"Corvus Surgery UI: Error validating surgery '{recipe.defName}' for pawn '{pawn.LabelShort}': {ex.Message}");
                return false; // If there's an error, don't apply the surgery
            }
        }

        private string GetPresetKey(string name, string saveId)
        {
            return $"{saveId}:{name}";
        }
    }

    public class CorvusSurgeryUISettings : ModSettings
    {
        public int lastSelectedTabIndex = 0;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref lastSelectedTabIndex, "lastSelectedTabIndex", 0);
            base.ExposeData();
        }
    }

    public class CorvusSurgeryUIMod : Mod
    {
        public static CorvusSurgeryUISettings settings;
        private static Harmony harmony;

        public CorvusSurgeryUIMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<CorvusSurgeryUISettings>();
            harmony = new Harmony("corvus.surgery.ui");
            harmony.PatchAll();
            
            Log.Message("Corvus Surgery UI: Initialized");
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            settings.Write();
        }

        public override string SettingsCategory()
        {
            return "Corvus Surgery UI";
        }
    }

    // Patch the health tab to add our custom button
    [HarmonyPatch(typeof(RimWorld.HealthCardUtility), "DrawMedOperationsTab")]
    public static class HealthCardUtility_DrawMedOperationsTab_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Rect leftRect, Pawn pawn, Thing thingForMedBills, float curY)
        {
            try
            {
                if (pawn == null || thingForMedBills == null) return;
                
                // Only show for pawns that can have surgery
                bool canHaveSurgery = pawn.RaceProps.Humanlike || (pawn.RaceProps.Animal && pawn.health?.hediffSet != null);
                if (!canHaveSurgery) return;

                // Position the "Plan" button to the right of the vanilla "Add Bill" button
                float addBillButtonX = leftRect.x - 9f; // From decompiled code analysis
                float addBillButtonWidth = 150f;
                float buttonSpacing = 5f;
                float planButtonWidth = 60f; 
                float buttonHeight = 29f; // Match vanilla button height

                var planButtonRect = new Rect(addBillButtonX + addBillButtonWidth + buttonSpacing, curY, planButtonWidth, buttonHeight);

                if (Widgets.ButtonText(planButtonRect, "CorvusSurgeryUI.PlanButton".Translate()))
                {
                    var dialog = new Dialog_CorvusSurgeryPlanner(pawn, thingForMedBills);
                    Find.WindowStack.Add(dialog);
                }
                
                // Add tooltip
                TooltipHandler.TipRegion(planButtonRect, "CorvusSurgeryUI.PlanButtonTooltip".Translate());
            }
            catch (Exception ex)
            {
                Log.Error($"Corvus Surgery UI: Error in operations tab patch: {ex}");
            }
        }
    }

    // Patch to add keyboard shortcut for opening surgery planner
    [HarmonyPatch(typeof(MainButtonsRoot), "MainButtonsOnGUI")]
    public static class MainButtonsRoot_MainButtonsOnGUI_Patch
    {
        private static bool keyHandled = false;

        [HarmonyPostfix]
        public static void Postfix()
        {
            try
            {
                // Reset the key handled state at the start of each frame
                if (Event.current != null && Event.current.type == EventType.Layout)
                {
                    keyHandled = false;
                }

                // Only proceed if we haven't handled the key this frame
                if (keyHandled) return;

                // Check for O key press
                if (Event.current != null && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.O)
                {
                    Log.Message("Corvus Surgery UI: O key detected");

                    // Don't trigger if we're in text input
                    if (KeyBindingDefOf.Cancel.IsDownEvent)
                    {
                        Log.Message("Corvus Surgery UI: Cancel key down, ignoring");
                        return;
                    }

                    // Get the target pawn
                    var selectedPawn = Find.Selector.SelectedPawns.FirstOrDefault();
                    if (selectedPawn == null)
                    {
                        // Try single selected thing
                        if (Find.Selector.SingleSelectedThing is Pawn pawn)
                        {
                            selectedPawn = pawn;
                        }
                    }

                    // If no pawn selected, use first colonist
                    if (selectedPawn == null)
                    {
                        selectedPawn = Find.ColonistBar.GetColonistsInOrder().FirstOrDefault();
                    }

                    if (selectedPawn != null)
                    {
                        // Check if pawn can have surgery
                        bool canHaveSurgery = selectedPawn.RaceProps.Humanlike || (selectedPawn.RaceProps.Animal && selectedPawn.health?.hediffSet != null);
                        if (!canHaveSurgery)
                        {
                            Messages.Message("Selected pawn cannot have surgery.", MessageTypeDefOf.RejectInput);
                            return;
                        }

                        // Just open the planner - RimWorld will handle facility requirements
                        Log.Message($"Corvus Surgery UI: Opening planner for {selectedPawn.LabelShort}");
                        var dialog = new Dialog_CorvusSurgeryPlanner(selectedPawn, selectedPawn);
                        Find.WindowStack.Add(dialog);
                        Event.current.Use();
                        keyHandled = true;
                    }
                    else
                    {
                        Messages.Message("No colonist available for surgery planning.", MessageTypeDefOf.RejectInput);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Corvus Surgery UI: Error in keyboard shortcut patch: {ex}");
            }
        }
    }

    // Main dialog window for enhanced surgery planning
    public class Dialog_CorvusSurgeryPlanner : Window
    {
        private const float TAB_HEIGHT = 35f;
        private const float FILTER_HEIGHT = 70f;
        private const float DROPDOWN_WIDTH = 150f;
        private const float BUTTON_SPACING = 10f;
        private const float CONTENT_SPACING = 5f;
        private const float LABEL_HEIGHT = 20f;
        private const float DROPDOWN_HEIGHT = 25f;
        private const float SECTION_SPACING = 5f;

        private int selectedTabIndex = 0;
        private List<TabRecord> tabs;
        
        private Pawn pawn;
        private Vector2 scrollPosition;
        private string searchFilter = "";
        private SurgeryCategory selectedCategory = SurgeryCategory.All;
        private AvailabilityFilter availabilityFilter = AvailabilityFilter.ShowAvailableOnly;
        private string selectedModFilter = "All";
        private List<string> availableMods;
        private BodyPartRecord selectedTargetPart = null;
        private List<BodyPartRecord> availableTargets;
        private bool allowQueueingDisabled = false; // New option - off by default
        
        private List<SurgeryOptionCached> allPawnSurgeries; // Master list, built once
        private List<SurgeryOptionCached> filteredSurgeries; // The list that gets displayed
        private Thing thingForMedBills;

        // Drag and drop state
        private bool isDragging = false;
        private int draggedIndex = -1;
        private Vector2 dragOffset;
        private int dropTargetIndex = -1;
        private List<Bill_Medical> queuedBills = new List<Bill_Medical>();
        private Vector2 billScrollPosition = Vector2.zero;

        // Surgery presets
        private string selectedPreset = "(none)";
        
        // Presets tab scroll position
        private Vector2 presetsScrollPosition = Vector2.zero;
        
        // Presets tab filters (reset to defaults each session)
        private bool showColonists = true;
        private bool showPrisoners = true;
        private bool showSlaves = true;
        private bool showAnimals = true;
        private bool showGuests = false;
        
        // Presets tab selected pawn
        private Pawn selectedPresetsPawn = null;

        // Performance optimization - static caches
        private static Dictionary<RecipeDef, SurgeryCategory> recipeCategoryCache = new Dictionary<RecipeDef, SurgeryCategory>();
        private static Dictionary<RecipeDef, bool> nonTargetedCache = new Dictionary<RecipeDef, bool>();
        private static bool cacheInitialized = false;

        public Dialog_CorvusSurgeryPlanner(Pawn pawn, Thing thingForMedBills)
        {
            this.pawn = pawn;
            this.thingForMedBills = thingForMedBills;
            this.forcePause = true;
            this.doCloseX = true;
            this.absorbInputAroundWindow = true;
            
            // Initialize tabs
            tabs = new List<TabRecord>
            {
                new TabRecord("Overview", () => { selectedTabIndex = 0; }, () => selectedTabIndex == 0),
                new TabRecord("Presets", () => { selectedTabIndex = 1; }, () => selectedTabIndex == 1)
            };
            
            // Load last selected tab from settings
            selectedTabIndex = CorvusSurgeryUIMod.settings.lastSelectedTabIndex;
            
            // Initialize static caches if needed
            InitializeStaticCaches();
            
            // Load existing queued bills
            LoadQueuedBills();
            
            BuildFullSurgeryList();
            PopulateAvailableMods();
            PopulateAvailableTargets();
            ApplyFilters();
        }

        public override Vector2 InitialSize => new Vector2(1200f, 800f); // Increased from 750f to 800f

        protected override void SetInitialSizeAndPosition()
        {
            windowRect = new Rect((UI.screenWidth - InitialSize.x) / 2f, 
                                (UI.screenHeight - InitialSize.y) / 2f, 
                                InitialSize.x, InitialSize.y);
            windowRect.y = Mathf.Max(15f, windowRect.y); // Ensure window doesn't start too high
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Follow WeaponStats pattern exactly - adjust rect once for tabs
            inRect.yMin += 35;

            // Draw tabs
            TabDrawer.DrawTabs(inRect, tabs);

            if (selectedTabIndex == 0)
            {
                DrawOverviewTab(inRect);
            }
            else if (selectedTabIndex == 1)
            {
                DrawPresetsTab(inRect);
            }
            
            // Save selected tab
            if (CorvusSurgeryUIMod.settings.lastSelectedTabIndex != selectedTabIndex)
            {
                CorvusSurgeryUIMod.settings.lastSelectedTabIndex = selectedTabIndex;
                CorvusSurgeryUIMod.settings.Write();
            }
        }

        private void DrawOverviewTab(Rect rect)
        {
            float currentY = rect.y + 5f; // Start with a small padding

            // Draw filters section
            var filtersRect = new Rect(rect.x, currentY, rect.width, FILTER_HEIGHT);
            DrawFilters(filtersRect);
            currentY += FILTER_HEIGHT + SECTION_SPACING;

            // Info bar with pawn name and surgery count
            var surgeryCount = filteredSurgeries?.Count ?? 0;
            var infoRect = new Rect(rect.x, currentY, rect.width, DROPDOWN_HEIGHT);
            Widgets.Label(infoRect, $"{pawn.LabelShort} ({surgeryCount} surgeries found)");
            currentY += DROPDOWN_HEIGHT + SECTION_SPACING;

            // Search and Queue Non Allowed controls
            var searchLabelRect = new Rect(rect.x, currentY + 2f, 50f, DROPDOWN_HEIGHT);
            Widgets.Label(searchLabelRect, "Search:");
            
            var searchRect = new Rect(searchLabelRect.xMax + 5f, currentY, 200f, DROPDOWN_HEIGHT);
            string newFilter = Widgets.TextField(searchRect, searchFilter);
            if (newFilter != searchFilter)
            {
                searchFilter = newFilter;
                ApplyFilters();
            }

            var queueToggleRect = new Rect(searchRect.xMax + 20f, currentY, 200f, DROPDOWN_HEIGHT);
            bool newAllowQueueingDisabled = allowQueueingDisabled;
            Widgets.CheckboxLabeled(queueToggleRect, "Queue Non Allowed", ref newAllowQueueingDisabled);
            if (newAllowQueueingDisabled != allowQueueingDisabled)
            {
                allowQueueingDisabled = newAllowQueueingDisabled;
            }
            currentY += DROPDOWN_HEIGHT + SECTION_SPACING;

            // Split the remaining area for surgeries and queue
            var remainingRect = new Rect(rect.x, currentY, rect.width, 
                rect.height - (currentY - rect.y));
            
            var queueWidth = 500f;
            var surgeryListWidth = remainingRect.width - queueWidth - 10f;

            // Available Surgeries (left side)
            var surgeryListRect = new Rect(remainingRect.x, remainingRect.y, surgeryListWidth, remainingRect.height);
            DrawSurgeryList(surgeryListRect);

            // Surgery Queue (right side)
            var queuedBillsRect = new Rect(surgeryListRect.xMax + 10f, remainingRect.y, queueWidth, remainingRect.height);
            DrawQueuedBills(queuedBillsRect);
        }

        private void DrawPresetsTab(Rect rect)
        {
            float currentY = rect.y + 5f;
            
            // Header
            var eligiblePawns = GetAllEligiblePawns();
            var headerRect = new Rect(rect.x, currentY, rect.width, 30f);
            Text.Font = GameFont.Medium;
            Widgets.Label(headerRect, $"Surgery Presets - {eligiblePawns.Count} eligible pawns");
            Text.Font = GameFont.Small;
            currentY = headerRect.yMax + 5f;

            // Filter checkboxes
            var filterRect = new Rect(rect.x, currentY, rect.width, 30f);
            bool filtersChanged = DrawPresetFilters(filterRect);
            currentY = filterRect.yMax + 10f;
            
            // If filters changed, get updated pawn list
            if (filtersChanged)
            {
                eligiblePawns = GetAllEligiblePawns();
                
                // Update header count
                Text.Font = GameFont.Medium;
                Widgets.Label(headerRect, $"Surgery Presets - {eligiblePawns.Count} eligible pawns");
                Text.Font = GameFont.Small;
            }

            // Ensure we have a selected pawn (default to first)
            if (selectedPresetsPawn == null || !eligiblePawns.Contains(selectedPresetsPawn))
            {
                selectedPresetsPawn = eligiblePawns.FirstOrDefault();
            }

            // Split the remaining area for pawn grid and queued bills
            var remainingRect = new Rect(rect.x, currentY, rect.width, rect.height - (currentY - rect.y));
            
            var queueWidth = 500f;
            var pawnGridWidth = remainingRect.width - queueWidth - 10f;

            // Pawn Grid (left side)
            var pawnGridRect = new Rect(remainingRect.x, remainingRect.y, pawnGridWidth, remainingRect.height);
            DrawPawnGrid(pawnGridRect, eligiblePawns);

            // Surgery Queue (right side) - only if we have a selected pawn
            if (selectedPresetsPawn != null)
            {
                var queuedBillsRect = new Rect(pawnGridRect.xMax + 10f, remainingRect.y, queueWidth, remainingRect.height);
                DrawPresetsQueuedBills(queuedBillsRect, selectedPresetsPawn);
            }
        }

        private List<Pawn> GetAllEligiblePawns()
        {
            var allPawns = new List<Pawn>();
            
            // Get all maps
            var maps = Current.Game?.Maps;
            if (maps == null) return allPawns;
            
            foreach (var map in maps)
            {
                if (map?.mapPawns == null) continue;
                
                // Get all pawns that can have surgery
                var mapPawns = map.mapPawns.AllPawnsSpawned.Where(p => CanHaveSurgery(p));
                allPawns.AddRange(mapPawns);
            }
            
            // Apply filters and return sorted list
            return FilterAndSortPawns(allPawns);
        }
        
        private List<Pawn> FilterAndSortPawns(List<Pawn> pawns)
        {
            var filteredPawns = new List<Pawn>();
            
            foreach (var pawn in pawns)
            {
                var category = GetPawnCategory(pawn);
                
                // Apply filters
                bool shouldInclude = false;
                switch (category)
                {
                    case PawnCategory.Colonist:
                        shouldInclude = showColonists;
                        break;
                    case PawnCategory.Prisoner:
                        shouldInclude = showPrisoners;
                        break;
                    case PawnCategory.Slave:
                        shouldInclude = showSlaves;
                        break;
                    case PawnCategory.Animal:
                        shouldInclude = showAnimals;
                        break;
                    case PawnCategory.Guest:
                        shouldInclude = showGuests;
                        break;
                }
                
                if (shouldInclude)
                {
                    filteredPawns.Add(pawn);
                }
            }
            
            // Sort by category, then alphabetically within category
            return filteredPawns
                .OrderBy(p => (int)GetPawnCategory(p))
                .ThenBy(p => p.LabelShort)
                .ToList();
        }
        
        private PawnCategory GetPawnCategory(Pawn pawn)
        {
            // Colony-owned animals only (not wild animals or animals from other factions)
            if (pawn.RaceProps.Animal && pawn.Faction == Faction.OfPlayer)
            {
                return PawnCategory.Animal;
            }
            
            if (pawn.IsSlave)
            {
                return PawnCategory.Slave;
            }
            
            if (pawn.IsPrisoner)
            {
                return PawnCategory.Prisoner;
            }
            
            if (pawn.IsColonist)
            {
                return PawnCategory.Colonist;
            }
            
            // Check if it's a guest (quest pawn, trader, hospitality guest, etc.)
            if (IsGuest(pawn))
            {
                return PawnCategory.Guest;
            }
            
            // Default to guest for any other non-faction pawns (including wild animals)
            return PawnCategory.Guest;
        }
        
        private bool IsGuest(Pawn pawn)
        {
            // Quest pawns
            if (pawn.questTags?.Any() == true)
            {
                return true;
            }
            
            // Trade caravan members
            if (pawn.TraderKind != null || pawn.Faction?.def?.categoryTag == "TradingCompany")
            {
                return true;
            }
            
            // Hospitality mod guests (check for guest beds or hospitality status)
            if (pawn.guest != null && pawn.guest.GuestStatus == GuestStatus.Guest)
            {
                return true;
            }
            
            // Non-player faction pawns that aren't prisoners/slaves
            if (pawn.Faction != null && pawn.Faction != Faction.OfPlayer && !pawn.IsPrisoner && !pawn.IsSlave)
            {
                return true;
            }
            
            return false;
        }
        
        private bool DrawPresetFilters(Rect rect)
        {
            bool changed = false;
            float currentX = rect.x;
            const float checkboxWidth = 100f;
            const float spacing = 10f;
            
            // Store original values to detect changes
            bool origColonists = showColonists;
            bool origPrisoners = showPrisoners;
            bool origSlaves = showSlaves;
            bool origAnimals = showAnimals;
            bool origGuests = showGuests;
            
            // Draw checkboxes horizontally
            var colonistRect = new Rect(currentX, rect.y, checkboxWidth, rect.height);
            Widgets.CheckboxLabeled(colonistRect, "Colonists", ref showColonists);
            currentX += checkboxWidth + spacing;
            
            var prisonerRect = new Rect(currentX, rect.y, checkboxWidth, rect.height);
            Widgets.CheckboxLabeled(prisonerRect, "Prisoners", ref showPrisoners);
            currentX += checkboxWidth + spacing;
            
            var slaveRect = new Rect(currentX, rect.y, checkboxWidth, rect.height);
            Widgets.CheckboxLabeled(slaveRect, "Slaves", ref showSlaves);
            currentX += checkboxWidth + spacing;
            
            var animalRect = new Rect(currentX, rect.y, checkboxWidth, rect.height);
            Widgets.CheckboxLabeled(animalRect, "Animals", ref showAnimals);
            currentX += checkboxWidth + spacing;
            
            var guestRect = new Rect(currentX, rect.y, checkboxWidth, rect.height);
            Widgets.CheckboxLabeled(guestRect, "Guests", ref showGuests);
            
            // Check if any filters changed
            changed = origColonists != showColonists || 
                     origPrisoners != showPrisoners || 
                     origSlaves != showSlaves || 
                     origAnimals != showAnimals || 
                     origGuests != showGuests;
            
            return changed;
        }
        
        private void DrawPawnGrid(Rect rect, List<Pawn> eligiblePawns)
        {
            // Calculate grid layout - increased height for preset dropdown
            const float cardWidth = 120f;
            const float cardHeight = 180f; // Increased from 140f to 180f
            const float spacing = 10f;
            
            int columns = Mathf.FloorToInt((rect.width + spacing) / (cardWidth + spacing));
            int rows = Mathf.CeilToInt((float)eligiblePawns.Count / columns);
            
            var viewRect = new Rect(0f, 0f, rect.width - 16f, rows * (cardHeight + spacing));
            
            Widgets.BeginScrollView(rect, ref presetsScrollPosition, viewRect);
            
            // Draw pawn cards
            for (int i = 0; i < eligiblePawns.Count; i++)
            {
                var pawn = eligiblePawns[i];
                int col = i % columns;
                int row = i / columns;
                
                var cardRect = new Rect(
                    col * (cardWidth + spacing), 
                    row * (cardHeight + spacing), 
                    cardWidth, 
                    cardHeight
                );
                
                DrawPawnCard(cardRect, pawn, pawn == selectedPresetsPawn);
            }
            
            Widgets.EndScrollView();
        }
        
        private void DrawPresetsQueuedBills(Rect rect, Pawn pawn)
        {
            // Update queued bills for the selected pawn
            UpdateQueuedBills(pawn);
            
            // Use the same DrawQueuedBills method as the Overview tab
            DrawQueuedBills(rect);
        }
        
        private void UpdateQueuedBills(Pawn pawn)
        {
            // Temporarily set the thingForMedBills to the selected pawn
            var previousThing = thingForMedBills;
            thingForMedBills = pawn;
            
            // Load the queued bills for this pawn
            LoadQueuedBills();
            
            // Restore the previous thing (not strictly necessary for presets tab, but good practice)
            // thingForMedBills = previousThing; // Actually, let's keep it as the selected pawn for consistency
        }
        
        private void DrawPawnPresetDropdown(Rect rect, Pawn pawn)
        {
            // Get current save presets
            var presets = SurgeryPresetManager.Instance.GetCurrentSavePresets();
            
            if (presets.Count == 0)
            {
                GUI.color = Color.gray;
                if (Widgets.ButtonText(rect, "(no presets)"))
                {
                    // Do nothing - no presets available
                }
                GUI.color = Color.white;
                return;
            }
            
            // Simple dropdown button
            if (Widgets.ButtonText(rect, "Select..."))
            {
                ShowPawnPresetDropdown(rect, pawn);
            }
        }
        
        private void ShowPawnPresetDropdown(Rect buttonRect, Pawn pawn)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            
            // Add saved presets
            var presets = SurgeryPresetManager.Instance.GetCurrentSavePresets();
            foreach (var kvp in presets)
            {
                var name = kvp.Key; // Capture for closure
                var preset = kvp.Value;
                string displayName = preset.IsValid 
                    ? $"{name} ({preset.Items.Count} surgeries)"
                    : $"❌ {name} ({preset.Items.Count} surgeries - {preset.ValidationErrors.Count} issues)";
                
                options.Add(new FloatMenuOption(displayName, () => {
                    ApplyPresetToPawn(name, pawn);
                }));
            }
            
            if (presets.Count == 0)
            {
                options.Add(new FloatMenuOption("No presets saved", null) { Disabled = true });
            }
            
            Find.WindowStack.Add(new FloatMenu(options));
        }
        
        private void ApplyPresetToPawn(string presetName, Pawn pawn)
        {
            try
            {
                // Use the existing LoadPreset method but modify behavior to append
                var presets = SurgeryPresetManager.Instance.GetCurrentSavePresets();
                if (!presets.ContainsKey(presetName))
                {
                    Messages.Message("Preset not found.", MessageTypeDefOf.RejectInput);
                    return;
                }
                
                var preset = presets[presetName];
                SurgeryPresetManager.Instance.ValidatePreset(preset); // Re-validate before loading
                
                // Apply bills to pawn (append, don't clear existing)
                if (pawn is IBillGiver billGiver && billGiver.BillStack != null)
                {
                    int loadedCount = 0;
                    int skippedCount = 0;
                    
                    foreach (var item in preset.Items)
                    {
                        var recipe = DefDatabase<RecipeDef>.GetNamedSilentFail(item.RecipeDefName);
                        if (recipe == null)
                        {
                            skippedCount++;
                            continue; // Skip missing recipes
                        }
                        
                        // Validate if this surgery is applicable to this specific pawn
                        if (!IsSurgeryValidForPawn(recipe, pawn, item))
                        {
                            skippedCount++;
                            continue; // Skip invalid surgeries for this pawn
                        }
                        
                        BodyPartRecord bodyPart = null;
                        if (!item.BodyPartLabel.NullOrEmpty())
                        {
                            bodyPart = FindBodyPart(item.BodyPartLabel, pawn);
                        }
                        
                        // Create and add bill (append to existing bills)
                        var bill = new Bill_Medical(recipe, null);
                        bill.Part = bodyPart;
                        bill.suspended = item.IsSuspended;
                        billGiver.BillStack.AddBill(bill);
                        loadedCount++;
                    }
                    
                    string message = skippedCount == 0 
                        ? $"Applied preset '{presetName}' to {pawn.LabelShort}: {loadedCount} surgeries added."
                        : $"Applied preset '{presetName}' to {pawn.LabelShort}: {loadedCount} surgeries added ({skippedCount} skipped - incompatible with this pawn type).";
                    
                    var messageType = skippedCount == 0 ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.CautionInput;
                    Messages.Message(message, messageType);
                    
                    // Refresh the bills display if this pawn is currently selected
                    if (selectedPresetsPawn == pawn)
                    {
                        UpdateQueuedBills(pawn);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Corvus Surgery UI: Error applying preset to pawn: {ex}");
                Messages.Message("Error applying preset to pawn.", MessageTypeDefOf.RejectInput);
            }
        }
        
        private bool IsSurgeryValidForPawn(RecipeDef recipe, Pawn pawn, SurgeryPresetItem item)
        {
            try
            {
                // Check if recipe is a surgery
                if (!recipe.IsSurgery) return false;
                
                // Check research requirements
                if ((recipe.researchPrerequisite != null && !recipe.researchPrerequisite.IsFinished) || 
                    (recipe.researchPrerequisites != null && recipe.researchPrerequisites.Any(r => !r.IsFinished)))
                {
                    return false;
                }
                
                // Check recipe availability report - this is the main check for pawn compatibility
                var report = recipe.Worker.AvailableReport(pawn);
                if (!report.Accepted && !report.Reason.NullOrEmpty()) 
                {
                    return false;
                }
                
                // For targeted surgeries, check if it's available on the specific body part
                if (recipe.targetsBodyPart)
                {
                    BodyPartRecord bodyPart = null;
                    if (!item.BodyPartLabel.NullOrEmpty())
                    {
                        bodyPart = FindBodyPart(item.BodyPartLabel, pawn);
                    }
                    
                    if (bodyPart != null)
                    {
                        // Check if surgery is available on this specific body part
                        if (!recipe.AvailableOnNow(pawn, bodyPart))
                        {
                            return false;
                        }
                    }
                    else if (!item.BodyPartLabel.NullOrEmpty())
                    {
                        // Body part was specified but not found on this pawn
                        return false;
                    }
                }
                
                // For non-targeted surgeries that add hediffs, check if pawn already has the hediff
                if (!recipe.targetsBodyPart && recipe.addsHediff != null)
                {
                    if (pawn.health.hediffSet.HasHediff(recipe.addsHediff))
                    {
                        return false; // Already has this hediff
                    }
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning($"Corvus Surgery UI: Error validating surgery '{recipe.defName}' for pawn '{pawn.LabelShort}': {ex.Message}");
                return false; // If there's an error, don't apply the surgery
            }
        }
        
        private bool CanHaveSurgery(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return false;
            
            // Same logic as in the original mod
            return pawn.RaceProps.Humanlike || (pawn.RaceProps.Animal && pawn.health.hediffSet != null);
        }
        
        private void DrawPawnCard(Rect cardRect, Pawn pawn, bool isSelected = false)
        {
            // Card background - highlight if selected
            Color backgroundColor = isSelected ? Color.blue * 0.3f : Color.black * 0.2f;
            Widgets.DrawBoxSolid(cardRect, backgroundColor);
            
            // Border - thicker if selected
            int borderWidth = isSelected ? 2 : 1;
            Color borderColor = isSelected ? Color.cyan : Color.white;
            GUI.color = borderColor;
            Widgets.DrawBox(cardRect, borderWidth);
            GUI.color = Color.white;
            
            // Portrait area
            var portraitRect = new Rect(cardRect.x + 5f, cardRect.y + 5f, cardRect.width - 10f, 80f);
            
            // Draw actual pawn portrait using RimWorld's portrait system
            try
            {
                // Get or create portrait from RimWorld's cache
                var portrait = PortraitsCache.Get(pawn, new Vector2(portraitRect.width, portraitRect.height), Rot4.South, default(Vector3), 1f);
                
                if (portrait != null)
                {
                    GUI.DrawTexture(portraitRect, portrait);
                }
                else
                {
                    // Fallback to colored rectangle if portrait fails
                    DrawFallbackPortrait(portraitRect, pawn);
                }
            }
            catch (Exception ex)
            {
                // If portrait rendering fails, use fallback
                Log.Warning($"Corvus Surgery UI: Failed to render portrait for {pawn.LabelShort}: {ex.Message}");
                DrawFallbackPortrait(portraitRect, pawn);
            }
            
            // Pawn name
            var nameRect = new Rect(cardRect.x + 2f, portraitRect.yMax + 5f, cardRect.width - 4f, 20f);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(nameRect, pawn.LabelShort.Truncate(nameRect.width));
            
            // Pawn status/type
            var statusRect = new Rect(cardRect.x + 2f, nameRect.yMax, cardRect.width - 4f, 15f);
            string status = "";
            switch (GetPawnCategory(pawn))
            {
                case PawnCategory.Colonist:
                    status = "Colonist";
                    break;
                case PawnCategory.Prisoner:
                    status = "Prisoner";
                    break;
                case PawnCategory.Slave:
                    status = "Slave";
                    break;
                case PawnCategory.Animal:
                    status = "Animal";
                    break;
                case PawnCategory.Guest:
                    status = "Guest";
                    break;
                default:
                    status = "Unknown";
                    break;
            }
            
            GUI.color = Color.gray;
            Widgets.Label(statusRect, status);
            GUI.color = Color.white;
            
            // Location info
            var locationRect = new Rect(cardRect.x + 2f, statusRect.yMax, cardRect.width - 4f, 15f);
            string location = pawn.Map?.Parent?.Label ?? "Unknown";
            Widgets.Label(locationRect, location.Truncate(locationRect.width));
            
            // Preset dropdown
            var presetLabelRect = new Rect(cardRect.x + 2f, locationRect.yMax + 2f, cardRect.width - 4f, 12f);
            GUI.color = Color.gray;
            Text.Font = GameFont.Tiny;
            Widgets.Label(presetLabelRect, "Preset:");
            GUI.color = Color.white;
            
            var presetDropdownRect = new Rect(cardRect.x + 2f, presetLabelRect.yMax, cardRect.width - 4f, 20f);
            DrawPawnPresetDropdown(presetDropdownRect, pawn);
            
            // Reset text settings to defaults
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            
            // Click handling - select pawn for presets tab (but not over the dropdown)
            var clickableRect = new Rect(cardRect.x, cardRect.y, cardRect.width, presetLabelRect.y - cardRect.y);
            if (Widgets.ButtonInvisible(clickableRect))
            {
                selectedPresetsPawn = pawn;
            }
            
            // Hover effect
            if (Mouse.IsOver(cardRect))
            {
                Widgets.DrawHighlight(cardRect);
                TooltipHandler.TipRegion(cardRect, $"{pawn.LabelShort}\n{status}\nLocation: {location}");
            }
        }
        
        private void DrawFallbackPortrait(Rect portraitRect, Pawn pawn)
        {
            // Fallback portrait with better visuals
            Color pawnColor = Color.gray;
            if (pawn.IsColonist) pawnColor = Color.green;
            else if (pawn.IsPrisoner) pawnColor = Color.red;
            else if (pawn.RaceProps.Animal) pawnColor = Color.yellow;
            
            // Draw gradient background
            Widgets.DrawBoxSolid(portraitRect, pawnColor * 0.3f);
            
            // Add border
            Widgets.DrawBox(portraitRect, 1);
            
            // Add a simple icon/initial in the center
            var iconRect = new Rect(portraitRect.center.x - 15f, portraitRect.center.y - 15f, 30f, 30f);
            
            // Store current text settings
            var originalFont = Text.Font;
            var originalAnchor = Text.Anchor;
            
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            
            string icon;
            if (pawn.RaceProps.Animal)
            {
                icon = "A"; // Simple "A" for Animal
            }
            else
            {
                icon = !string.IsNullOrEmpty(pawn.LabelShort) ? pawn.LabelShort.Substring(0, 1).ToUpper() : "?";
            }
            
            // Draw with high contrast
            GUI.color = Color.black;
            var shadowRect = new Rect(iconRect.x + 1f, iconRect.y + 1f, iconRect.width, iconRect.height);
            Widgets.Label(shadowRect, icon);
            
            GUI.color = Color.white;
            Widgets.Label(iconRect, icon);
            
            // Restore text settings
            Text.Font = originalFont;
            Text.Anchor = originalAnchor;
            GUI.color = Color.white;
        }

        private void DrawFilters(Rect rect)
        {
            float currentY = rect.y;

            // First row: Labels for dropdowns
            var labelY = currentY;
            var categoryLabelRect = new Rect(rect.x, labelY, DROPDOWN_WIDTH, LABEL_HEIGHT);
            Widgets.Label(categoryLabelRect, "Category:");

            var modLabelRect = new Rect(categoryLabelRect.xMax + BUTTON_SPACING, labelY, DROPDOWN_WIDTH, LABEL_HEIGHT);
            Widgets.Label(modLabelRect, "Mod:");

            var targetLabelRect = new Rect(modLabelRect.xMax + BUTTON_SPACING, labelY, DROPDOWN_WIDTH, LABEL_HEIGHT);
            Widgets.Label(targetLabelRect, "Target:");

            var pawnLabelRect = new Rect(targetLabelRect.xMax + BUTTON_SPACING, labelY, DROPDOWN_WIDTH, LABEL_HEIGHT);
            Widgets.Label(pawnLabelRect, "Pawn:");

            // Second row: Dropdowns (reduced spacing)
            currentY += LABEL_HEIGHT + 2f; // Minimal spacing between label and dropdown
            var dropdownY = currentY;

            // Category dropdown
            var categoryRect = new Rect(rect.x, dropdownY, DROPDOWN_WIDTH, DROPDOWN_HEIGHT);
            string buttonText = selectedCategory == SurgeryCategory.All ? 
                "CorvusSurgeryUI.All".Translate() : 
                ("CorvusSurgeryUI.Category." + selectedCategory.ToString()).Translate();
            
            if (Widgets.ButtonText(categoryRect, buttonText))
            {
                DrawCategoryDropdown(categoryRect);
            }
            if (Mouse.IsOver(categoryRect))
            {
                TooltipHandler.TipRegion(categoryRect, buttonText);
            }

            // Mod dropdown
            var modRect = new Rect(categoryRect.xMax + BUTTON_SPACING, dropdownY, DROPDOWN_WIDTH, DROPDOWN_HEIGHT);
            string modButtonText = selectedModFilter == "All" ? "CorvusSurgeryUI.All".Translate().ToString() : selectedModFilter;
            if (Widgets.ButtonText(modRect, modButtonText))
            {
                DrawModDropdown(modRect);
            }
            if (Mouse.IsOver(modRect))
            {
                TooltipHandler.TipRegion(modRect, modButtonText);
            }

            // Target dropdown
            var targetRect = new Rect(modRect.xMax + BUTTON_SPACING, dropdownY, DROPDOWN_WIDTH, DROPDOWN_HEIGHT);
            string targetButtonLabel = selectedTargetPart?.LabelCap ?? "All";
            if (Widgets.ButtonText(targetRect, targetButtonLabel))
            {
                DrawTargetDropdown(targetRect);
            }
            if (Mouse.IsOver(targetRect))
            {
                TooltipHandler.TipRegion(targetRect, targetButtonLabel);
            }

            // Pawn dropdown
            var pawnRect = new Rect(targetRect.xMax + BUTTON_SPACING, dropdownY, DROPDOWN_WIDTH, DROPDOWN_HEIGHT);
            if (Widgets.ButtonText(pawnRect, pawn.LabelShort))
            {
                DrawPawnDropdown(pawnRect);
            }
            if (Mouse.IsOver(pawnRect))
            {
                TooltipHandler.TipRegion(pawnRect, pawn.LabelShort);
            }

            // Quick filter buttons on the right
            float rightEdge = rect.xMax;
            var implantsRect = new Rect(rightEdge - 80f, dropdownY, 80f, DROPDOWN_HEIGHT);
            if (Widgets.ButtonText(implantsRect, "Implants"))
            {
                selectedCategory = SurgeryCategory.Implants;
                ApplyFilters();
            }

            var availableRect = new Rect(implantsRect.x - 110f, dropdownY, 100f, DROPDOWN_HEIGHT);
            if (Widgets.ButtonText(availableRect, "Available Only"))
            {
                availabilityFilter = AvailabilityFilter.ShowAvailableOnly;
                ApplyFilters();
            }

            var clearRect = new Rect(availableRect.x - 90f, dropdownY, 80f, DROPDOWN_HEIGHT);
            if (Widgets.ButtonText(clearRect, "Clear All"))
            {
                ClearFilters();
            }
        }

        private void DrawCategoryDropdown(Rect rect)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (SurgeryCategory category in Enum.GetValues(typeof(SurgeryCategory)))
            {
                var count = allPawnSurgeries?.Count(s => category == SurgeryCategory.All || s.Category == category) ?? 0;
                string categoryKey = category == SurgeryCategory.All ? "CorvusSurgeryUI.All" : "CorvusSurgeryUI.Category." + category.ToString();
                options.Add(new FloatMenuOption($"{categoryKey.Translate()} ({count})", () => {
                    selectedCategory = category;
                    ApplyFilters();
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void DrawModDropdown(Rect rect)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (string modName in availableMods)
            {
                int count;
                if (modName == "All")
                {
                    count = allPawnSurgeries.Count;
                }
                else
                {
                    count = allPawnSurgeries.Count(s => (s.Recipe?.modContentPack?.Name ?? "Core") == modName);
                }
                
                string displayName = modName == "All" ? "CorvusSurgeryUI.All".Translate().ToString() : modName;
                options.Add(new FloatMenuOption($"{displayName} ({count})", () => {
                    selectedModFilter = modName;
                    ApplyFilters();
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void DrawTargetDropdown(Rect rect)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            options.Add(new FloatMenuOption("All", () => {
                selectedTargetPart = null;
                ApplyFilters();
            }));
            
            foreach (var target in availableTargets)
            {
                options.Add(new FloatMenuOption(target.LabelCap, () => {
                    selectedTargetPart = target;
                    ApplyFilters();
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void DrawPawnDropdown(Rect rect)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            
            var allPawns = Find.Maps.SelectMany(m => m.mapPawns.AllPawns)
                .Where(p => p.Faction == Faction.OfPlayer && 
                    (p.RaceProps.Humanlike || (p.RaceProps.Animal && p.health?.hediffSet != null)))
                .OrderByDescending(p => p.RaceProps.Humanlike)
                .ThenBy(p => p.LabelShort);

            foreach (var possiblePawn in allPawns)
            {
                string label = possiblePawn.LabelShort;
                if (possiblePawn.RaceProps.Animal)
                {
                    label += $" ({possiblePawn.def.label})";
                }

                options.Add(new FloatMenuOption(label, () => {
                    if (possiblePawn != pawn)
                    {
                        pawn = possiblePawn;
                        thingForMedBills = possiblePawn;
                        BuildFullSurgeryList();
                        PopulateAvailableTargets();
                        LoadQueuedBills();
                        ApplyFilters();
                    }
                }));
            }

            if (!options.Any())
            {
                options.Add(new FloatMenuOption("No other valid pawns", null) { Disabled = true });
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ClearFilters()
        {
            searchFilter = "";
            selectedCategory = SurgeryCategory.All;
            selectedModFilter = "All";
            availabilityFilter = AvailabilityFilter.ShowAvailableOnly;
            selectedTargetPart = null;
            ApplyFilters();
        }
        
        private void PopulateAvailableMods()
        {
            if (allPawnSurgeries == null)
            {
                availableMods = new List<string> { "All" };
                return;
            }
            availableMods = allPawnSurgeries
                .Select(s => s.Recipe?.modContentPack?.Name ?? "Core")
                .Distinct()
                .OrderBy(m => m)
                .ToList();
            availableMods.Insert(0, "All");
        }

        private void PopulateAvailableTargets()
        {
            if (allPawnSurgeries == null)
            {
                availableTargets = new List<BodyPartRecord>();
                return;
            }
            availableTargets = allPawnSurgeries
                .Where(s => s.BodyPart != null)
                .Select(s => s.BodyPart)
                .Distinct()
                .OrderBy(bp => bp.LabelCap.ToString())
                .ToList();
        }

        private void DrawSurgeryList(Rect rect)
        {
            // Follow WeaponStats pattern for scroll views
            GUI.BeginGroup(rect);
            
            var rowHeight = 70f;
            var rowSpacing = 5f;
            var tableHeight = (rowHeight + rowSpacing) * filteredSurgeries.Count;
            
            // Inner rect for scroll content
            Rect inRect = new Rect(0, 0, rect.width - 20f, tableHeight + 100);
            
            // Scroll rect covers the entire group area
            Rect scrollRect = new Rect(0, 0, rect.width, rect.height);
            Widgets.BeginScrollView(scrollRect, ref scrollPosition, inRect);

            float y = 0f;
            for (int i = 0; i < filteredSurgeries.Count; i++)
            {
                var surgery = filteredSurgeries[i];
                var surgeryRect = new Rect(5f, y, inRect.width - 10f, rowHeight);
                
                DrawSurgeryOption(surgeryRect, surgery, i);
                y += rowHeight + rowSpacing;
            }

            Widgets.EndScrollView();
            GUI.EndGroup();
        }

        private void DrawSurgeryOption(Rect rect, SurgeryOptionCached surgery, int index)
        {
            // Lazy load expensive properties only when displaying
            if (string.IsNullOrEmpty(surgery.Requirements))
            {
                surgery.Requirements = GetRequirements(surgery.Recipe);
                var (warningText, warningColor) = GetImplantWarning(surgery.Recipe, surgery.BodyPart);
                surgery.ImplantWarning = warningText;
                surgery.ImplantWarningColor = warningColor;
                surgery.Tooltip = GetDetailedTooltip(surgery.Recipe, surgery.BodyPart);
            }
            
            // Background with better visual feedback
            Color bgColor = Color.black * 0.1f;
            Widgets.DrawBoxSolid(rect, bgColor);
            
            // Outline for better definition
            Widgets.DrawBox(rect, 1);

            // Left column - Surgery info
            var leftColumnWidth = rect.width * 0.45f;
            
            // Surgery name
            Rect nameRect = new Rect(rect.x + 15f, rect.y + 5f, leftColumnWidth - 20f, 20f);
            Widgets.Label(nameRect, surgery.Label);

            // Body part info
            if (surgery.BodyPart != null)
            {
                Rect bodyPartRect = new Rect(nameRect.x, nameRect.yMax + 2f, leftColumnWidth - 10f, 22f);
                GUI.color = Color.gray;
                Widgets.Label(bodyPartRect, $"Target: {surgery.BodyPart.LabelCap}");
                GUI.color = Color.white;
            }

            // Mod source badge
            var modName = surgery.Recipe?.modContentPack?.Name ?? "Core";
            if (modName != "Core")
            {
                Rect modBadgeRect = new Rect(nameRect.x, rect.yMax - 18f, leftColumnWidth - 10f, 15f);
                GUI.color = Color.cyan;
                Text.Font = GameFont.Tiny;
                Widgets.Label(modBadgeRect, $"[{modName}]");
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
            }

            // Middle column - Category and requirements
            var middleColumnX = rect.x + leftColumnWidth;
            var middleColumnWidth = rect.width * 0.3f;
            
            Rect categoryRect = new Rect(middleColumnX, rect.y + 5f, middleColumnWidth, 20f);
            GUI.color = GetCategoryColor(surgery.Category);
            Widgets.Label(categoryRect, surgery.Category.ToString());
            GUI.color = Color.white;

            Rect reqRect = new Rect(middleColumnX, categoryRect.yMax + 2f, middleColumnWidth, rect.height - categoryRect.height - 10f);
            Text.Font = GameFont.Tiny;
            Widgets.Label(reqRect, surgery.Requirements);
            Text.Font = GameFont.Small;

            // Right column - Warnings and button
            var rightColumnX = middleColumnX + middleColumnWidth;
            var rightColumnWidth = rect.width * 0.25f;

            // Existing implant warning
            if (!string.IsNullOrEmpty(surgery.ImplantWarning))
            {
                Rect warningRect = new Rect(rightColumnX, rect.y + 5f, rightColumnWidth, 30f);
                GUI.color = surgery.ImplantWarningColor;
                Text.Font = GameFont.Tiny;
                Widgets.Label(warningRect, "⚠ " + surgery.ImplantWarning);
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
            }

            // Add to queue button
            Rect buttonRect = new Rect(rightColumnX, rect.yMax - 30f, rightColumnWidth - 5f, 25f);
            string buttonText;
            bool canQueue;
            
            if (surgery.IsDisabled && !allowQueueingDisabled)
            {
                buttonText = "Unavailable";
                canQueue = false;
            }
            else
            {
                buttonText = "Queue";
                canQueue = true;
            }
            
            GUI.enabled = canQueue;
            if (Widgets.ButtonText(buttonRect, buttonText))
            {
                if (surgery.IsDisabled && allowQueueingDisabled)
                {
                    // Use the force queue action for disabled surgeries
                    surgery.ForceQueueAction?.Invoke();
                }
                else
                {
                    // Use the normal action for available surgeries
                    surgery.Action?.Invoke();
                }
            }
            GUI.enabled = true;

            // Status indicator
            Rect statusRect = new Rect(rect.x + 2f, rect.y + (rect.height / 2) - 4f, 8f, 8f);
            Color statusColor;
            if (!surgery.IsDisabled)
            {
                statusColor = Color.green; // Available
            }
            else if (allowQueueingDisabled)
            {
                statusColor = Color.yellow; // Disabled but can be queued
            }
            else
            {
                statusColor = Color.red; // Disabled and cannot be queued
            }
            Widgets.DrawBoxSolid(statusRect, statusColor);

            // Tooltip with comprehensive info
            if (Mouse.IsOver(rect))
            {
                string statusText;
                if (!surgery.IsDisabled)
                {
                    statusText = "Available";
                }
                else if (allowQueueingDisabled)
                {
                    statusText = "Not Available (but can be queued for later)";
                }
                else
                {
                    statusText = "Not Available";
                }
                
                var tooltipText = $"{surgery.Tooltip}\n\nMod: {modName}\nStatus: {statusText}";
                TooltipHandler.TipRegion(rect, tooltipText);
            }
        }
        
        private Color GetCategoryColor(SurgeryCategory category)
        {
            switch (category)
            {
                case SurgeryCategory.Medical: return Color.white;
                case SurgeryCategory.Prosthetics: return Color.cyan;
                case SurgeryCategory.Implants: return Color.magenta;
                case SurgeryCategory.Removal: return Color.yellow;
                case SurgeryCategory.Amputation: return Color.red;
                default: return Color.gray;
            }
        }

        private void BuildFullSurgeryList()
        {
            allPawnSurgeries = new List<SurgeryOptionCached>();
            var generateSurgeryOptionMethod = typeof(HealthCardUtility).GetMethod("GenerateSurgeryOption", BindingFlags.NonPublic | BindingFlags.Static);
            if (generateSurgeryOptionMethod == null)
            {
                Log.Error("Corvus Surgery UI: Could not find vanilla method 'GenerateSurgeryOption' via reflection. Mod will not function.");
                return;
            }

            var recipes = thingForMedBills.def.AllRecipes;
            int index = 0;
            
            foreach (var recipe in recipes)
            {
                if (!recipe.IsSurgery) continue;

                // Explicitly check research before anything else.
                // A recipe isn't even a "possibility" until it's been researched.
                if ((recipe.researchPrerequisite != null && !recipe.researchPrerequisite.IsFinished) || 
                    (recipe.researchPrerequisites != null && recipe.researchPrerequisites.Any(r => !r.IsFinished)))
                {
                    continue;
                }
                
                var report = recipe.Worker.AvailableReport(pawn);
                if (!report.Accepted && report.Reason.NullOrEmpty()) continue;

                var missingIngredients = recipe.PotentiallyMissingIngredients(null, thingForMedBills.MapHeld);

                if (recipe.targetsBodyPart)
                {
                    foreach (var part in recipe.Worker.GetPartsToApplyOn(pawn, recipe))
                    {
                        if (recipe.AvailableOnNow(pawn, part))
                        {
                            try
                            {
                                var parameters = new object[] { pawn, thingForMedBills, recipe, missingIngredients, report, index, part };
                                var option = (FloatMenuOption)generateSurgeryOptionMethod.Invoke(null, parameters);
                                AddSurgeryOption(option, recipe, part);
                                index++;
                            }
                            catch (Exception ex)
                            {
                                Log.Warning($"Corvus Surgery UI: Error generating targeted surgery option for '{recipe.defName}': {ex.InnerException?.Message ?? ex.Message}");
                            }
                        }
                    }
                }
                else
                {
                     if (!pawn.health.hediffSet.HasHediff(recipe.addsHediff))
                     {
                        try
                        {
                            var parameters = new object[] { pawn, thingForMedBills, recipe, missingIngredients, report, index, null };
                            var option = (FloatMenuOption)generateSurgeryOptionMethod.Invoke(null, parameters);
                            AddSurgeryOption(option, recipe, null);
                            index++;
                        }
                        catch (Exception ex)
                        {
                            Log.Warning($"Corvus Surgery UI: Error generating non-targeted surgery option for '{recipe.defName}': {ex.InnerException?.Message ?? ex.Message}");
                        }
                     }
                }
            }
            
            Log.Message($"Corvus Surgery UI: Built master list with {allPawnSurgeries.Count} surgeries for {pawn.Name.ToStringShort}.");
        }
        
        private void ApplyFilters()
        {
            if (allPawnSurgeries == null) return;
            
            filteredSurgeries = allPawnSurgeries.Where(PassesBasicFilters).OrderBy(s => s.Label).ToList();
            Log.Message($"Corvus Surgery UI: Applied filters, {filteredSurgeries.Count} surgeries visible.");
        }

        private void AddSurgeryOption(FloatMenuOption option, RecipeDef recipe, BodyPartRecord part)
        {
            if (option == null || option.Label.NullOrEmpty())
            {
                return;
            }
            
            // Wrap the original action to refresh the bill queue after adding
            var originalAction = option.action;
            var wrappedAction = new System.Action(() => {
                originalAction?.Invoke();
                LoadQueuedBills(); // Refresh the queue after adding
            });
            
            // Create a custom action for disabled surgeries that can be force-queued
            var forceQueueAction = new System.Action(() => {
                ForceQueueSurgery(recipe, part);
                LoadQueuedBills(); // Refresh the queue after adding
            });
            
            allPawnSurgeries.Add(new SurgeryOptionCached
            {
                Label = option.Label,
                Description = recipe.description,
                Category = CategorizeRecipe(recipe),
                IsAvailable = !option.Disabled,
                IsDisabled = option.Disabled,
                Action = wrappedAction,
                ForceQueueAction = forceQueueAction, // New action for disabled surgeries
                Recipe = recipe,
                BodyPart = part
            });
        }

        private SurgeryCategory CategorizeRecipe(RecipeDef recipe)
        {
            if (recipe == null) return SurgeryCategory.Medical;
            return recipeCategoryCache.GetValueOrDefault(recipe, SurgeryCategory.Medical);
        }
        
        private bool PassesBasicFilters(SurgeryOptionCached surgery)
        {
            // Search filter
            if (!string.IsNullOrEmpty(searchFilter))
            {
                string searchLower = searchFilter.ToLower();
                bool matchesLabel = surgery.Label.ToLower().Contains(searchLower);
                bool matchesDescription = surgery.Recipe?.description?.ToLower().Contains(searchLower) ?? false;
                
                if (!matchesLabel && !matchesDescription)
                {
                    return false;
                }
            }

            // Category filter
            if (selectedCategory != SurgeryCategory.All && surgery.Category != selectedCategory)
            {
                return false;
            }

            // Mod filter
            if (selectedModFilter != "All" && (surgery.Recipe?.modContentPack?.Name ?? "Core") != selectedModFilter)
            {
                return false;
            }

            // Target filter
            if (selectedTargetPart != null && surgery.BodyPart != selectedTargetPart)
            {
                return false;
            }

            if (!PassesAvailabilityFilter(surgery, availabilityFilter))
            {
                return false;
            }

            return true;
        }

        private string GetRequirements(RecipeDef recipe)
        {
            var requirements = new List<string>();
            
            // Research requirements
            if (recipe.researchPrerequisite != null && !recipe.researchPrerequisite.IsFinished)
            {
                requirements.Add($"Research: {recipe.researchPrerequisite.LabelCap}");
            }
            
            if (recipe.researchPrerequisites != null)
            {
                var missingResearch = recipe.researchPrerequisites.Where(r => !r.IsFinished);
                foreach (var research in missingResearch)
                {
                    requirements.Add($"Research: {research.LabelCap}");
                }
            }
            
            // Skill requirements
            if (recipe.skillRequirements?.Any() == true)
            {
                var skill = recipe.skillRequirements.First();
                var hasSkill = pawn.Map?.mapPawns?.FreeColonists?.Any(p => 
                    p.skills?.GetSkill(skill.skill)?.Level >= skill.minLevel) ?? false;
                    
                var status = hasSkill ? "✓" : "✗";
                requirements.Add($"{status} {skill.skill.skillLabel} {skill.minLevel}");
            }
            
            // Ingredient requirements
            if (recipe.ingredients?.Any() == true)
            {
                foreach (var ingredient in recipe.ingredients)
                {
                    var availableCount = 0;
                    if (pawn.Map != null)
                    {
                        foreach (var thing in pawn.Map.listerThings.AllThings.Where(t => ingredient.filter.Allows(t)))
                        {
                            availableCount += thing.stackCount;
                        }
                    }
                    
                    var needed = ingredient.GetBaseCount();
                    var status = availableCount >= needed ? "✓" : "✗";
                    var itemName = ingredient.filter.Summary;
                    requirements.Add($"{status} {itemName} ({availableCount}/{needed})");
                }
            }
            
            return requirements.Any() ? string.Join(", ", requirements) : "No requirements";
        }

        private (string, Color) GetImplantWarning(RecipeDef recipe, BodyPartRecord part)
        {
            // Check if this would replace an existing implant
            if (recipe.addsHediff == null || part == null) return ("", Color.white);
            
            var existingImplantHediff = pawn.health.hediffSet.hediffs
                .FirstOrDefault(h => h.Part == part && h.def.isBad == false && h.def.countsAsAddedPartOrImplant);
            
            if (existingImplantHediff == null)
            {
                return ("", Color.white);
            }

            var newHediffDef = recipe.addsHediff;
            var oldHediffDef = existingImplantHediff.def;
            
            var warningText = $"Replaces {oldHediffDef.label}";
            var warningColor = Color.yellow; // Default

            // Compare part efficiency first, then market value
            bool newHasEff = newHediffDef.addedPartProps != null && newHediffDef.addedPartProps.partEfficiency > 0;
            bool oldHasEff = oldHediffDef.addedPartProps != null && oldHediffDef.addedPartProps.partEfficiency > 0;

            if (newHasEff && oldHasEff)
            {
                float newEff = newHediffDef.addedPartProps.partEfficiency;
                float oldEff = oldHediffDef.addedPartProps.partEfficiency;
                if (newEff > oldEff) warningColor = Color.green;
                else if (newEff < oldEff) warningColor = Color.red;
            }
            else
            {
                var newMarketValue = recipe.products?.FirstOrDefault()?.thingDef?.BaseMarketValue ?? 0f;
                var oldMarketValue = oldHediffDef.spawnThingOnRemoved?.BaseMarketValue ?? 0f;

                if (newMarketValue > 0 || oldMarketValue > 0)
                {
                    if (newMarketValue > oldMarketValue) warningColor = Color.green;
                    else if (newMarketValue < oldMarketValue) warningColor = Color.red;
                }
            }
            
            return (warningText, warningColor);
        }

        private string GetDetailedTooltip(RecipeDef recipe, BodyPartRecord part)
        {
            return $"{recipe.description}\n\nBody part: {part?.Label ?? "None"}\nSkill: {recipe.skillRequirements?.FirstOrDefault()?.skill.skillLabel ?? "None"}";
        }

        private bool IsNonTargetedSurgery(RecipeDef recipe)
        {
            return nonTargetedCache.GetValueOrDefault(recipe, false);
        }

        private static void InitializeStaticCaches()
        {
            if (cacheInitialized) return;
            
            // Cache recipe categories and non-targeted status for all recipes
            var allRecipes = DefDatabase<RecipeDef>.AllDefs.ToList();
            
            foreach (var recipe in allRecipes)
            {
                if (!recipeCategoryCache.ContainsKey(recipe))
                {
                    recipeCategoryCache[recipe] = CategorizeRecipeStatic(recipe);
                }
                
                if (!nonTargetedCache.ContainsKey(recipe))
                {
                    nonTargetedCache[recipe] = IsNonTargetedSurgeryStatic(recipe);
                }
            }
            
            cacheInitialized = true;
        }
        
        private static SurgeryCategory CategorizeRecipeStatic(RecipeDef recipe)
        {
            if (recipe.addsHediff != null)
            {
                if (recipe.addsHediff.addedPartProps != null)
                    return SurgeryCategory.Prosthetics;
                else
                    return SurgeryCategory.Implants;
            }
            
            if (recipe.removesHediff != null)
                return SurgeryCategory.Removal;
            
            if (recipe.workerClass == typeof(Recipe_RemoveBodyPart))
                return SurgeryCategory.Amputation;
            
            return SurgeryCategory.Medical;
        }
        
        private static bool IsNonTargetedSurgeryStatic(RecipeDef recipe)
        {
            // Check if this is a non-targeted surgery (like drug administration)
            if (recipe.targetsBodyPart == false) return true;
            if (recipe.LabelCap.ToString().ToLower().Contains("administer")) return true;
            if (recipe.workerClass?.Name?.Contains("Administer") == true) return true;
            if (recipe.LabelCap.ToString().ToLower().Contains("tend")) return true;
            
            return false;
        }

        public static void ClearStaticCaches()
        {
            recipeCategoryCache.Clear();
            nonTargetedCache.Clear();
            cacheInitialized = false;
            
            Log.Message("Corvus Surgery UI: Caches cleared.");
        }

        private void LoadQueuedBills()
        {
            queuedBills.Clear();
            if (thingForMedBills is IBillGiver billGiver && billGiver.BillStack != null)
            {
                queuedBills.AddRange(billGiver.BillStack.Bills.OfType<Bill_Medical>());
            }
        }

        private void DrawQueuedBills(Rect rect)
        {
            // Preset management section
            var presetSectionHeight = 25f;
            var presetRect = new Rect(rect.x, rect.y, rect.width, presetSectionHeight);
            DrawPresetSection(presetRect);
            
            // Adjust the queue area to account for preset section
            var queueStartY = rect.y + presetSectionHeight + 5f;
            var queueHeight = rect.height - presetSectionHeight - 5f;
            
            // Section header
            Text.Font = GameFont.Medium;
            var headerRect = new Rect(rect.x, queueStartY, rect.width - 190f, 25f); // Increased from 160f to accommodate wider buttons
            Widgets.Label(headerRect, $"Surgery Queue ({queuedBills.Count} bills)");
            Text.Font = GameFont.Small;
            
            // Bulk action buttons
            if (queuedBills.Count > 0)
            {
                var suspendAllRect = new Rect(rect.xMax - 185f, queueStartY, 85f, 20f); // Increased width from 70f to 85f
                if (Widgets.ButtonText(suspendAllRect, "Suspend All"))
                {
                    SuspendAllBills();
                }
                
                var activateAllRect = new Rect(rect.xMax - 95f, queueStartY, 90f, 20f); // Increased width from 75f to 90f
                if (Widgets.ButtonText(activateAllRect, "Activate All"))
                {
                    ActivateAllBills();
                }
            }
            
            // Queue area
            var queueRect = new Rect(rect.x, queueStartY + 30f, rect.width, queueHeight - 30f);
            Widgets.DrawBoxSolid(queueRect, Color.black * 0.2f);
            Widgets.DrawBox(queueRect, 1);
            
            if (queuedBills.Count == 0)
            {
                var noQueueRect = new Rect(queueRect.x + 10f, queueRect.y + 10f, queueRect.width - 20f, 30f);
                GUI.color = Color.gray;
                Widgets.Label(noQueueRect, "No surgeries queued. Click 'Queue' on surgeries below to add them.");
                GUI.color = Color.white;
                return;
            }
            
            // Handle drag and drop for bill reordering
            HandleBillDragAndDrop(queueRect);
            
            // Draw queued bills with drag-and-drop support
            float billHeight = 30f;
            float billSpacing = 2f;
            var billsViewRect = new Rect(0f, 0f, queueRect.width - 20f, (billHeight + billSpacing) * queuedBills.Count);
            
            Widgets.BeginScrollView(queueRect, ref billScrollPosition, billsViewRect);
            
            float y = 0f;
            for (int i = 0; i < queuedBills.Count; i++)
            {
                var bill = queuedBills[i];
                var billRect = new Rect(5f, y, billsViewRect.width - 10f, billHeight);
                
                // Draw drop indicator
                if (dropTargetIndex == i && isDragging)
                {
                    var dropIndicatorRect = new Rect(billRect.x, billRect.y - 2f, billRect.width, 4f);
                    Widgets.DrawBoxSolid(dropIndicatorRect, Color.cyan);
                }
                
                // Skip drawing the dragged item at its original position
                if (isDragging && draggedIndex == i)
                {
                    y += billHeight + billSpacing;
                    continue;
                }
                
                DrawBillItem(billRect, bill, i);
                y += billHeight + billSpacing;
            }
            
            // Draw drop indicator at the end
            if (dropTargetIndex == queuedBills.Count && isDragging)
            {
                var dropIndicatorRect = new Rect(5f, y - 2f, billsViewRect.width - 10f, 4f);
                Widgets.DrawBoxSolid(dropIndicatorRect, Color.cyan);
            }
            
            Widgets.EndScrollView();
            
            // Draw dragged item on top
            if (isDragging && draggedIndex >= 0 && draggedIndex < queuedBills.Count)
            {
                var draggedBill = queuedBills[draggedIndex];
                var dragRect = new Rect(Event.current.mousePosition.x + dragOffset.x, 
                                      Event.current.mousePosition.y + dragOffset.y, 
                                      billsViewRect.width - 10f, billHeight);
                
                // Draw with transparency to show it's being dragged
                GUI.color = new Color(1f, 1f, 1f, 0.8f);
                DrawBillItem(dragRect, draggedBill, draggedIndex);
                GUI.color = Color.white;
            }
        }

        private void DrawPresetSection(Rect rect)
        {
            // Save Preset button
            var saveButtonRect = new Rect(rect.x, rect.y, 100f, rect.height);
            if (Widgets.ButtonText(saveButtonRect, "Save Preset"))
            {
                ShowSavePresetDialog();
            }
            
            // Preset dropdown (with validation indicator)
            var dropdownRect = new Rect(saveButtonRect.xMax + 10f, rect.y, 150f, rect.height);
            string dropdownText = selectedPreset;
            
            // Add red X for invalid presets
            if (selectedPreset != "(none)")
            {
                var presets = SurgeryPresetManager.Instance.GetCurrentSavePresets();
                if (presets.ContainsKey(selectedPreset) && !presets[selectedPreset].IsValid)
                {
                    dropdownText = "❌ " + selectedPreset;
                }
            }
            
            if (Widgets.ButtonText(dropdownRect, dropdownText))
            {
                ShowPresetDropdown();
            }
            
            // Load button (only if a preset is selected)
            if (selectedPreset != "(none)")
            {
                var loadButtonRect = new Rect(dropdownRect.xMax + 10f, rect.y, 80f, rect.height);
                if (Widgets.ButtonText(loadButtonRect, "Load"))
                {
                    LoadPreset(selectedPreset);
                }
            }
            
            // Single Import button
            var importButtonRect = new Rect(dropdownRect.xMax + (selectedPreset != "(none)" ? 95f : 15f), rect.y, 80f, rect.height);
            if (Widgets.ButtonText(importButtonRect, "Import"))
            {
                ShowConsolidatedImportDialog();
            }
            
            // Export button (only if a preset is selected)
            if (selectedPreset != "(none)")
            {
                var exportButtonRect = new Rect(importButtonRect.xMax + 10f, rect.y, 60f, rect.height);
                if (Widgets.ButtonText(exportButtonRect, "Export"))
                {
                    ShowExportDialog();
                }
            }
        }

        private void SuspendAllBills()
        {
            try
            {
                int suspendedCount = 0;
                foreach (var bill in queuedBills)
                {
                    if (!bill.suspended)
                    {
                        bill.suspended = true;
                        suspendedCount++;
                    }
                }
                Log.Message($"Corvus Surgery UI: Suspended {suspendedCount} bills");
            }
            catch (Exception ex)
            {
                Log.Error($"Corvus Surgery UI: Error suspending all bills: {ex}");
            }
        }

        private void ActivateAllBills()
        {
            try
            {
                int activatedCount = 0;
                foreach (var bill in queuedBills)
                {
                    if (bill.suspended)
                    {
                        bill.suspended = false;
                        activatedCount++;
                    }
                }
                Log.Message($"Corvus Surgery UI: Activated {activatedCount} bills");
            }
            catch (Exception ex)
            {
                Log.Error($"Corvus Surgery UI: Error activating all bills: {ex}");
            }
        }

        private void DrawBillItem(Rect billRect, Bill_Medical bill, int index)
        {
            // Background - different color for suspended bills
            Color bgColor;
            if (bill.suspended)
            {
                bgColor = index % 2 == 0 ? Color.red * 0.2f : Color.red * 0.3f; // Reddish for suspended
            }
            else
            {
                bgColor = index % 2 == 0 ? Color.black * 0.1f : Color.black * 0.2f; // Normal colors
            }
            Widgets.DrawBoxSolid(billRect, bgColor);
            
            // Bill info
            var labelRect = new Rect(billRect.x + 5f, billRect.y + 5f, billRect.width - 160f, billRect.height - 10f);
            string billLabel = bill.LabelCap;
            if (bill.Part != null)
            {
                billLabel += $" ({bill.Part.LabelCap})";
            }
            
            // Add suspension indicator to label
            if (bill.suspended)
            {
                GUI.color = Color.yellow;
                billLabel = "⏸ " + billLabel + " (SUSPENDED)";
            }
            else
            {
                GUI.color = Color.white;
            }
            
            Widgets.Label(labelRect, billLabel);
            GUI.color = Color.white;
            
            // Suspend/Activate button
            var suspendButtonRect = new Rect(billRect.xMax - 155f, billRect.y + 3f, 70f, billRect.height - 6f);
            string suspendButtonText = bill.suspended ? "Activate" : "Suspend";
            Color suspendButtonColor = bill.suspended ? Color.green : Color.yellow;
            
            GUI.color = suspendButtonColor;
            if (Widgets.ButtonText(suspendButtonRect, suspendButtonText))
            {
                ToggleBillSuspension(index);
            }
            GUI.color = Color.white;
            
            // Remove button
            var removeButtonRect = new Rect(billRect.xMax - 80f, billRect.y + 3f, 75f, billRect.height - 6f);
            if (Widgets.ButtonText(removeButtonRect, "Remove"))
            {
                RemoveBill(index);
            }
            
            // Priority indicators
            var priorityRect = new Rect(billRect.xMax - 170f, billRect.y + 5f, 50f, billRect.height - 10f);
            GUI.color = bill.suspended ? Color.gray : Color.yellow;
            Widgets.Label(priorityRect, $"#{index + 1}");
            GUI.color = Color.white;
        }

        private void ToggleBillSuspension(int index)
        {
            try
            {
                if (index >= 0 && index < queuedBills.Count)
                {
                    var bill = queuedBills[index];
                    bill.suspended = !bill.suspended;
                    
                    string action = bill.suspended ? "suspended" : "activated";
                    Log.Message($"Corvus Surgery UI: {action} bill '{bill.LabelCap}' at index {index}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Corvus Surgery UI: Error toggling bill suspension: {ex}");
            }
        }

        private void HandleBillDragAndDrop(Rect queueRect)
        {
            Event e = Event.current;
            var billHeight = 30f;
            var billSpacing = 2f;
            
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button == 0 && queueRect.Contains(e.mousePosition))
                    {
                        // Calculate which bill was clicked
                        var relativeY = e.mousePosition.y - queueRect.y + billScrollPosition.y;
                        var clickedIndex = Mathf.FloorToInt(relativeY / (billHeight + billSpacing));
                        
                        if (clickedIndex >= 0 && clickedIndex < queuedBills.Count)
                        {
                            // Check if click is not on the remove or suspend buttons
                            var billRect = new Rect(5f, clickedIndex * (billHeight + billSpacing), queueRect.width - 20f, billHeight);
                            var removeButtonRect = new Rect(billRect.xMax - 80f, billRect.y + 3f, 75f, billRect.height - 6f);
                            var suspendButtonRect = new Rect(billRect.xMax - 155f, billRect.y + 3f, 70f, billRect.height - 6f);
                            var localMousePos = new Vector2(e.mousePosition.x - queueRect.x, relativeY);
                            
                            if (!removeButtonRect.Contains(localMousePos) && !suspendButtonRect.Contains(localMousePos))
                            {
                                isDragging = true;
                                draggedIndex = clickedIndex;
                                dragOffset = new Vector2(-150f, -15f); // Center the dragged item on cursor
                                e.Use();
                            }
                        }
                    }
                    break;
                    
                case EventType.MouseUp:
                    if (e.button == 0 && isDragging)
                    {
                        // Calculate drop position
                        var relativeY = e.mousePosition.y - queueRect.y + billScrollPosition.y;
                        var dropIndex = Mathf.FloorToInt(relativeY / (billHeight + billSpacing));
                        dropIndex = Mathf.Clamp(dropIndex, 0, queuedBills.Count);
                        
                        // Perform the reorder if valid
                        if (dropIndex != draggedIndex && draggedIndex >= 0 && draggedIndex < queuedBills.Count)
                        {
                            ReorderBill(draggedIndex, dropIndex);
                        }
                        
                        isDragging = false;
                        draggedIndex = -1;
                        dropTargetIndex = -1;
                        e.Use();
                    }
                    break;
                    
                case EventType.MouseDrag:
                    if (isDragging)
                    {
                        // Update drop target index for visual feedback
                        var relativeY = e.mousePosition.y - queueRect.y + billScrollPosition.y;
                        dropTargetIndex = Mathf.FloorToInt(relativeY / (billHeight + billSpacing));
                        dropTargetIndex = Mathf.Clamp(dropTargetIndex, 0, queuedBills.Count);
                        e.Use();
                    }
                    break;
            }
        }

        private void ReorderBill(int fromIndex, int toIndex)
        {
            try
            {
                // Find the corresponding bill in the actual bill stack
                if (fromIndex < 0 || fromIndex >= queuedBills.Count) return;
                if (toIndex < 0 || toIndex > queuedBills.Count) return;
                
                var billToMove = queuedBills[fromIndex];
                
                // Get the bill giver (medical bed/facility)
                if (thingForMedBills is IBillGiver billGiver && billGiver.BillStack != null)
                {
                    // Remove the bill from its current position
                    billGiver.BillStack.Delete(billToMove);
                    
                    // Insert it at the new position
                    if (toIndex >= billGiver.BillStack.Count)
                    {
                        billGiver.BillStack.AddBill(billToMove);
                    }
                    else
                    {
                        var billsToMove = new List<Bill>();
                        for (int i = toIndex; i < billGiver.BillStack.Count; i++)
                        {
                            billsToMove.Add(billGiver.BillStack[i]);
                        }
                        
                        // Remove bills after insertion point
                        for (int i = billGiver.BillStack.Count - 1; i >= toIndex; i--)
                        {
                            billGiver.BillStack.Delete(billGiver.BillStack[i]);
                        }
                        
                        // Add the moved bill
                        billGiver.BillStack.AddBill(billToMove);
                        
                        // Re-add the moved bills
                        foreach (var bill in billsToMove)
                        {
                            billGiver.BillStack.AddBill(bill);
                        }
                    }
                    
                    // Refresh our local bill list
                    LoadQueuedBills();
                    
                    Log.Message($"Corvus Surgery UI: Reordered bill from position {fromIndex} to {toIndex}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Corvus Surgery UI: Error reordering bill: {ex}");
            }
        }

        private void RemoveBill(int index)
        {
            try
            {
                if (index >= 0 && index < queuedBills.Count)
                {
                    var bill = queuedBills[index];
                    if (thingForMedBills is IBillGiver billGiver && billGiver.BillStack != null)
                    {
                        billGiver.BillStack.Delete(bill);
                        LoadQueuedBills(); // Refresh the list
                        Log.Message($"Corvus Surgery UI: Removed bill at index {index}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Corvus Surgery UI: Error removing bill: {ex}");
            }
        }

        private string GetAvailabilityFilterText(AvailabilityFilter filter)
        {
            switch (filter)
            {
                case AvailabilityFilter.ShowAll:
                    return "Show All";
                case AvailabilityFilter.ShowAvailableOnly:
                    return "Available Only";
                case AvailabilityFilter.MissingItem:
                    return "Missing Item";
                case AvailabilityFilter.MissingSkill:
                    return "Missing Skill";
                default:
                    return "Show All";
            }
        }

        private int GetFilteredCount(AvailabilityFilter filter)
        {
            if (allPawnSurgeries == null) return 0;
            
            return allPawnSurgeries.Count(surgery => PassesAvailabilityFilter(surgery, filter));
        }

        private bool PassesAvailabilityFilter(SurgeryOptionCached surgery, AvailabilityFilter filter)
        {
            switch (filter)
            {
                case AvailabilityFilter.ShowAll:
                    return true;
                case AvailabilityFilter.ShowAvailableOnly:
                    return !surgery.IsDisabled;
                case AvailabilityFilter.MissingItem:
                    return surgery.IsDisabled && IsMissingItems(surgery);
                case AvailabilityFilter.MissingSkill:
                    return surgery.IsDisabled && IsMissingSkill(surgery);
                default:
                    return true;
            }
        }

        private bool IsMissingItems(SurgeryOptionCached surgery)
        {
            if (surgery.Recipe?.ingredients == null) return false;
            
            foreach (var ingredient in surgery.Recipe.ingredients)
            {
                var availableCount = 0;
                if (pawn.Map != null)
                {
                    foreach (var thing in pawn.Map.listerThings.AllThings.Where(t => ingredient.filter.Allows(t)))
                    {
                        availableCount += thing.stackCount;
                    }
                }
                
                var needed = ingredient.GetBaseCount();
                if (availableCount < needed)
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsMissingSkill(SurgeryOptionCached surgery)
        {
            if (surgery.Recipe?.skillRequirements?.Any() != true) return false;
            
            var skill = surgery.Recipe.skillRequirements.First();
            var hasSkill = pawn.Map?.mapPawns?.FreeColonists?.Any(p => 
                p.skills?.GetSkill(skill.skill)?.Level >= skill.minLevel) ?? false;
                
            return !hasSkill;
        }
        
        private void ForceQueueSurgery(RecipeDef recipe, BodyPartRecord part)
        {
            try
            {
                if (thingForMedBills is IBillGiver billGiver && billGiver.BillStack != null)
                {
                    // Create a new medical bill directly with empty ingredients list
                    Bill_Medical bill = new Bill_Medical(recipe, null);
                    bill.Part = part;
                    
                    // Automatically suspend force-queued bills so pawns don't wait in bed
                    bill.suspended = true;
                    
                    // Add the bill to the stack
                    billGiver.BillStack.AddBill(bill);
                    
                    // Register this bill as auto-suspended for periodic checking
                    AutoSuspendTracker.RegisterAutoSuspendedBill(bill, thingForMedBills, pawn);
                    
                    Log.Message($"Corvus Surgery UI: Force-queued '{recipe.LabelCap}' on {part?.LabelCap ?? "whole body"} (automatically suspended)");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Corvus Surgery UI: Error force-queueing surgery '{recipe?.defName}': {ex}");
            }
        }

        private void ShowSavePresetDialog()
        {
            if (queuedBills.Count == 0)
            {
                Messages.Message("No surgeries to save in preset.", MessageTypeDefOf.RejectInput);
                return;
            }
            
            Find.WindowStack.Add(new Dialog_PresetNaming((name) => {
                if (!name.NullOrEmpty())
                {
                    SavePreset(name);
                    selectedPreset = name;
                }
            }));
        }

        private void ShowPresetDropdown()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            
            // Add "(none)" option
            options.Add(new FloatMenuOption("(none)", () => {
                selectedPreset = "(none)";
            }));
            
            // Add saved presets with validation indicators
            var presets = SurgeryPresetManager.Instance.GetCurrentSavePresets();
            foreach (var kvp in presets)
            {
                var name = kvp.Key; // Capture for closure
                var preset = kvp.Value;
                string displayName = preset.IsValid 
                    ? $"{name} ({preset.Items.Count} surgeries)"
                    : $"❌ {name} ({preset.Items.Count} surgeries - {preset.ValidationErrors.Count} issues)";
                
                options.Add(new FloatMenuOption(displayName, () => {
                    selectedPreset = name;
                }));
            }
            
            if (presets.Count == 0)
            {
                options.Add(new FloatMenuOption("No presets saved", null) { Disabled = true });
            }
            
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void SavePreset(string name)
        {
            try
            {
                var presetItems = new List<SurgeryPresetItem>();
                
                foreach (var bill in queuedBills)
                {
                    presetItems.Add(new SurgeryPresetItem(bill.recipe, bill.Part, bill.suspended));
                }
                
                SurgeryPresetManager.Instance.SavePreset(name, presetItems, () => {
                    selectedPreset = name;
                });
                
                Log.Message($"Corvus Surgery UI: Saved preset '{name}' with {presetItems.Count} surgeries.");
            }
            catch (Exception ex)
            {
                Log.Error($"Corvus Surgery UI: Error saving preset '{name}': {ex}");
                Messages.Message("Error saving preset.", MessageTypeDefOf.RejectInput);
            }
        }

        private void LoadPreset(string name)
        {
            try
            {
                if (!SurgeryPresetManager.Instance.LoadPreset(name, pawn, thingForMedBills))
                {
                    return;
                }
                
                LoadQueuedBills(); // Refresh our local list
                Messages.Message($"Loaded preset '{name}': {queuedBills.Count} surgeries.", MessageTypeDefOf.PositiveEvent);
                
                Log.Message($"Corvus Surgery UI: Loaded preset '{name}' with {queuedBills.Count} surgeries");
            }
            catch (Exception ex)
            {
                Log.Error($"Corvus Surgery UI: Error loading preset '{name}': {ex}");
                Messages.Message("Error loading preset.", MessageTypeDefOf.RejectInput);
            }
        }

        private BodyPartRecord FindBodyPart(string bodyPartLabel, Pawn pawn)
        {
            if (bodyPartLabel.NullOrEmpty()) return null;
            
            var parts = bodyPartLabel.Split('|');
            if (parts.Length != 2) return null;
            
            var defName = parts[0];
            if (!int.TryParse(parts[1], out int index)) return null;
            
            // Find the body part by def name and index
            foreach (var part in pawn.health.hediffSet.GetNotMissingParts())
            {
                if (part.def.defName == defName && part.Index == index)
                {
                    return part;
                }
            }
            
            return null;
        }
        
        private void ShowConsolidatedImportDialog()
        {
            Find.WindowStack.Add(new Dialog_ConsolidatedImport());
        }
        
        private void ShowImportFromSavesDialog()
        {
            Find.WindowStack.Add(new Dialog_ImportFromSaves());
        }
        
        private void ShowExportDialog()
        {
            if (selectedPreset == "(none)") return;
            
            Find.WindowStack.Add(new Dialog_ExportPreset(selectedPreset));
        }
        
        private void ShowImportFileDialog()
        {
            Find.WindowStack.Add(new Dialog_ImportPresetFile());
        }
    }

    // Data structure for cached surgery options
    public class SurgeryOptionCached
    {
        public string Label;
        public string Description;
        public SurgeryCategory Category;
        public bool IsAvailable;
        public bool IsDisabled;
        public string Requirements;
        public string ImplantWarning;
        public Color ImplantWarningColor;
        public string Tooltip;
        public Action Action;
        public Action ForceQueueAction;
        public RecipeDef Recipe;
        public BodyPartRecord BodyPart;
    }

    public enum SurgeryCategory
    {
        All,
        Medical,
        Prosthetics,
        Implants,
        Removal,
        Amputation
    }

    public enum AvailabilityFilter
    {
        ShowAll,
        ShowAvailableOnly,
        MissingItem,
        MissingSkill
    }

    public enum PawnCategory
    {
        Colonist = 0,
        Prisoner = 1,
        Slave = 2,
        Animal = 3,
        Guest = 4
    }

    public class SurgeryPresetItem
    {
        public string RecipeDefName;
        public string BodyPartLabel;
        public bool IsSuspended;
        
        public SurgeryPresetItem() { }
        
        public SurgeryPresetItem(RecipeDef recipe, BodyPartRecord bodyPart, bool suspended = false)
        {
            RecipeDefName = recipe?.defName;
            BodyPartLabel = bodyPart?.def?.defName + "|" + bodyPart?.Index;
            IsSuspended = suspended;
        }
    }

    public class Dialog_PresetNaming : Window
    {
        private string presetName = "";
        private Action<string> onConfirm;

        public Dialog_PresetNaming(Action<string> onConfirm)
        {
            this.onConfirm = onConfirm;
            this.forcePause = true;
            this.doCloseX = true;
            this.absorbInputAroundWindow = true;
            this.closeOnAccept = false;
        }

        public override Vector2 InitialSize => new Vector2(400f, 200f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            var titleRect = new Rect(0f, 0f, inRect.width, 30f);
            Widgets.Label(titleRect, "Save Surgery Preset");
            Text.Font = GameFont.Small;

            var labelRect = new Rect(0f, 40f, inRect.width, 25f);
            Widgets.Label(labelRect, "Enter preset name:");

            var textFieldRect = new Rect(0f, 70f, inRect.width, 30f);
            presetName = Widgets.TextField(textFieldRect, presetName);

            // Buttons
            var buttonWidth = 80f;
            var buttonHeight = 35f;
            var spacing = 20f;
            var totalButtonWidth = (buttonWidth * 2) + spacing;
            var buttonStartX = (inRect.width - totalButtonWidth) / 2f;

            var cancelRect = new Rect(buttonStartX, inRect.height - buttonHeight - 10f, buttonWidth, buttonHeight);
            if (Widgets.ButtonText(cancelRect, "Cancel"))
            {
                Close();
            }

            var confirmRect = new Rect(buttonStartX + buttonWidth + spacing, inRect.height - buttonHeight - 10f, buttonWidth, buttonHeight);
            if (Widgets.ButtonText(confirmRect, "Save") || (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return))
            {
                if (!presetName.NullOrEmpty())
                {
                    onConfirm?.Invoke(presetName);
                    Close();
                }
            }
        }
    }

    public class Dialog_ImportFromSaves : Window
    {
        private Vector2 scrollPosition = Vector2.zero;
        private Dictionary<string, List<PersistentSurgeryPreset>> presetsBySave;

        public Dialog_ImportFromSaves()
        {
            this.forcePause = true;
            this.doCloseX = true;
            this.absorbInputAroundWindow = true;
            presetsBySave = SurgeryPresetManager.Instance.GetPresetsBySave();
        }

        public override Vector2 InitialSize => new Vector2(600f, 500f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            var titleRect = new Rect(0f, 0f, inRect.width, 30f);
            Widgets.Label(titleRect, "Import Presets from Other Saves");
            Text.Font = GameFont.Small;

            var currentSaveId = SurgeryPresetManager.Instance.GetCurrentSaveIdentifier();
            var scrollRect = new Rect(0f, 40f, inRect.width, inRect.height - 80f);
            var viewRect = new Rect(0f, 0f, scrollRect.width - 20f, CalculateViewHeight());

            Widgets.BeginScrollView(scrollRect, ref scrollPosition, viewRect);

            float y = 0f;
            foreach (var saveGroup in presetsBySave)
            {
                if (saveGroup.Key == currentSaveId) continue; // Skip current save

                var saveName = saveGroup.Value.FirstOrDefault()?.SaveDisplayName ?? "Unknown Save";
                var headerRect = new Rect(0f, y, viewRect.width, 30f);
                
                Text.Font = GameFont.Medium;
                Widgets.Label(headerRect, $"{saveName} ({saveGroup.Value.Count} presets)");
                Text.Font = GameFont.Small;
                y += 35f;

                foreach (var preset in saveGroup.Value)
                {
                    var presetRect = new Rect(20f, y, viewRect.width - 40f, 25f);
                    
                    string presetLabel = preset.IsValid 
                        ? $"{preset.Name} ({preset.Items.Count} surgeries)"
                        : $"❌ {preset.Name} ({preset.Items.Count} surgeries - may have issues)";
                    
                    var labelRect = new Rect(presetRect.x, presetRect.y, presetRect.width - 100f, presetRect.height);
                    Widgets.Label(labelRect, presetLabel);
                    
                    var importButtonRect = new Rect(presetRect.xMax - 90f, presetRect.y, 80f, 20f);
                    if (Widgets.ButtonText(importButtonRect, "Import"))
                    {
                        SurgeryPresetManager.Instance.ImportPresetFromOtherSave(preset.Name, saveGroup.Key);
                        Close();
                    }
                    
                    y += 30f;
                }
                
                y += 10f; // Extra spacing between saves
            }

            Widgets.EndScrollView();

            // Close button
            var closeButtonRect = new Rect((inRect.width - 80f) / 2f, inRect.height - 35f, 80f, 30f);
            if (Widgets.ButtonText(closeButtonRect, "Close"))
            {
                Close();
            }
        }

        private float CalculateViewHeight()
        {
            float height = 0f;
            var currentSaveId = SurgeryPresetManager.Instance.GetCurrentSaveIdentifier();
            
            foreach (var saveGroup in presetsBySave)
            {
                if (saveGroup.Key == currentSaveId) continue;
                height += 35f; // Header
                height += saveGroup.Value.Count * 30f; // Presets
                height += 10f; // Spacing
            }
            
            return height;
        }
    }

    public class Dialog_ExportPreset : Window
    {
        private string presetName;
        private string filePath = "";

        public Dialog_ExportPreset(string presetName)
        {
            this.presetName = presetName;
            this.forcePause = true;
            this.doCloseX = true;
            this.absorbInputAroundWindow = true;
            this.filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"{presetName}_preset.json");
        }

        public override Vector2 InitialSize => new Vector2(500f, 300f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            var titleRect = new Rect(0f, 0f, inRect.width, 30f);
            Widgets.Label(titleRect, $"Export Preset: {presetName}");
            Text.Font = GameFont.Small;

            var labelRect = new Rect(0f, 50f, inRect.width, 25f);
            Widgets.Label(labelRect, "Export to file:");

            var textFieldRect = new Rect(0f, 80f, inRect.width - 100f, 30f);
            filePath = Widgets.TextField(textFieldRect, filePath);

            var browseButtonRect = new Rect(inRect.width - 90f, 80f, 80f, 30f);
            if (Widgets.ButtonText(browseButtonRect, "Browse"))
            {
                // Note: RimWorld doesn't have a native file browser, so we'll use a simple path
                Messages.Message("Tip: Modify the path above or use the default location (Desktop)", MessageTypeDefOf.NeutralEvent);
            }

            var infoRect = new Rect(0f, 130f, inRect.width, 80f);
            Widgets.Label(infoRect, "This will export the preset to a JSON file that can be shared with other players. They can import it using the 'Import File' button.");

            // Buttons
            var buttonWidth = 80f;
            var buttonHeight = 35f;
            var spacing = 20f;
            var totalButtonWidth = (buttonWidth * 2) + spacing;
            var buttonStartX = (inRect.width - totalButtonWidth) / 2f;

            var cancelRect = new Rect(buttonStartX, inRect.height - buttonHeight - 10f, buttonWidth, buttonHeight);
            if (Widgets.ButtonText(cancelRect, "Cancel"))
            {
                Close();
            }

            var exportRect = new Rect(buttonStartX + buttonWidth + spacing, inRect.height - buttonHeight - 10f, buttonWidth, buttonHeight);
            if (Widgets.ButtonText(exportRect, "Export"))
            {
                if (!filePath.NullOrEmpty())
                {
                    SurgeryPresetManager.Instance.ExportPreset(presetName, filePath);
                    Close();
                }
            }
        }
    }

    public class Dialog_ImportPresetFile : Window
    {
        private string filePath = "";

        public Dialog_ImportPresetFile()
        {
            this.forcePause = true;
            this.doCloseX = true;
            this.absorbInputAroundWindow = true;
            this.filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "preset.json");
        }

        public override Vector2 InitialSize => new Vector2(500f, 300f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            var titleRect = new Rect(0f, 0f, inRect.width, 30f);
            Widgets.Label(titleRect, "Import Preset from File");
            Text.Font = GameFont.Small;

            var labelRect = new Rect(0f, 50f, inRect.width, 25f);
            Widgets.Label(labelRect, "Import from file:");

            var textFieldRect = new Rect(0f, 80f, inRect.width - 100f, 30f);
            filePath = Widgets.TextField(textFieldRect, filePath);

            var browseButtonRect = new Rect(inRect.width - 90f, 80f, 80f, 30f);
            if (Widgets.ButtonText(browseButtonRect, "Browse"))
            {
                Messages.Message("Tip: Modify the path above to point to your preset JSON file", MessageTypeDefOf.NeutralEvent);
            }

            var infoRect = new Rect(0f, 130f, inRect.width, 80f);
            Widgets.Label(infoRect, "Select a JSON preset file exported from this or another player's game. The preset will be imported and tagged for your current save.");

            // Buttons
            var buttonWidth = 80f;
            var buttonHeight = 35f;
            var spacing = 20f;
            var totalButtonWidth = (buttonWidth * 2) + spacing;
            var buttonStartX = (inRect.width - totalButtonWidth) / 2f;

            var cancelRect = new Rect(buttonStartX, inRect.height - buttonHeight - 10f, buttonWidth, buttonHeight);
            if (Widgets.ButtonText(cancelRect, "Cancel"))
            {
                Close();
            }

            var importRect = new Rect(buttonStartX + buttonWidth + spacing, inRect.height - buttonHeight - 10f, buttonWidth, buttonHeight);
            if (Widgets.ButtonText(importRect, "Import"))
            {
                if (!filePath.NullOrEmpty() && File.Exists(filePath))
                {
                    SurgeryPresetManager.Instance.ImportPreset(filePath);
                    Close();
                }
                else
                {
                    Messages.Message("File not found. Please check the path.", MessageTypeDefOf.RejectInput);
                }
            }
        }
    }

    public class Dialog_ConsolidatedImport : Window
    {
        public Dialog_ConsolidatedImport()
        {
            this.forcePause = true;
            this.doCloseX = true;
            this.absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new Vector2(400f, 250f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            var titleRect = new Rect(0f, 0f, inRect.width, 30f);
            Widgets.Label(titleRect, "Import Presets");
            Text.Font = GameFont.Small;

            var infoRect = new Rect(0f, 40f, inRect.width, 40f);
            Widgets.Label(infoRect, "Choose import source:");

            // Import from Other Saves button
            var importSavesRect = new Rect(50f, 90f, inRect.width - 100f, 35f);
            if (Widgets.ButtonText(importSavesRect, "Import from Other Saves"))
            {
                Close();
                Find.WindowStack.Add(new Dialog_ImportFromSaves());
            }

            // Import from File button  
            var importFileRect = new Rect(50f, 135f, inRect.width - 100f, 35f);
            if (Widgets.ButtonText(importFileRect, "Import from File"))
            {
                Close();
                Find.WindowStack.Add(new Dialog_ImportPresetFile());
            }

            // Close button
            var closeButtonRect = new Rect((inRect.width - 80f) / 2f, inRect.height - 35f, 80f, 30f);
            if (Widgets.ButtonText(closeButtonRect, "Close"))
            {
                Close();
            }
        }
    }

    public class Dialog_OverwriteConfirmation : Window
    {
        private string presetName;
        private string existingSaveName;
        private Action onConfirm;
        private Action onCancel;

        public Dialog_OverwriteConfirmation(string presetName, string existingSaveName, Action onConfirm, Action onCancel = null)
        {
            this.presetName = presetName;
            this.existingSaveName = existingSaveName;
            this.onConfirm = onConfirm;
            this.onCancel = onCancel;
            this.forcePause = true;
            this.doCloseX = true;
            this.absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new Vector2(450f, 200f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            var titleRect = new Rect(0f, 0f, inRect.width, 30f);
            Widgets.Label(titleRect, "Preset Already Exists");
            Text.Font = GameFont.Small;

            var messageRect = new Rect(0f, 40f, inRect.width, 80f);
            string message = $"A preset named '{presetName}' with the same surgeries already exists in '{existingSaveName}'.\n\nDo you want to overwrite it?";
            Widgets.Label(messageRect, message);

            // Buttons
            var buttonWidth = 80f;
            var buttonHeight = 35f;
            var spacing = 20f;
            var totalButtonWidth = (buttonWidth * 2) + spacing;
            var buttonStartX = (inRect.width - totalButtonWidth) / 2f;

            var cancelRect = new Rect(buttonStartX, inRect.height - buttonHeight - 10f, buttonWidth, buttonHeight);
            if (Widgets.ButtonText(cancelRect, "Cancel"))
            {
                onCancel?.Invoke();
                Close();
            }

            var overwriteRect = new Rect(buttonStartX + buttonWidth + spacing, inRect.height - buttonHeight - 10f, buttonWidth, buttonHeight);
            if (Widgets.ButtonText(overwriteRect, "Overwrite"))
            {
                onConfirm?.Invoke();
                Close();
            }
        }
    }

    // System for tracking auto-suspended bills and checking their availability
    public class AutoSuspendTracker : GameComponent
    {
        private static Dictionary<int, AutoSuspendedBillInfo> autoSuspendedBills = new Dictionary<int, AutoSuspendedBillInfo>();
        private int lastCheckTick = 0;
        private const int CHECK_INTERVAL_TICKS = 2500; // Check every ~1 game hour

        public AutoSuspendTracker() { }
        public AutoSuspendTracker(Game game) { }

        public static void RegisterAutoSuspendedBill(Bill_Medical bill, Thing billGiver, Pawn pawn = null)
        {
            var targetPawn = pawn ?? GetPawnFromBillGiver(billGiver);
            var info = new AutoSuspendedBillInfo
            {
                Bill = bill,
                BillGiver = billGiver,
                Pawn = targetPawn
            };
            
            autoSuspendedBills[bill.GetHashCode()] = info;
        }

        private static Pawn GetPawnFromBillGiver(Thing billGiver)
        {
            // For medical beds
            if (billGiver is Building_Bed bed)
            {
                return bed.CurOccupants.FirstOrDefault();
            }
            
            // For other medical facilities, try to find the pawn via bills
            if (billGiver is IBillGiver giver && giver.BillStack?.Bills?.Any() == true)
            {
                // Look for any existing medical bill to get the pawn
                var existingBill = giver.BillStack.Bills.OfType<Bill_Medical>().FirstOrDefault();
                if (existingBill != null)
                {
                    // For medical bills, we can't easily get the target pawn this way
                    // We'll rely on the bed occupant check or return null
                    return null;
                }
            }
            
            return null;
        }

        public static void UnregisterBill(Bill_Medical bill)
        {
            autoSuspendedBills.Remove(bill.GetHashCode());
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame - lastCheckTick >= CHECK_INTERVAL_TICKS)
            {
                CheckAutoSuspendedBills();
                lastCheckTick = Find.TickManager.TicksGame;
            }
        }

        private void CheckAutoSuspendedBills()
        {
            var billsToRemove = new List<int>();
            var billsToUnsuspend = new List<AutoSuspendedBillInfo>();

            foreach (var kvp in autoSuspendedBills)
            {
                var billInfo = kvp.Value;
                var bill = billInfo.Bill;

                // Check if bill still exists
                if (bill?.billStack == null || bill.DeletedOrDereferenced)
                {
                    billsToRemove.Add(kvp.Key);
                    continue;
                }

                // Skip if bill was manually unsuspended already
                if (!bill.suspended)
                {
                    billsToRemove.Add(kvp.Key);
                    continue;
                }

                // Check if the surgery is now available
                if (IsSurgeryNowAvailable(bill, billInfo))
                {
                    billsToUnsuspend.Add(billInfo);
                    billsToRemove.Add(kvp.Key);
                }
            }

            // Clean up removed bills
            foreach (var key in billsToRemove)
            {
                autoSuspendedBills.Remove(key);
            }

            // Unsuspend available bills
            foreach (var billInfo in billsToUnsuspend)
            {
                try
                {
                    billInfo.Bill.suspended = false;
                    Log.Message($"Corvus Surgery UI: Auto-unsuspended '{billInfo.Bill.LabelCap}' - now available");
                    
                    // Send notification to player
                    if (billInfo.Pawn != null)
                    {
                        Messages.Message($"Surgery now available: {billInfo.Bill.LabelCap} for {billInfo.Pawn.LabelShort}", 
                                       billInfo.Pawn, MessageTypeDefOf.PositiveEvent);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Corvus Surgery UI: Error auto-unsuspending bill: {ex}");
                }
            }

            if (billsToUnsuspend.Count > 0)
            {
                Log.Message($"Corvus Surgery UI: Auto-unsuspended {billsToUnsuspend.Count} bills that became available");
            }
        }

        private bool IsSurgeryNowAvailable(Bill_Medical bill, AutoSuspendedBillInfo billInfo)
        {
            try
            {
                var recipe = bill.recipe;
                var pawn = billInfo.Pawn;
                var billGiver = billInfo.BillGiver;

                if (recipe == null || pawn == null || billGiver == null) return false;

                // Check research requirements
                if ((recipe.researchPrerequisite != null && !recipe.researchPrerequisite.IsFinished) || 
                    (recipe.researchPrerequisites != null && recipe.researchPrerequisites.Any(r => !r.IsFinished)))
                {
                    return false;
                }

                // Check recipe availability report
                var report = recipe.Worker.AvailableReport(pawn);
                if (!report.Accepted && !report.Reason.NullOrEmpty()) return false;

                // Check if surgery is available on the specific body part (for targeted surgeries)
                if (recipe.targetsBodyPart && bill.Part != null)
                {
                    if (!recipe.AvailableOnNow(pawn, bill.Part)) return false;
                }

                // Check for non-targeted surgeries that add hediffs
                if (!recipe.targetsBodyPart && recipe.addsHediff != null)
                {
                    if (pawn.health.hediffSet.HasHediff(recipe.addsHediff)) return false;
                }

                // Check ingredients availability (basic check)
                var missingIngredients = recipe.PotentiallyMissingIngredients(null, billGiver.MapHeld);
                if (missingIngredients?.Any() == true)
                {
                    // Check if we actually have the ingredients available now
                    bool hasAllIngredients = true;
                    foreach (var ingredient in recipe.ingredients)
                    {
                        var availableCount = 0;
                        if (billGiver.Map != null)
                        {
                            foreach (var thing in billGiver.Map.listerThings.AllThings.Where(t => ingredient.filter.Allows(t)))
                            {
                                availableCount += thing.stackCount;
                            }
                        }
                        
                        var needed = ingredient.GetBaseCount();
                        if (availableCount < needed)
                        {
                            hasAllIngredients = false;
                            break;
                        }
                    }
                    
                    if (!hasAllIngredients) return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Corvus Surgery UI: Error checking surgery availability: {ex}");
                return false;
            }
        }

        public override void ExposeData()
        {
            // Clear on save/load since bills will be recreated
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                autoSuspendedBills.Clear();
            }
        }
    }

    public class AutoSuspendedBillInfo
    {
        public Bill_Medical Bill;
        public Thing BillGiver;
        public Pawn Pawn;
    }
}