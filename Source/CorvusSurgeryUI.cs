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
            
            Log.Message("Corvus Surgery UI: Initialized with Harmony patches");
        }
        
        // Method to clear caches - calls the method in Dialog class
        public static void ClearCaches()
        {
            Dialog_CorvusSurgeryPlanner.ClearStaticCaches();
        }
    }

    // Patch to ensure our GameComponent gets added
    [HarmonyPatch(typeof(Game), "InitNewGame")]
    public static class Game_InitNewGame_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Game __instance)
        {
            if (__instance.components.OfType<CorvusSurgeryGameComp>().Any()) return;
            
            __instance.components.Add(new CorvusSurgeryGameComp(__instance));
            Log.Message("Corvus Surgery UI: GameComponent added to new game");
        }
    }
    
    [HarmonyPatch(typeof(Game), "LoadGame")]
    public static class Game_LoadGame_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            var game = Current.Game;
            if (game?.components?.OfType<CorvusSurgeryGameComp>().Any() != true)
            {
                game?.components?.Add(new CorvusSurgeryGameComp(game));
                Log.Message("Corvus Surgery UI: GameComponent added to loaded game");
            }
        }
    }

    // Game state manager for global surgery caching
    public class CorvusSurgeryGameComp : GameComponent
    {
        private static List<RecipeDef> availableRecipesCache = new List<RecipeDef>();
        private static bool globalCacheBuilt = false;
        private static int lastResearchHash = 0;
        
        public CorvusSurgeryGameComp(Game game) : base() { }
        
        public override void GameComponentTick()
        {
            // Check every 5 seconds if research has changed
            if (Find.TickManager.TicksGame % 300 == 0)
            {
                CheckAndUpdateGlobalCache();
            }
        }
        
        public override void LoadedGame()
        {
            BuildGlobalSurgeryCache();
        }
        
        public override void StartedNewGame()
        {
            BuildGlobalSurgeryCache();
        }
        
        private void CheckAndUpdateGlobalCache()
        {
            var currentResearchHash = GetResearchHash();
            if (currentResearchHash != lastResearchHash)
            {
                Log.Message("Corvus Surgery UI: Research changed, rebuilding global cache");
                BuildGlobalSurgeryCache();
            }
        }
        
        private static int GetResearchHash()
        {
            // Create a hash based on completed research
            int hash = 0;
            foreach (var research in DefDatabase<ResearchProjectDef>.AllDefs)
            {
                if (research.IsFinished)
                {
                    hash ^= research.GetHashCode();
                }
            }
            return hash;
        }
        
        public static void BuildGlobalSurgeryCache()
        {
            availableRecipesCache.Clear();
            
            // Instead of trying to identify medical recipes ourselves, 
            // let's use RimWorld's built-in filtering for a sample human pawn
            var humanDef = ThingDefOf.Human;
            if (humanDef?.AllRecipes != null)
            {
                var potentialRecipes = humanDef.AllRecipes
                    .Where(r => IsPotentiallyMedical(r))
                    .ToList();
                
                Log.Message($"Corvus Surgery UI: Processing {potentialRecipes.Count} potentially medical recipes from human def (filtered from {DefDatabase<RecipeDef>.AllDefs.Count()} total)");
                
                // Log some examples of what we're processing
                var examples = potentialRecipes.Take(5);
                foreach (var example in examples)
                {
                    Log.Message($"Corvus Surgery UI: Processing recipe '{example.LabelCap}' (worker: {example.workerClass?.Name})");
                }
                
                int filteredCount = 0;
                foreach (var recipe in potentialRecipes)
                {
                    // Check research prerequisites
                    bool hasResearch = true;
                    
                    if (recipe.researchPrerequisite != null && !recipe.researchPrerequisite.IsFinished)
                    {
                        hasResearch = false;
                    }
                    
                    if (recipe.researchPrerequisites != null && recipe.researchPrerequisites.Any())
                    {
                        hasResearch = recipe.researchPrerequisites.All(prereq => prereq.IsFinished);
                    }
                    
                    if (hasResearch)
                    {
                        availableRecipesCache.Add(recipe);
                    }
                    else
                    {
                        filteredCount++;
                    }
                }
                
                globalCacheBuilt = true;
                lastResearchHash = GetResearchHash();
                
                Log.Message($"Corvus Surgery UI: Global cache built with {availableRecipesCache.Count} available recipes ({filteredCount} filtered out by research)");
                
                // Log some examples of what was filtered
                if (filteredCount > 0)
                {
                    var filteredExamples = potentialRecipes.Where(r => !availableRecipesCache.Contains(r)).Take(3);
                    foreach (var example in filteredExamples)
                    {
                        var missingResearch = example.researchPrerequisite?.LabelCap ?? "Multiple prerequisites";
                        Log.Message($"Corvus Surgery UI: Filtered out '{example.LabelCap}' (needs: {missingResearch})");
                    }
                }
            }
        }
        
        private static bool IsPotentiallyMedical(RecipeDef recipe)
        {
            // Ultra-conservative filtering - only the most obvious medical recipes
            
            // Only include recipes with very specific medical worker classes
            if (recipe.workerClass != null)
            {
                var workerName = recipe.workerClass.Name;
                if (workerName == "Recipe_Surgery" || 
                    workerName == "Recipe_InstallArtificialBodyPart" ||
                    workerName == "Recipe_InstallImplant" ||
                    workerName == "Recipe_RemoveBodyPart" ||
                    workerName == "Recipe_RemoveHediff" ||
                    workerName == "Recipe_AddHediff")
                {
                    return true;
                }
            }
            
            // Only include if it explicitly targets body parts AND adds/removes hediffs
            if ((recipe.appliedOnFixedBodyParts?.Any() == true || recipe.appliedOnFixedBodyPartGroups?.Any() == true) &&
                (recipe.addsHediff != null || recipe.removesHediff != null))
            {
                return true;
            }
            
            return false;
        }
        
        public static List<RecipeDef> GetAvailableRecipes()
        {
            if (!globalCacheBuilt)
            {
                BuildGlobalSurgeryCache();
            }
            
            return availableRecipesCache;
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
        private bool showOnlyAvailable = false;
        private bool showOnlyQueueable = true;
        private string selectedModFilter = "All";
        private List<string> availableMods;
        
        private List<SurgeryOptionCached> allPawnSurgeries; // Master list, built once
        private List<SurgeryOptionCached> filteredSurgeries; // The list that gets displayed
        private Thing thingForMedBills;

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
            
            BuildFullSurgeryList();
            PopulateAvailableMods();
            ApplyFilters();
        }

        public override Vector2 InitialSize => new Vector2(800f, 600f);

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

            // Filters section (expanded for better mod support)
            Rect filtersRect = new Rect(0f, 55f, inRect.width, 100f);
            DrawFilters(filtersRect);

            // Surgery list
            Rect surgeryListRect = new Rect(0f, 160f, inRect.width, inRect.height - 200f);
            DrawSurgeryList(surgeryListRect);
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

            // Second row - Quick Filters
            currentY += 30f;
            var quickFilterY = currentY;
            var quickFilterX = rect.x + 5f;
            var quickFilterWidth = 80f;
            var quickFilterSpacing = 85f;
            
            if (Widgets.ButtonText(new Rect(quickFilterX, quickFilterY, quickFilterWidth, 20f), "Clear All"))
            {
                searchFilter = "";
                selectedCategory = SurgeryCategory.All;
                selectedModFilter = "All";
                showOnlyAvailable = false;
                showOnlyQueueable = false;
                ApplyFilters();
            }
            
            if (Widgets.ButtonText(new Rect(quickFilterX + quickFilterSpacing, quickFilterY, quickFilterWidth, 20f), "Available"))
            {
                showOnlyAvailable = true;
                showOnlyQueueable = true;
                ApplyFilters();
            }
            
            if (Widgets.ButtonText(new Rect(quickFilterX + quickFilterSpacing * 2, quickFilterY, quickFilterWidth, 20f), "Implants"))
            {
                selectedCategory = SurgeryCategory.Implants;
                ApplyFilters();
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

        private void DrawSurgeryList(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, Color.black * 0.1f);
            
            var viewRect = new Rect(0f, 0f, rect.width - 20f, filteredSurgeries.Count * 60f);
            Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);

            float y = 0f;
            foreach (var surgery in filteredSurgeries)
            {
                DrawSurgeryOption(new Rect(5f, y, viewRect.width - 10f, 55f), surgery);
                y += 60f;
            }

            Widgets.EndScrollView();
        }

        private void DrawSurgeryOption(Rect rect, SurgeryOptionCached surgery)
        {
            // Lazy load expensive properties only when displaying
            if (string.IsNullOrEmpty(surgery.Requirements))
            {
                surgery.Requirements = GetRequirements(surgery.Recipe);
                surgery.ImplantWarning = GetImplantWarning(surgery.Recipe, surgery.BodyPart);
                surgery.Tooltip = GetDetailedTooltip(surgery.Recipe, surgery.BodyPart);
            }
            
            // Background with better visual feedback
            Color bgColor = Color.black * 0.1f;
            Widgets.DrawBoxSolid(rect, bgColor);
            
            // Outline for better definition
            Widgets.DrawBox(rect, 1);

            // Left column - Surgery info
            var leftColumnWidth = rect.width * 0.45f;
            
            // Surgery name with mod badge
            Rect nameRect = new Rect(rect.x + 5f, rect.y + 2f, leftColumnWidth - 10f, 20f);
            Widgets.Label(nameRect, surgery.Label);

            // Mod source badge
            var modName = surgery.Recipe?.modContentPack?.Name ?? "Core";
            if (modName != "Core")
            {
                Rect modBadgeRect = new Rect(nameRect.x, nameRect.yMax, leftColumnWidth - 10f, 15f);
                GUI.color = Color.cyan;
                Text.Font = GameFont.Tiny;
                Widgets.Label(modBadgeRect, $"[{modName}]");
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
            }

            // Description
            Rect descRect = new Rect(nameRect.x, nameRect.yMax + (modName != "Core" ? 17f : 2f), leftColumnWidth - 10f, 25f);
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            Widgets.Label(descRect, surgery.Description);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            // Middle column - Category and requirements
            var middleColumnX = rect.x + leftColumnWidth;
            var middleColumnWidth = rect.width * 0.3f;
            
            Rect categoryRect = new Rect(middleColumnX, rect.y + 2f, middleColumnWidth, 20f);
            GUI.color = GetCategoryColor(surgery.Category);
            Widgets.Label(categoryRect, surgery.Category.ToString());
            GUI.color = Color.white;

            Rect reqRect = new Rect(middleColumnX, categoryRect.yMax + 2f, middleColumnWidth, 30f);
            Text.Font = GameFont.Tiny;
            Widgets.Label(reqRect, surgery.Requirements);
            Text.Font = GameFont.Small;

            // Body part info
            if (surgery.BodyPart != null)
            {
                Rect bodyPartRect = new Rect(middleColumnX, reqRect.yMax + 2f, middleColumnWidth, 15f);
                Text.Font = GameFont.Tiny;
                GUI.color = Color.gray;
                Widgets.Label(bodyPartRect, $"Target: {surgery.BodyPart.Label}");
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }

            // Right column - Warnings and button
            var rightColumnX = middleColumnX + middleColumnWidth;
            var rightColumnWidth = rect.width * 0.25f;

            // Existing implant warning
            if (!string.IsNullOrEmpty(surgery.ImplantWarning))
            {
                Rect warningRect = new Rect(rightColumnX, rect.y + 5f, rightColumnWidth, 30f);
                GUI.color = Color.yellow;
                Text.Font = GameFont.Tiny;
                Widgets.Label(warningRect, "⚠ " + surgery.ImplantWarning);
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
            }

            // Add to queue button
            Rect buttonRect = new Rect(rightColumnX, rect.yMax - 28f, rightColumnWidth - 5f, 25f);
            bool canQueue = true; // All options from vanilla are considered available and queueable
            
            GUI.enabled = canQueue;
            var buttonText = canQueue ? "Queue" : "Unavailable";
            if (Widgets.ButtonText(buttonRect, buttonText) && canQueue)
            {
                QueueSurgery(surgery.Recipe, surgery.BodyPart);
            }
            GUI.enabled = true;

            // Status indicator
            Rect statusRect = new Rect(rect.x + 2f, rect.y + 2f, 8f, 8f);
            var statusColor = canQueue ? Color.green : Color.red;
            Widgets.DrawBoxSolid(statusRect, statusColor);

            // Tooltip with comprehensive info
            if (Mouse.IsOver(rect))
            {
                var tooltipText = $"{surgery.Tooltip}\n\nMod: {modName}\nStatus: {(canQueue ? "Available" : "Not Available")}";
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
                if (!recipe.AvailableNow) continue;
                
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
            if (option == null || option.Disabled || option.Label.NullOrEmpty() || option.action == null)
            {
                return;
            }
            
            allPawnSurgeries.Add(new SurgeryOptionCached
            {
                Label = option.Label,
                Description = recipe.description,
                Category = CategorizeRecipe(recipe),
                IsAvailable = true, // If we got an option, it's available
                IsQueueable = true, // And queueable
                Action = option.action,
                Recipe = recipe,
                BodyPart = part
            });
        }

        private SurgeryCategory CategorizeRecipe(RecipeDef recipe)
        {
            if (recipe == null) return SurgeryCategory.Medical;
            return recipeCategoryCache.GetValueOrDefault(recipe, SurgeryCategory.Medical);
        }
        
        private void QueueSurgery(RecipeDef recipe, BodyPartRecord part)
        {
            var bill = recipe.MakeNewBill();
            if (part != null && bill is Bill_Medical medicalBill)
            {
                medicalBill.Part = part;
            }
            pawn.BillStack.AddBill(bill);
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

            // This check is now driven by the "Available" and "Clear All" buttons.
            if (showOnlyAvailable)
            {
                 var missingIngredients = surgery.Recipe.PotentiallyMissingIngredients(null, pawn.MapHeld);
                 if (missingIngredients != null && missingIngredients.Any())
                 {
                     return false;
                 }
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

        private string GetImplantWarning(RecipeDef recipe, BodyPartRecord part)
        {
            // Check if this would replace an existing implant
            if (recipe.addsHediff == null || part == null) return "";
            
            var existingImplant = pawn.health.hediffSet.hediffs
                .FirstOrDefault(h => h.Part == part && h.def.isBad == false);
            
            if (existingImplant != null)
            {
                return $"Replaces {existingImplant.def.label}";
            }
            
            return "";
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
            
            Log.Message("Corvus Surgery UI: Caches cleared and will be rebuilt");
        }
    }

    // Data structure for cached surgery options
    public class SurgeryOptionCached
    {
        public string Label;
        public string Description;
        public SurgeryCategory Category;
        public bool IsAvailable;
        public bool IsQueueable;
        public string Requirements;
        public string ImplantWarning;
        public string Tooltip;
        public Action Action;
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
}