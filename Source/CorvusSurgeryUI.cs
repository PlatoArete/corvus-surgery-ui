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
                TooltipHandler.TipRegion(planButtonRect, "Open enhanced surgery planning interface with filtering and mod compatibility");
            }
            catch (Exception ex)
            {
                Log.Error($"Corvus Surgery UI: Error in operations tab patch: {ex}");
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
        private AvailabilityFilter availabilityFilter = AvailabilityFilter.ShowAll;
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

        public override Vector2 InitialSize => new Vector2(950f, 600f);

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
            
            // Subtitle
            var subtitleRect = new Rect(0f, 25f, inRect.width, 25f);
            GUI.color = Color.gray;
            Widgets.Label(subtitleRect, "Compatible with all surgery mods - Filter and organize your surgical options");
            GUI.color = Color.white;

            // Filters section
            Rect filtersRect = new Rect(0f, 55f, inRect.width, 130f); // Increased height for checkbox
            DrawFilters(filtersRect);

            // Split the remaining area: left for available surgeries, right for queue
            var remainingHeight = inRect.height - 190f; // Increased from 160f to account for taller filters
            var queueWidth = 400f; // Increased width for queue (was 300f)
            var surgeryListWidth = inRect.width - queueWidth - 10f; // Rest for surgeries

            // Available Surgeries (left side)
            Rect surgeryListRect = new Rect(0f, 190f, surgeryListWidth, remainingHeight); // Increased Y from 160f
            DrawSurgeryList(surgeryListRect);

            // Surgery Queue (right side)
            Rect queuedBillsRect = new Rect(surgeryListWidth + 10f, 190f, queueWidth, remainingHeight); // Increased Y from 160f
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
                availabilityFilter = AvailabilityFilter.ShowAll;
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
            // Section header
            Text.Font = GameFont.Medium;
            var headerRect = new Rect(rect.x, rect.y, rect.width - 160f, 25f);
            Widgets.Label(headerRect, $"Surgery Queue ({queuedBills.Count} bills)");
            Text.Font = GameFont.Small;
            
            // Bulk action buttons
            if (queuedBills.Count > 0)
            {
                var suspendAllRect = new Rect(rect.xMax - 155f, rect.y, 70f, 20f);
                if (Widgets.ButtonText(suspendAllRect, "Suspend All"))
                {
                    SuspendAllBills();
                }
                
                var activateAllRect = new Rect(rect.xMax - 80f, rect.y, 75f, 20f);
                if (Widgets.ButtonText(activateAllRect, "Activate All"))
                {
                    ActivateAllBills();
                }
            }
            
            // Queue area
            var queueRect = new Rect(rect.x, rect.y + 30f, rect.width, rect.height - 30f);
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
                    
                    // Add the bill to the stack
                    billGiver.BillStack.AddBill(bill);
                    
                    Log.Message($"Corvus Surgery UI: Force-queued '{recipe.LabelCap}' on {part?.LabelCap ?? "whole body"}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Corvus Surgery UI: Error force-queueing surgery '{recipe?.defName}': {ex}");
            }
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
}