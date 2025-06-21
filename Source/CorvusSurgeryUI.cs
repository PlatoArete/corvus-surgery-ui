using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace CorvusSurgeryUI
{
    [StaticConstructorOnStartup]
    public static class CorvusSurgeryUIMod
    {
        static CorvusSurgeryUIMod()
        {
            var harmony = new Harmony("corvus.surgery.ui");
            harmony.PatchAll();
            Log.Message("Corvus Surgery UI: Enhanced medical interface loaded successfully");
            Log.Message("Corvus Surgery UI: Harmony patches applied");
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
                if (pawn == null) return;
                
                // Only show for pawns that can have surgery
                bool canHaveSurgery = pawn.RaceProps.Humanlike || (pawn.RaceProps.Animal && pawn.health?.hediffSet != null);
                if (!canHaveSurgery) return;
                
                // Create the Surgery Planner button positioned within the operations tab area
                var buttonRect = new Rect(leftRect.x + 10f, curY + 35f, 150f, 25f);
                
                if (Widgets.ButtonText(buttonRect, "Surgery Planner"))
                {
                    var dialog = new Dialog_CorvusSurgeryPlanner(pawn);
                    Find.WindowStack.Add(dialog);
                }
                
                // Add tooltip
                TooltipHandler.TipRegion(buttonRect, "Open enhanced surgery planning interface with filtering and mod compatibility");
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
        private List<SurgeryOptionCached> cachedSurgeries;

        public Dialog_CorvusSurgeryPlanner(Pawn pawn)
        {
            this.pawn = pawn;
            this.forcePause = true;
            this.doCloseX = true;
            this.absorbInputAroundWindow = true;
            RefreshSurgeryCache();
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
            var surgeryCount = cachedSurgeries?.Count ?? 0;
            Widgets.Label(titleRect, $"Surgery Planner - {pawn.LabelShort} ({surgeryCount} surgeries available)");
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
                RefreshSurgeryCache();
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
                    var count = cachedSurgeries?.Count(s => category == SurgeryCategory.All || s.Category == category) ?? 0;
                    options.Add(new FloatMenuOption($"{category} ({count})", () => {
                        selectedCategory = category;
                        RefreshSurgeryCache();
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            // Mod source filter (top row, continued)
            Rect modLabelRect = new Rect(categoryRect.xMax + 15f, currentY, 70f, 20f);
            Widgets.Label(modLabelRect, "Show source:");
            
            // Second row - Toggle filters
            currentY += 30f;
            Rect toggleRect1 = new Rect(rect.x + 5f, currentY, 180f, 25f);
            Widgets.CheckboxLabeled(toggleRect1, "Only available now", ref showOnlyAvailable);
            if (GUI.changed)
            {
                RefreshSurgeryCache();
                GUI.changed = false;
            }

            Rect toggleRect2 = new Rect(toggleRect1.xMax + 10f, currentY, 180f, 25f);
            Widgets.CheckboxLabeled(toggleRect2, "Only queueable", ref showOnlyQueueable);
            if (GUI.changed)
            {
                RefreshSurgeryCache();
                GUI.changed = false;
            }
            
            // Show mod compatibility info
            Rect modInfoRect = new Rect(toggleRect2.xMax + 10f, currentY, 200f, 25f);
            var modCount = GetUniqueModCount();
            GUI.color = Color.cyan;
            Widgets.Label(modInfoRect, $"From {modCount} mod(s)");
            GUI.color = Color.white;

            // Third row - Quick filters
            currentY += 30f;
            var quickFilterY = currentY;
            var quickFilterX = rect.x + 5f;
            var quickFilterWidth = 80f;
            var quickFilterSpacing = 85f;
            
            if (Widgets.ButtonText(new Rect(quickFilterX, quickFilterY, quickFilterWidth, 20f), "Clear All"))
            {
                searchFilter = "";
                selectedCategory = SurgeryCategory.All;
                showOnlyAvailable = false;
                showOnlyQueueable = false;
                RefreshSurgeryCache();
            }
            
            if (Widgets.ButtonText(new Rect(quickFilterX + quickFilterSpacing, quickFilterY, quickFilterWidth, 20f), "Available"))
            {
                showOnlyAvailable = true;
                showOnlyQueueable = true;
                RefreshSurgeryCache();
            }
            
            if (Widgets.ButtonText(new Rect(quickFilterX + quickFilterSpacing * 2, quickFilterY, quickFilterWidth, 20f), "Implants"))
            {
                selectedCategory = SurgeryCategory.Implants;
                RefreshSurgeryCache();
            }
        }
        
        private int GetUniqueModCount()
        {
            if (cachedSurgeries == null) return 0;
            return cachedSurgeries.Select(s => s.Recipe?.modContentPack?.Name ?? "Core").Distinct().Count();
        }

        private void DrawSurgeryList(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, Color.black * 0.1f);
            
            var viewRect = new Rect(0f, 0f, rect.width - 20f, cachedSurgeries.Count * 60f);
            Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);

            float y = 0f;
            foreach (var surgery in cachedSurgeries)
            {
                DrawSurgeryOption(new Rect(5f, y, viewRect.width - 10f, 55f), surgery);
                y += 60f;
            }

            Widgets.EndScrollView();
        }

        private void DrawSurgeryOption(Rect rect, SurgeryOptionCached surgery)
        {
            // Background with better visual feedback
            Color bgColor = surgery.IsAvailable ? 
                (surgery.IsQueueable ? Color.green * 0.1f : Color.yellow * 0.1f) : 
                Color.red * 0.1f;
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
            bool canQueue = surgery.IsAvailable && surgery.IsQueueable;
            
            GUI.enabled = canQueue;
            var buttonText = canQueue ? "Queue" : (surgery.IsAvailable ? "Cannot Queue" : "Unavailable");
            if (Widgets.ButtonText(buttonRect, buttonText) && canQueue)
            {
                QueueSurgery(surgery.Recipe, surgery.BodyPart);
            }
            GUI.enabled = true;

            // Status indicator
            Rect statusRect = new Rect(rect.x + 2f, rect.y + 2f, 8f, 8f);
            var statusColor = surgery.IsAvailable ? (surgery.IsQueueable ? Color.green : Color.yellow) : Color.red;
            Widgets.DrawBoxSolid(statusRect, statusColor);

            // Tooltip with comprehensive info
            if (Mouse.IsOver(rect))
            {
                var tooltipText = $"{surgery.Tooltip}\n\nMod: {modName}\nStatus: {(surgery.IsAvailable ? "Available" : "Not Available")}\nQueueable: {(surgery.IsQueueable ? "Yes" : "No")}";
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

        private void RefreshSurgeryCache()
        {
            cachedSurgeries = new List<SurgeryOptionCached>();
            
            // Get all available surgeries for this pawn
            var surgeryOptions = GetAllSurgeryOptions();
            
            foreach (var option in surgeryOptions)
            {
                if (PassesFilters(option))
                {
                    cachedSurgeries.Add(option);
                }
            }

            // Sort by category, then by name
            cachedSurgeries = cachedSurgeries.OrderBy(s => s.Category).ThenBy(s => s.Label).ToList();
        }

        private List<SurgeryOptionCached> GetAllSurgeryOptions()
        {
            var options = new List<SurgeryOptionCached>();
            
            // Get all recipes available for this pawn
            var allRecipes = pawn.def.AllRecipes.ToList();
            
            // First, handle non-targeted surgeries (like drug administration)
            var nonTargetedRecipes = allRecipes
                .Where(r => IsNonTargetedSurgery(r))
                .Where(r => r.AvailableOnNow(pawn))
                .ToList();
                
            foreach (var recipe in nonTargetedRecipes)
            {
                var cached = new SurgeryOptionCached
                {
                    Label = recipe.LabelCap,
                    Description = recipe.description ?? "",
                    Category = CategorizeRecipe(recipe),
                    IsAvailable = CanPerformSurgery(recipe, null),
                    IsQueueable = CanQueueSurgery(recipe, null),
                    Requirements = GetRequirements(recipe),
                    ImplantWarning = "",
                    Tooltip = GetDetailedTooltip(recipe, null),
                    Action = () => QueueSurgery(recipe, null),
                    Recipe = recipe,
                    BodyPart = null
                };
                options.Add(cached);
            }
            
            // Then handle targeted surgeries for specific body parts
            foreach (var part in pawn.RaceProps.body.AllParts)
            {
                // Get recipes that are specifically available for this body part (excluding non-targeted ones)
                var availableRecipes = allRecipes
                    .Where(r => !IsNonTargetedSurgery(r))
                    .Where(r => r.AvailableOnNow(pawn, part))
                    .Where(r => IsRecipeValidForBodyPart(r, part))
                    .ToList();

                foreach (var recipe in availableRecipes)
                {
                    var cached = new SurgeryOptionCached
                    {
                        Label = recipe.LabelCap,
                        Description = recipe.description ?? "",
                        Category = CategorizeRecipe(recipe),
                        IsAvailable = CanPerformSurgery(recipe, part),
                        IsQueueable = CanQueueSurgery(recipe, part),
                        Requirements = GetRequirements(recipe),
                        ImplantWarning = GetImplantWarning(recipe, part),
                        Tooltip = GetDetailedTooltip(recipe, part),
                        Action = () => QueueSurgery(recipe, part),
                        Recipe = recipe,
                        BodyPart = part
                    };
                    options.Add(cached);
                }
            }

            return options;
        }

        private bool PassesFilters(SurgeryOptionCached surgery)
        {
            // Search filter
            if (!string.IsNullOrEmpty(searchFilter) && 
                !surgery.Label.ToLower().Contains(searchFilter.ToLower()) &&
                !surgery.Description.ToLower().Contains(searchFilter.ToLower()))
            {
                return false;
            }

            // Category filter
            if (selectedCategory != SurgeryCategory.All && surgery.Category != selectedCategory)
            {
                return false;
            }

            // Availability filter
            if (showOnlyAvailable && !surgery.IsAvailable)
            {
                return false;
            }

            // Queueable filter
            if (showOnlyQueueable && !surgery.IsQueueable)
            {
                return false;
            }

            return true;
        }

        private SurgeryCategory CategorizeRecipe(RecipeDef recipe)
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

        private bool CanPerformSurgery(RecipeDef recipe, BodyPartRecord part)
        {
            // Check research prerequisites
            if (recipe.researchPrerequisite != null && !recipe.researchPrerequisite.IsFinished)
            {
                return false;
            }

            // Check if any of the research prerequisites are met
            if (recipe.researchPrerequisites != null && recipe.researchPrerequisites.Any())
            {
                bool hasAllPrereqs = recipe.researchPrerequisites.All(prereq => prereq.IsFinished);
                if (!hasAllPrereqs)
                {
                    return false;
                }
            }

            // Check if we have the required ingredients/materials
            if (recipe.ingredients != null && recipe.ingredients.Any())
            {
                foreach (var ingredient in recipe.ingredients)
                {
                    // Check if we have enough of this ingredient available in the colony
                    var availableCount = 0;
                    
                    // Count available items in stockpiles, storage, etc.
                    foreach (var thing in pawn.Map?.listerThings?.AllThings?.Where(t => ingredient.filter.Allows(t)) ?? Enumerable.Empty<Thing>())
                    {
                        availableCount += thing.stackCount;
                    }
                    
                    // Check if we have enough
                    if (availableCount < ingredient.GetBaseCount())
                    {
                        return false;
                    }
                }
            }

            // Check facility requirements (like research bench, hospital bed, etc.)
            if (recipe.requiredGiverWorkType != null)
            {
                // Check if we have colonists capable of doing this work type
                bool hasCapablePawn = pawn.Map?.mapPawns?.FreeColonists?.Any(p => 
                    !p.WorkTypeIsDisabled(recipe.requiredGiverWorkType) && 
                    !p.Downed && 
                    p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)) ?? false;
                    
                if (!hasCapablePawn)
                {
                    return false;
                }
            }

            // Check skill requirements
            if (recipe.skillRequirements != null && recipe.skillRequirements.Any())
            {
                foreach (var skillReq in recipe.skillRequirements)
                {
                    // Check if any colonist has the required skill level
                    bool hasSkillfulPawn = pawn.Map?.mapPawns?.FreeColonists?.Any(p => 
                        p.skills?.GetSkill(skillReq.skill)?.Level >= skillReq.minLevel &&
                        !p.Downed &&
                        !p.WorkTypeIsDisabled(recipe.requiredGiverWorkType ?? WorkTypeDefOf.Doctor)) ?? false;
                        
                    if (!hasSkillfulPawn)
                    {
                        return false;
                    }
                }
            }

            // Check if the basic recipe is available
            if (part != null)
                return recipe.AvailableOnNow(pawn, part);
            else
                return recipe.AvailableOnNow(pawn);
        }

        private bool CanQueueSurgery(RecipeDef recipe, BodyPartRecord part)
        {
            // Additional checks for whether this can be queued
            return true; // Simplified for example
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

        private void QueueSurgery(RecipeDef recipe, BodyPartRecord part)
        {
            // Queue the surgery - this would integrate with the normal bill system
            var bill = recipe.MakeNewBill();
            if (part != null && bill is Bill_Medical medicalBill)
            {
                medicalBill.Part = part;
            }
            pawn.BillStack.AddBill(bill);
        }

        private bool IsRecipeValidForBodyPart(RecipeDef recipe, BodyPartRecord part)
        {
            // Check if the recipe has specific body part requirements
            if (recipe.appliedOnFixedBodyParts != null && recipe.appliedOnFixedBodyParts.Any())
            {
                // Recipe specifies which body parts it can be applied to
                return recipe.appliedOnFixedBodyParts.Contains(part.def);
            }

            // Check if the recipe has body part group requirements
            if (recipe.appliedOnFixedBodyPartGroups != null && recipe.appliedOnFixedBodyPartGroups.Any())
            {
                // Recipe specifies which body part groups it can be applied to
                return recipe.appliedOnFixedBodyPartGroups.Any(group => part.groups.Contains(group));
            }

            // For recipes that add hediffs, check if the hediff makes sense for this body part
            if (recipe.addsHediff != null)
            {
                // For implants and prosthetics, be more restrictive
                if (recipe.workerClass?.Name?.Contains("InstallArtificial") == true ||
                    recipe.workerClass?.Name?.Contains("InstallNatural") == true ||
                    recipe.defName.ToLower().Contains("install"))
                {
                    // This is an installation recipe, check if it's meant for this body part type
                    var recipeName = recipe.defName.ToLower();
                    var partName = part.def.defName.ToLower();
                    
                    // Basic body part matching
                    if (recipeName.Contains("arm") && !partName.Contains("arm") && !partName.Contains("shoulder") && !partName.Contains("hand"))
                        return false;
                    if (recipeName.Contains("leg") && !partName.Contains("leg") && !partName.Contains("foot") && !partName.Contains("toe"))
                        return false;
                    if (recipeName.Contains("eye") && !partName.Contains("eye"))
                        return false;
                    if (recipeName.Contains("nose") && !partName.Contains("nose"))
                        return false;
                    if (recipeName.Contains("ear") && !partName.Contains("ear"))
                        return false;
                    if (recipeName.Contains("jaw") && !partName.Contains("jaw"))
                        return false;
                    if (recipeName.Contains("spine") && !partName.Contains("spine"))
                        return false;
                    if (recipeName.Contains("heart") && !partName.Contains("heart"))
                        return false;
                    if (recipeName.Contains("lung") && !partName.Contains("lung"))
                        return false;
                    if (recipeName.Contains("kidney") && !partName.Contains("kidney"))
                        return false;
                    if (recipeName.Contains("liver") && !partName.Contains("liver"))
                        return false;
                    if (recipeName.Contains("stomach") && !partName.Contains("stomach"))
                        return false;
                }
            }

            // For removal recipes, check if there's actually something to remove
            if (recipe.removesHediff != null)
            {
                return pawn.health.hediffSet.GetFirstHediffOfDef(recipe.removesHediff) != null;
            }

            // Default to allowing the recipe if no specific restrictions found
            return true;
        }

        private bool IsNonTargetedSurgery(RecipeDef recipe)
        {
            // Check if this is a drug administration or other non-targeted surgery
            if (recipe.defName.ToLower().Contains("administer"))
                return true;
                
            // Check if the recipe doesn't target specific body parts
            if (recipe.targetsBodyPart == false)
                return true;
                
            // Check if it's a general treatment that doesn't require specific body parts
            if (recipe.workerClass?.Name?.Contains("Administer") == true)
                return true;
                
            // Check for other patterns that indicate non-targeted surgeries
            var recipeName = recipe.defName.ToLower();
            if (recipeName.Contains("treat") && !recipeName.Contains("install") && !recipeName.Contains("remove"))
                return true;
                
            return false;
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