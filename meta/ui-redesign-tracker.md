# Corvus Surgery UI Redesign Tracker

This tracker turns the redesign brief into a staged implementation plan.

Primary references:

- `meta/ui-redesign-brief.md`
- approved Corvus production-planner redesign screenshot
- current implementation in `Source/CorvusSurgeryUI.cs`

## Status Summary

- Brief updated for surgery-specific UI: done
- Visual benchmark incorporated: done
- Tracker created: done
- Phase 1 style foundation: done
- Phase 2 queue and row language: done
- Phase 3 layout refinement: done
- Phase 4A visual planner medium-scope upgrade: done
- Responsive main window sizing: done
- Final polish pass: in progress

## Guiding Rules

- Keep the Corvus product feel aligned with the approved production-planner redesign.
- Translate the component language, do not copy production-specific layout literally.
- Favor procedural drawing and shared helpers over a large texture art pass.
- Improve cohesion across main planner, queue, visual planner, presets, and dialogs.
- Avoid a full architecture rewrite before visual gains are visible.

## Phase 1: Style Foundation

Status: done

Goals:

- establish the shared Corvus visual language
- create reusable drawing primitives
- replace the generic flat shell with a styled shell

Tasks:

- create `Source/CorvusStyle.cs`
- define shared palette values
- add shared panel, separator, border, and background helpers
- add shared compact button helpers
- add shared section-header helpers
- add shared status-color helpers
- apply shell styling to `Dialog_CorvusSurgeryPlanner`

Acceptance checks:

- planner window no longer reads as a vanilla utility window
- shell and section framing clearly resemble the approved Corvus benchmark
- styling helpers are reused rather than duplicated inline

## Phase 2: Queue And Row Language

Status: done

Goals:

- make surgery rows and queue rows feel deliberate and compact
- reduce visual bulk while improving scan speed

Tasks:

- restyle overview-tab surgery rows
- restyle shared queue header area
- restyle queued-bill rows
- tighten suspend/activate/remove controls
- improve drag affordance visibility
- add consistent hover states for rows
- refine status treatment for available, blocked, force-queueable, and suspended states

Acceptance checks:

- row hierarchy is clearer at a glance
- local actions feel compact and consistent
- queue density and control placement align with the benchmark's right-column feel

## Phase 3: Layout Refinement

Status: done

Goals:

- improve overall information hierarchy
- make all three tabs feel like one cohesive workstation

Tasks:

- redesign filter bar grouping and spacing
- refine search placement and scale
- improve overview tab information strip
- improve split between main work area and queue column
- refine presets tab layout and pawn-card presentation
- align section spacing and padding across all tabs

Acceptance checks:

- filters read as grouped control clusters instead of loose vanilla widgets
- tabs feel structurally related
- the planner feels calmer and easier to scan under heavy use

## Phase 4: Visual Planner Upgrade

Status: mostly done

Goals:

- turn the body diagram tab into the signature Corvus surgery surface
- keep anatomy readability primary

Tasks:

- create a stronger panel frame for the body diagram area
- restyle visual planner header and surrounding shell
- refine selected body-part feedback
- refine body-part state colors
- replace the floating body-part surgery dropdown with an inline Corvus inspector
- ensure the visual planner still feels part of the same product family as the overview tab
- remove prototype-like guide artifacts and text clipping issues

Acceptance checks:

- visual planner feels intentional rather than prototype-like
- selected part and part state are immediately legible
- tab remains consistent with the production-planner benchmark language

Implementation notes:

- the current visual planner now uses an inline inspector panel instead of a vanilla float menu
- body-part rendering is still rect-based under the hood, but presentation and interaction have been upgraded
- a future Phase 4B can still revisit the actual anatomy rendering model if needed

## Phase 5: Presets And Dialog Cohesion

Status: partially done

Goals:

- eliminate the visual break between the main planner and auxiliary flows

Tasks:

- restyle preset controls in the main planner
- restyle preset controls in the presets tab
- restyle `Dialog_PresetNaming`
- restyle `Dialog_ImportFromSaves`
- restyle `Dialog_ExportPreset`
- restyle `Dialog_ImportPresetFile`
- restyle `Dialog_ConsolidatedImport`
- restyle `Dialog_OverwriteConfirmation`

Acceptance checks:

- dialogs visibly belong to the same Corvus UI family
- preset workflows no longer feel like leftover vanilla windows

Current status notes:

- preset controls inside the main planner and presets tab have been brought into the shared Corvus control language
- the auxiliary popup dialogs still need a dedicated dialog restyling pass

## Optional Phase 6: Final Polish

Status: in progress

Goals:

- add restrained finishing touches without adding noise

Tasks:

- tune remaining hover brightness and spacing
- add subtle procedural texture where helpful
- refine small alignment inconsistencies
- review accent usage and reduce any overuse
- review dialog and tab consistency
- capture remaining dialog work as post-redesign follow-up

Acceptance checks:

- polish improves identity without hurting readability
- accent color remains sparse and meaningful

Current polish outcome:

- shell, tabs, rows, queue, presets controls, scrollbars, and visual planner are now visually aligned
- the remaining visible work is concentrated in the smaller preset/import/export dialogs rather than the main planner

## Risks And Watch Items

- `Source/CorvusSurgeryUI.cs` is large, so visual work can become tangled if helper extraction is skipped.
- The visual planner can drift into a separate style if it is treated as a standalone feature.
- Dialog polish may get deferred unless tracked explicitly.
- Overuse of cyan will break the Corvus feel quickly.
- Large control rewrites may risk behavior regressions unless styling and structure are separated carefully.
- the body diagram is still built from hard-coded rect regions, so future rendering ambition should be scoped separately from the current UI pass

## Suggested Work Order

1. `CorvusStyle.cs`
2. planner shell and section framing
3. shared queue styling
4. surgery row styling
5. filter bar cleanup
6. visual planner shell and inspector
7. presets tab cleanup
8. dialogs
9. final polish pass

## Verification Checklist

- compare each major phase against the approved production-planner screenshot
- verify overview, visual planner, and presets still feel like one product
- verify dense lists remain readable at normal RimWorld UI scale
- verify hover and status cues remain restrained
- verify no critical surgery-planning interactions regress during UI changes
- verify the visual planner remains usable without a popup menu
