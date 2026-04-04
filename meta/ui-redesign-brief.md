# Corvus Production UI Redesign Brief

## Purpose

This document defines the intended visual direction and implementation strategy for the `corvus-production-ui` mod UI. It exists so the design goals survive beyond any one coding session.

The aim is not to imitate vanilla RimWorld or to copy the Isekai mod's fantasy presentation. The aim is to build a cleaner, more distinctive, more usable production dashboard with a clear Corvus identity.

## Design Intent

Target feeling:

- industrial command software
- hardened operations dashboard
- discreet futuristic control system
- quiet, disciplined, information-first

Avoid:

- glossy holograms
- neon-heavy cyberpunk UI
- decorative fantasy framing
- gamer HUD clutter
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

Suggested roles:

- cyan: selection, active controls, focus, important positive state
- amber: warning, caution, material shortfall, exceptional attention
- red: destructive actions and hard invalid states only

### Texture and Finish

Use subtle materials, not illustrative backgrounds:

- faint vertical or brushed texture
- soft graphite gradients
- low-contrast panel separation
- occasional inset or bevel treatment

Do not place busy background motifs behind dense content areas.

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
3. visual cohesion
4. motion polish
5. decorative style

### Interface Philosophy

- rows should be easier to scan than they are now
- actions should feel more deliberate and less bulky
- controls should be consistent in size and behavior
- icon-only actions are acceptable when meaning is obvious and local
- text labels should remain where ambiguity would otherwise increase

### Quiet-by-Default

Default state should be subdued.

Only these should visibly "wake up":

- hovered actionable controls
- selected rows
- active filters
- primary actions
- important alerts

## Current UI Assessment

The current `ProductionWindow` is structurally sound:

- good functional coverage
- useful filter model
- strong bill-management utility
- appropriate use of native RimWorld dialogs and menus

Main weaknesses are presentation and hierarchy, not core capability:

- controls are visually chunky
- rows feel boxy and utilitarian rather than deliberate
- the filter area lacks stronger grouping and state emphasis
- action buttons consume too much visual weight
- the overall screen reads like a functional form rather than a command console

## Target UI Changes

### Window-Level

- replace the generic flat window presentation with a custom dark industrial background
- separate major regions more clearly:
  - filters
  - recipe browser
  - bill management
- use subtle panel depth or insets instead of heavy boxes everywhere

### Filter Bar

- visually group related controls
- show active filter state more clearly
- reduce bulk of dropdown controls where possible
- make reset secondary, not dominant

### Recipe Rows

Move toward a compact card or strip layout with:

- stronger title hierarchy
- quieter metadata lines
- a compact action cluster on the right
- smaller, more deliberate primary action than the current `Add Bill` text button

Likely direction:

- replace `Add Bill` with a square `+` button
- retain tooltip text for discoverability
- keep info action near the product/title area

### Bill Rows

Use a tighter action language:

- compact `-`, count, `+`
- small repeat-mode chip
- compact details/config control
- compact delete icon

The bill row should feel like an operational control strip, not a form row.

### Status Signaling

Use low-noise visual cues:

- cyan for selected/active
- amber for constrained but still operable
- red for invalid/destructive only
- gray for passive metadata

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

### Icons

Safe icon-only actions:

- add: `+`
- remove/decrease: `-`
- delete: trash
- info: `i` or RimWorld info card affordance

Use caution with icon-only controls for:

- details/configuration
- repeat mode changes
- abstract filters

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
- very small scale change on important buttons only

Avoid:

- bouncy motion
- soft elastic movement
- excessive glow animations

## Implementation Strategy

This redesign should be incremental. Do not rewrite the mod into a fully custom UI system.

### Recommended Architecture

Add a small shared style helper, likely:

- `Source/CorvusProductionUI/CorvusStyle.cs`

This should centralize:

- colors
- panel fills
- border drawing
- separator drawing
- status badge drawing
- compact button rendering
- row hover rendering
- optional subtle procedural textures

UI state should remain in the window layer, not in data model classes.

Do not store hover animation values on `RecipeInfo`.

Instead:

- keep hover/selection animation state in `ProductionWindow`
- key by stable identifiers such as recipe defName or bill load ID

### Technique Borrowing From Isekai

Good techniques to borrow:

- procedural gradients
- cached style textures
- custom panel drawing
- hover easing with `Mathf.MoveTowards`
- reusable drawing helpers

Techniques to avoid copying directly:

- fantasy-themed framing
- art-heavy window skins
- highly bespoke full-screen composition unless genuinely needed

## Phased Rollout

### Phase 1: Style Foundation

Deliverables:

- add `CorvusStyle.cs`
- define palette and drawing helpers
- create custom background/panel rendering for `ProductionWindow`
- standardize separators and borders

Goal:

- establish the visual system without changing layout logic much

### Phase 2: Control Language

Deliverables:

- replace bulky row buttons with compact action controls
- introduce consistent small button styles
- refine hover states
- add better tooltips where icon-only actions appear

Goal:

- reduce clutter and create a more deliberate interaction style

### Phase 3: Layout Refinement

Deliverables:

- restructure filter bar hierarchy
- refine spacing and alignment
- improve recipe and bill row anatomy
- improve section distinction between left and right panes

Goal:

- improve scan speed and visual order

### Phase 4: Polish

Deliverables:

- subtle background texture or procedural finish
- restrained motion tuning
- status chips/badges for availability states
- optional logo integration in header area if it does not compete with content

Goal:

- add identity without hurting usability

## Non-Goals

These are not priorities unless later justified:

- custom font pipeline
- full texture art pack
- animated decorative chrome
- replacing native RimWorld bill dialogs
- turning the UI into a generalized sci-fi HUD

## Practical Rules For Future Work

When making UI changes, prefer:

- simpler controls
- denser but clearer layout
- quiet surfaces
- sparse accent usage
- stronger row hierarchy

Reject changes that:

- add visual noise without clarifying interaction
- use cyan everywhere
- make dense lists harder to scan
- replace clear text with ambiguous icons
- move transient UI state into data classes

## Working Definition Of Success

The redesign is successful if the window feels like:

- a Corvus-branded operations console
- more modern and more deliberate than the current version
- easier to scan than before
- cleaner under heavy usage
- visually distinctive without becoming visually loud

In short:

less spreadsheet
more command terminal

but still fast, clear, and practical.
