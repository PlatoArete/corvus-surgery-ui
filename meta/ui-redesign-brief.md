# Corvus Surgery UI Redesign Brief

## Purpose

This document defines the intended visual direction and implementation strategy for the `corvus-surgery-ui` mod UI. It exists so the design goals survive beyond any one coding session.

The aim is not to imitate vanilla RimWorld and not to copy the Isekai mod's fantasy presentation. The aim is to build a cleaner, more distinctive, more usable surgical planning interface with a clear Corvus identity.

This brief should be read alongside the approved Corvus production-planner redesign screenshot, which serves as the most concrete visual benchmark for the intended tone and component language.

## Visual Benchmark

The redesigned production-planner UI is the canonical reference for the Corvus feel.

Use it as the benchmark for:

- shell framing
- panel treatment
- section headers
- compact filter controls
- dense row hierarchy
- right-column operational layout
- restrained accent usage
- compact square local actions

Do not copy it literally where the surgery workflow needs different interaction patterns.

Translate it instead:

- production recipe list becomes surgery list language
- production bills column becomes surgery queue language
- production filter rail becomes surgical filter rail
- production shell polish becomes planner and dialog shell polish
- the surgery visual planner should inherit the same shell language while remaining anatomy-first

## Design Intent

Target feeling:

- industrial medical operations console
- hardened clinical planning software
- discreet futuristic surgical interface
- quiet, disciplined, information-first

Avoid:

- glossy holograms
- neon-heavy cyberpunk UI
- decorative fantasy framing
- medical melodrama
- loud glow everywhere

Pursue:

- matte charcoal and graphite surfaces
- gunmetal panel divisions
- cool silver text
- one primary accent color
- restrained interaction feedback
- strong information hierarchy

## Brand Translation

The Corvus logo suggests a UI philosophy of "mostly dark, mostly silent, one illuminated signal."

That means:

- most of the interface should remain visually quiet
- structure should come from spacing, borders, panel depth, and hierarchy
- only important states should illuminate
- the accent color should be rare enough to retain meaning

For this mod specifically, the "signal" should represent surgical readiness, selected context, and deliberate user action.

## Visual System

### Palette

Core palette:

- background: oil black / deep graphite
- surface: gunmetal / dark steel
- surface-alt: slightly lighter graphite for nested panels
- text-primary: cool silver
- text-secondary: muted steel gray
- border: low-contrast blue-gray or steel-gray
- accent-primary: cyan-blue
- accent-warning: muted amber
- accent-danger: restrained red, only when necessary
- accent-medical-info: optional pale ice-blue for passive anatomical highlighting

Suggested roles:

- cyan: selection, active controls, focus, ready actions
- amber: warning, blocked requirements, force-queued status, partial health concerns
- red: destructive actions and hard invalid states only
- gray: passive metadata, labels, inactive controls

### Texture and Finish

Use subtle materials, not illustrative backgrounds:

- faint brushed or clinical-monitor texture
- soft graphite gradients
- low-contrast panel separation
- occasional inset or bevel treatment

The body diagram area can be cleaner and flatter than the list areas so anatomy remains readable.

Do not place busy background motifs behind dense content areas or over the body diagram.

The benchmark screenshot confirms that the finish should feel:

- matte rather than glossy
- procedural rather than art-heavy
- sharp-edged and disciplined rather than soft and ornamental

### Typography

Desired character:

- condensed sans feel
- technical but readable
- compact where possible

Practical constraint:

- RimWorld text rendering is limited, so typography will mostly be achieved through spacing, weight, capitalization, and scale rather than custom font systems

## UX Principles

### Priority Order

1. scan speed
2. interaction clarity
3. surgical context awareness
4. visual cohesion
5. motion polish

### Interface Philosophy

- surgery lists should be easier to scan than they are now
- queue actions should feel deliberate and compact
- controls should be consistent in size and behavior
- the selected pawn and target body part should always stay legible
- icon-only actions are acceptable when meaning is obvious and local
- text labels should remain where ambiguity would otherwise increase

### Quiet-by-Default

Default state should be subdued.

Only these should visibly "wake up":

- hovered actionable controls
- selected tabs
- active filters
- selected body parts
- queue and apply actions
- important warnings

## Current UI Assessment

The current surgery planner is functionally strong:

- broad surgery compatibility via existing RimWorld logic
- useful filters and search
- strong queue-management utility
- valuable visual planner concept
- meaningful preset workflow

Main weaknesses are presentation, hierarchy, and consistency rather than capability:

- controls are visually chunky
- the main window reads like a utility tool assembled from vanilla widgets
- tabs do not yet feel like one coherent system
- filter controls lack stronger grouping and active-state emphasis
- queue rows and surgery rows are heavier than they need to be
- small dialogs use default RimWorld window language rather than the Corvus visual system

## Primary UI Surfaces

This mod is not one screen. The brief must explicitly cover all of these:

- main planner shell with three tabs
- overview tab
- visual planner tab
- presets tab
- queue panel reused across tabs
- body-part inspector / action surface
- preset and import/export dialogs

These surfaces should feel like one product family, not one redesigned window plus several leftover vanilla dialogs.

## Target UI Changes

### Window-Level

- replace the generic flat window presentation with a custom dark industrial background
- make the tab strip feel integrated with the window instead of sitting on top of it
- separate major regions more clearly:
  - filters and context controls
  - main working surface
  - queue / preset management
- use subtle panel depth or insets instead of heavy boxes everywhere

The production-planner benchmark suggests a useful shell model:

- darker outer shell
- slightly lighter section panels
- thin low-contrast dividers
- compact top-right utility area

### Tab Language

- tabs should read as mode switches within one surgical workstation
- active tab should use the accent color sparingly but clearly
- inactive tabs should remain quiet and low contrast
- tab labels should be short, consistent, and slightly more deliberate than vanilla tabs

### Overview Tab

Move toward an operator-console layout with:

- a compact filter rail at the top
- a quieter but clearer info strip for selected pawn and result counts
- denser surgery rows with stronger label hierarchy
- a more deliberate right-side queue column

Likely direction:

- convert large text buttons into compact action controls
- visually separate surgery metadata from actionable controls
- make status signaling readable at a glance without flooding the row with color

This tab should be the closest structural match to the benchmark screenshot.

### Visual Planner Tab

This is the hero surface and should carry the strongest identity.

Current implemented direction:

- the redesign now uses an inline inspector beside the anatomy surface instead of a floating dropdown
- body-part interaction remains logic-compatible with the existing surgery lookup
- the anatomy rendering is still rect-based under the hood, so a deeper rendering pass remains optional future work

Goals:

- make the body diagram feel like a surgical planning surface rather than a debug overlay
- frame the anatomy area with a cleaner and more intentional panel treatment
- make selected body part state unmistakable
- make body-part actions feel integrated with the planner rather than like a default popup

Guidelines:

- keep anatomy readability above decorative style
- use color to indicate state, but keep the palette restrained
- distinguish natural, damaged, missing, and artificial parts clearly
- keep click feedback precise and mechanical, not playful
- prefer an inline inspector or attached contextual panel over a loose vanilla-style float menu where practical

Consistency rule:

- the tab should feel like it belongs to the same product as the benchmark screenshot
- it should not feel like an entirely different themed screen

### Presets Tab

This tab should feel like template deployment, not a side utility.

Goals:

- emphasize the workflow of selecting eligible pawns and applying repeatable surgery packages
- make the selected pawn card and its queue feel more connected
- keep bulk apply actions visually secondary to safety and clarity

Likely direction:

- cleaner pawn cards
- compact preset dropdowns and apply controls
- clearer validation state for invalid presets

### Filter Bar

- visually group related controls
- separate search, categorization, and targeting controls
- show active filter state more clearly
- reduce bulk of dropdown controls where possible
- make clear/reset secondary, not dominant

### Surgery Rows

Move toward a compact strip layout with:

- stronger title hierarchy
- quieter metadata lines
- concise requirement/status area
- a compact action cluster on the right

Likely direction:

- replace oversized `Add` style actions with compact buttons or chips
- keep discoverability through tooltips and supporting text
- make status and requirements visually distinct from action controls

### Queue Rows

The queue is one of the most important surfaces and should feel operational.

Use a tighter action language:

- compact suspend/activate control
- compact remove control
- clearer drag affordance
- stronger ordering readability
- distinct but quiet styling for suspended bills

The queue row should feel like an operational control strip, not a form row.

The production-planner screenshot is the direct reference for:

- queue density
- action placement
- subdued row framing
- quiet but readable control grouping

### Small Dialogs

Dialogs that currently use default RimWorld layout should inherit the same visual system:

- preset naming
- import from saves
- export preset
- import preset file
- consolidated import
- overwrite confirmation

They do not need bespoke layouts, but they should feel like part of the same product.

The current mismatch between the planner window and auxiliary dialogs should be treated as a real polish issue, not an optional extra.

### Status Signaling

Use low-noise visual cues:

- cyan for selected/active
- amber for constrained but still operable
- red for invalid/destructive only
- gray for passive metadata
- soft ice-blue or muted cyan can be used for passive anatomical emphasis

Map these cues to actual surgery concepts:

- selected tab
- selected body part
- available surgery
- force-queueable but blocked surgery
- suspended bill
- invalid preset

## Component Rules

### Buttons

Preferred styles:

- square icon buttons for local row actions
- compact rectangular buttons for section-level actions
- larger text buttons only for major global actions

Rules:

- consistent dimensions per button class
- consistent hover behavior
- consistent border treatment
- no large bright buttons inside dense lists unless absolutely necessary

### Dropdowns And Chips

- dropdowns should look quieter than action buttons
- active selection should be readable without overpowering the row
- repeat-use filters may use chip-like styling if it improves scan speed

### Icons

Safe icon-only actions:

- add: `+`
- remove/decrease: `-`
- delete: trash or `x`
- info: `i` or RimWorld info card affordance
- drag: subtle grip treatment

Use caution with icon-only controls for:

- import/export
- filter categories
- preset apply behavior
- force-queue state

If an action is not universally recognizable, keep text or a text-chip.

### Rows

Every row should have:

- a clearly dominant primary label
- subdued supporting metadata
- a stable action zone
- hover state that improves focus without overpowering content

### Motion

Motion should feel:

- precise
- mechanical
- restrained

Good candidates:

- hover fade
- subtle border brightening
- slight button brightness shift
- minor selected-state interpolation for tabs or body parts

Avoid:

- bouncy motion
- soft elastic movement
- excessive glow animations

## Implementation Strategy

This redesign should be incremental. Do not rewrite the mod into a fully custom UI framework.

### Recommended Architecture

Add a small shared style helper, likely:

- `Source/CorvusStyle.cs`

This should centralize:

- colors
- panel fills
- border drawing
- separators
- status badge drawing
- compact button rendering
- row hover rendering
- optional subtle procedural textures

The current mod concentrates most UI logic in `CorvusSurgeryUI.cs`. The redesign should gradually extract shared drawing code without trying to split every feature at once.

Initial extraction targets:

- planner shell and panel drawing
- compact button helpers
- row background and hover rendering
- queue row rendering helpers
- dialog header and button helpers

One explicit goal of the shared style helper is to make it easy to reproduce the benchmark screenshot's component language without duplicating drawing code across tabs and dialogs.

UI state should remain in the window layer, not in data model classes.

Do not store hover animation values on surgery data or preset data classes.

Instead:

- keep hover and selection animation state in the planner window
- key by stable identifiers such as recipe defName, bill identity, preset name, or body part key

### Technique Borrowing From Isekai

Good techniques to borrow:

- procedural gradients
- cached style textures
- custom panel drawing
- reusable drawing helpers
- restrained hover interpolation where useful

Techniques to avoid copying directly:

- fantasy-themed framing
- ornate highlights
- bespoke texture-heavy windows
- decorative full-screen compositions that fight dense data

## Phased Rollout

### Phase 1: Style Foundation

Deliverables:

- add `CorvusStyle.cs`
- define palette and drawing helpers
- create custom planner shell background and section panels
- standardize separators, borders, and compact buttons
- establish the benchmark screenshot as the canonical comparison target during review

Goal:

- establish the visual system without changing layout logic much

### Phase 2: Queue And Row Language

Deliverables:

- restyle surgery rows
- restyle queue rows
- introduce consistent compact action controls
- refine hover and status states

Goal:

- reduce clutter and make repeated interaction feel deliberate

### Phase 3: Layout Refinement

Deliverables:

- restructure filter bar hierarchy
- improve spacing and alignment across all three tabs
- improve distinction between main work area and queue column
- refine pawn cards and preset application controls

Goal:

- improve scan speed and visual order

### Phase 4: Visual Planner Upgrade

Deliverables:

- integrate the body diagram into a stronger surgical-planning surface
- improve selected-part feedback
- restyle the floating body-part surgery dropdown
- tune anatomical status coloring

Goal:

- make the visual planner feel like the signature feature

### Phase 5: Dialog Polish

Deliverables:

- restyle all auxiliary dialogs using shared helpers
- standardize title, body copy, and button layout
- align import/export and confirmation flows with the main UI language

Goal:

- make the whole mod feel cohesive

## Non-Goals

These are not priorities unless later justified:

- custom font pipeline
- full texture art pack
- animated decorative chrome
- replacing native RimWorld surgery logic
- replacing native RimWorld bill dialogs outside this mod's own surfaces
- turning the UI into a generalized sci-fi HUD

## Practical Rules For Future Work

When making UI changes, prefer:

- simpler controls
- denser but clearer layout
- quiet surfaces
- sparse accent usage
- stronger row hierarchy
- visible but restrained surgical state cues

Reject changes that:

- add visual noise without clarifying interaction
- use cyan everywhere
- make dense lists harder to scan
- replace clear text with ambiguous icons
- make anatomy harder to read in the visual planner
- move transient UI state into data classes

## Working Definition Of Success

The redesign is successful if the planner feels like:

- a Corvus-branded surgical operations console
- more modern and more deliberate than the current version
- easier to scan than before
- cleaner under heavy usage
- visually distinctive without becoming visually loud
- especially strong on the visual planner tab, since that is the mod's signature feature
- recognizably part of the same Corvus product family as the approved production-planner redesign

In short:

less vanilla utility window
more Corvus surgical workstation

but still fast, clear, and practical.
