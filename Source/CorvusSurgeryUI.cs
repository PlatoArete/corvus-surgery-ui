using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using System.Reflection;

namespace CorvusSurgeryUI
{
    [StaticConstructorOnStartup]
    public static class CorvusSurgeryUIMod
    {
        static CorvusSurgeryUIMod()
        {
            var harmony = new Harmony("corvus.surgery.ui");
            harmony.PatchAll();
            
            Log.Message("Corvus Surgery UI: Initialized");
        }
        
        // Method to clear caches - calls the method in Dialog class
        public static void ClearCaches()
        {
            Dialog_CorvusSurgeryPlanner.ClearStaticCaches();
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

                if (Widgets.ButtonText(planButtonRect, "Plan"))
                {
                    var dialog = new Dialog_CorvusSurgeryPlanner(pawn, thingForMedBills);
                    Find.WindowStack.Add(dialog);
                }
                
                // Add tooltip
                TooltipHandler.TipRegion(planButtonRect, "Open enhanced surgery planning interface with filtering and mod compatibility (or press O)");
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
        private static Dictionary<string, List<SurgeryPresetItem>> surgeryPresets = new Dictionary<string, List<SurgeryPresetItem>>();
        private string selectedPreset = "(none)";

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
            
            // Initialize static caches if needed
            InitializeStaticCaches();
            
            // Load existing queued bills
            LoadQueuedBills();
            
            BuildFullSurgeryList();
            PopulateAvailableMods();
            PopulateAvailableTargets();
            ApplyFilters();
        }

        public override Vector2 InitialSize => new Vector2(1200f, 700f);

        protected override void SetInitialSizeAndPosition()
        {
            windowRect = new Rect((UI.screenWidth - InitialSize.x) / 2f, (UI.screenHeight - InitialSize.y) / 2f, InitialSize.x, InitialSize.y);
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Title
            Text.Font = GameFont.Medium;
            var titleRect = new Rect(0f, 0f, inRect.width, 30f);
            var surgeryCount = filteredSurgeries?.Count ?? 0;
            Widgets.Label(titleRect, $"Surgery Planner - {pawn.LabelShort} ({surgeryCount} surgeries found)");
            Text.Font = GameFont.Small;

            // Filters section
            Rect filtersRect = new Rect(0f, 35f, inRect.width, 130f); // Moved up from 55f since subtitle was removed
            DrawFilters(filtersRect);

            // Split the remaining area: left for available surgeries, right for queue
            var remainingHeight = inRect.height - 170f; // Reduced from 190f since we moved filters up
            var queueWidth = 500f; // Increased from 400f for less clutter
            var surgeryListWidth = inRect.width - queueWidth - 10f; // Rest for surgeries

            // Available Surgeries (left side)
            Rect surgeryListRect = new Rect(0f, 170f, surgeryListWidth, remainingHeight); // Reduced Y from 190f
            DrawSurgeryList(surgeryListRect);

            // Surgery Queue (right side)
            Rect queuedBillsRect = new Rect(surgeryListWidth + 10f, 170f, queueWidth, remainingHeight); // Reduced Y from 190f
            DrawQueuedBills(queuedBillsRect);
        }

        private void DrawFilters(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, Color.grey * 0.2f);
            
            var currentY = rect.y + 5f;
            
            // Search filter (top row)
            Rect searchLabelRect = new Rect(rect.x + 5f, currentY, 50f, 20f);
            Widgets.Label(searchLabelRect, "Search:");
            
            Rect searchRect = new Rect(searchLabelRect.xMax + 5f, currentY, 180f, 25f);
            string newFilter = Widgets.TextField(searchRect, searchFilter);
            if (newFilter != searchFilter)
            {
                searchFilter = newFilter;
                ApplyFilters();
            }

            // Category filter (top row, continued)
            Rect categoryLabelRect = new Rect(searchRect.xMax + 15f, currentY, 60f, 20f);
            Widgets.Label(categoryLabelRect, "Category:");
            
            Rect categoryRect = new Rect(categoryLabelRect.xMax + 5f, currentY, 120f, 25f);
            if (Widgets.ButtonText(categoryRect, selectedCategory.ToString()))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (SurgeryCategory category in Enum.GetValues(typeof(SurgeryCategory)))
                {
                    var count = allPawnSurgeries?.Count(s => category == SurgeryCategory.All || s.Category == category) ?? 0;
                    options.Add(new FloatMenuOption($"{category} ({count})", () => {
                        selectedCategory = category;
                        ApplyFilters();
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            // Mod source filter
            Rect modLabelRect = new Rect(categoryRect.xMax + 15f, currentY, 40f, 20f);
            Widgets.Label(modLabelRect, "Mod:");
            
            Rect modButtonRect = new Rect(modLabelRect.xMax + 5f, currentY, 150f, 25f);
            if (Widgets.ButtonText(modButtonRect, selectedModFilter))
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
                    
                    options.Add(new FloatMenuOption($"{modName} ({count})", () => {
                        selectedModFilter = modName;
                        ApplyFilters();
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            // Target Body Part filter
            Rect targetLabelRect = new Rect(modButtonRect.xMax + 15f, currentY, 50f, 20f);
            Widgets.Label(targetLabelRect, "Target:");
            
            Rect targetButtonRect = new Rect(targetLabelRect.xMax + 5f, currentY, 150f, 25f);
            string targetButtonLabel = selectedTargetPart?.LabelCap ?? "All";
            if (Widgets.ButtonText(targetButtonRect, targetButtonLabel))
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

            // Pawn selector
            Rect pawnLabelRect = new Rect(targetButtonRect.xMax + 15f, currentY, 45f, 20f);
            Widgets.Label(pawnLabelRect, "Pawn:");

            Rect pawnButtonRect = new Rect(pawnLabelRect.xMax + 5f, currentY, 150f, 25f);
            if (Widgets.ButtonText(pawnButtonRect, pawn.LabelShort))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                
                // Get all valid pawns (same logic as the O key shortcut)
                var allPawns = Find.Maps.SelectMany(m => m.mapPawns.AllPawns)
                    .Where(p => p.Faction == Faction.OfPlayer && 
                        (p.RaceProps.Humanlike || (p.RaceProps.Animal && p.health?.hediffSet != null)))
                    .OrderByDescending(p => p.RaceProps.Humanlike) // Humans first, then animals
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
                            // Switch to the new pawn
                            pawn = possiblePawn;
                            // Rebuild surgery list for new pawn
                            BuildFullSurgeryList();
                            PopulateAvailableTargets();
                            // Load any existing bills for the new pawn
                            LoadQueuedBills();
                            // Reapply filters to update the view
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

            // Second row - Quick Filters
            currentY += 30f;
            var quickFilterY = currentY;
            var quickFilterX = rect.x + 5f;
            var buttonHeight = 20f;
            var buttonSpacing = 5f;

            // Clear All button
            var clearButtonWidth = 80f;
            var clearButtonRect = new Rect(quickFilterX, quickFilterY, clearButtonWidth, buttonHeight);
            if (Widgets.ButtonText(clearButtonRect, "Clear All"))
            {
                searchFilter = "";
                selectedCategory = SurgeryCategory.All;
                selectedModFilter = "All";
                availabilityFilter = AvailabilityFilter.ShowAvailableOnly;
                selectedTargetPart = null;
                ApplyFilters();
            }
            
            // Availability filter dropdown
            var availabilityButtonWidth = 140f;
            var availabilityButtonRect = new Rect(clearButtonRect.xMax + buttonSpacing, quickFilterY, availabilityButtonWidth, buttonHeight);
            string availabilityButtonText = GetAvailabilityFilterText(availabilityFilter);
            if (Widgets.ButtonText(availabilityButtonRect, availabilityButtonText))
            {
                List<FloatMenuOption> availabilityOptions = new List<FloatMenuOption>();
                foreach (AvailabilityFilter filter in Enum.GetValues(typeof(AvailabilityFilter)))
                {
                    var count = GetFilteredCount(filter);
                    availabilityOptions.Add(new FloatMenuOption($"{GetAvailabilityFilterText(filter)} ({count})", () => {
                        availabilityFilter = filter;
                        ApplyFilters();
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(availabilityOptions));
            }

            // Implants quick-filter button
            var implantsButtonWidth = 80f;
            var implantsButtonRect = new Rect(availabilityButtonRect.xMax + buttonSpacing, quickFilterY, implantsButtonWidth, buttonHeight);
            if (Widgets.ButtonText(implantsButtonRect, "Implants"))
            {
                selectedCategory = SurgeryCategory.Implants;
                ApplyFilters();
            }

            // Queue Non Allowed checkbox (third row)
            currentY += 30f;
            var checkboxY = currentY;
            var checkboxRect = new Rect(rect.x + 5f, checkboxY, 200f, 20f);
            bool newAllowQueueingDisabled = allowQueueingDisabled;
            Widgets.CheckboxLabeled(checkboxRect, "Queue Non Allowed", ref newAllowQueueingDisabled);
            if (newAllowQueueingDisabled != allowQueueingDisabled)
            {
                allowQueueingDisabled = newAllowQueueingDisabled;
                // No need to refresh filters, this affects button behavior not filtering
            }
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
            Widgets.DrawBoxSolid(rect, Color.black * 0.1f);
            
            var rowHeight = 70f;
            var rowSpacing = 5f;
            var viewRect = new Rect(0f, 0f, rect.width - 20f, (rowHeight + rowSpacing) * filteredSurgeries.Count);
            Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);

            float y = 0f;
            for (int i = 0; i < filteredSurgeries.Count; i++)
            {
                var surgery = filteredSurgeries[i];
                var surgeryRect = new Rect(5f, y, viewRect.width - 10f, rowHeight);
                
                DrawSurgeryOption(surgeryRect, surgery, i);
                y += rowHeight + rowSpacing;
            }

            Widgets.EndScrollView();
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
            if (!string.IsNullOrEmpty(searchFilter) && 
                !surgery.Label.ToLower().Contains(searchFilter.ToLower()) &&
                (surgery.Recipe?.description?.ToLower().Contains(searchFilter.ToLower()) == false))
            {
                return false;
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
            
            // Preset dropdown
            var dropdownRect = new Rect(saveButtonRect.xMax + 10f, rect.y, 150f, rect.height);
            if (Widgets.ButtonText(dropdownRect, selectedPreset))
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
            
            // Add saved presets
            foreach (var presetName in surgeryPresets.Keys)
            {
                var name = presetName; // Capture for closure
                options.Add(new FloatMenuOption($"{name} ({surgeryPresets[name].Count} surgeries)", () => {
                    selectedPreset = name;
                }));
            }
            
            if (surgeryPresets.Count == 0)
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
                
                surgeryPresets[name] = presetItems;
                Messages.Message($"Preset '{name}' saved with {presetItems.Count} surgeries.", MessageTypeDefOf.PositiveEvent);
                
                Log.Message($"Corvus Surgery UI: Saved preset '{name}' with {presetItems.Count} surgeries");
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
                if (!surgeryPresets.ContainsKey(name))
                {
                    Messages.Message("Preset not found.", MessageTypeDefOf.RejectInput);
                    return;
                }
                
                // Clear current bills
                if (thingForMedBills is IBillGiver billGiver && billGiver.BillStack != null)
                {
                    var billsToRemove = billGiver.BillStack.Bills.OfType<Bill_Medical>().ToList();
                    foreach (var bill in billsToRemove)
                    {
                        billGiver.BillStack.Delete(bill);
                    }
                }
                
                // Load preset bills
                var presetItems = surgeryPresets[name];
                int loadedCount = 0;
                
                foreach (var item in presetItems)
                {
                    var recipe = DefDatabase<RecipeDef>.GetNamedSilentFail(item.RecipeDefName);
                    if (recipe == null) continue;
                    
                    BodyPartRecord bodyPart = null;
                    if (!item.BodyPartLabel.NullOrEmpty())
                    {
                        bodyPart = FindBodyPart(item.BodyPartLabel);
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
                
                LoadQueuedBills(); // Refresh our local list
                Messages.Message($"Loaded preset '{name}': {loadedCount} surgeries.", MessageTypeDefOf.PositiveEvent);
                
                Log.Message($"Corvus Surgery UI: Loaded preset '{name}' with {loadedCount} surgeries");
            }
            catch (Exception ex)
            {
                Log.Error($"Corvus Surgery UI: Error loading preset '{name}': {ex}");
                Messages.Message("Error loading preset.", MessageTypeDefOf.RejectInput);
            }
        }

        private BodyPartRecord FindBodyPart(string bodyPartLabel)
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