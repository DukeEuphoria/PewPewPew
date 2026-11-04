# Ship loadout menu

The `ShipLoadoutMenu` canvas in `Assets/Scenes/GameScene.unity` contains all
loadout controls as saved scene objects. No buttons are instantiated at runtime.
`MatchHud` provides the in-flight timer and end-of-match host controls.
The canvas owns the loadout panes at the top and the lobby strip at the bottom.

## Layout and fitting

- The left panel has five category tabs: shields, main guns, secondary guns,
  engines and subsystems. Each major-component category has 16 saved buttons
  in a 4-by-4 grid; subsystems have 64 in an 8-by-8 grid. Unused buttons are
  inactive, not destroyed. Only the selected category's grid is shown.
- Click a component to fit it. Selected components have a cyan highlight.
  Incompatible or locked components are disabled, but still provide hover
  information in the bottom-left description panel.
- Clicking a subsystem fills the first empty slot. Clicking a fitted slot
  clears it. Slots beyond the current hull's capacity are disabled.
- Hull navigation uses small square backward/forward triangle buttons inside
  the right-hand rendered image, without word labels. The hull name overlays
  the image; its description overlays the bottom only while the separate
  `HullDescriptionHoverArea` rectangle is hovered.
  Hull changes repair
  incompatible weapon/engine choices and clear excess subsystem slots.
- Locked hulls can be browsed, with prices shown with the overlaid hull name,
  but cannot be submitted or deployed.
- The main hull image fills the pane above the unchanged ship-colour controls.
  It uses a 1024-by-744 transparent render, with aspect-correct framing.
  The bottom lobby cards retain their own smaller hull thumbnails.
- Ship colours retain the two saved 64-colour palettes. Ready/deploy observes
  the match state, respawn timer and catalog validation.
- The loadout panel hides during blocking menus, while a ship is alive, and
  after the match ends. The lobby strip also hides while a ship is alive or a
  blocking menu is open, but remains available at match end to show scores.

## Lobby, readiness and player cards

- There are 16 saved player cards. Occupied cards are ordered by network ID;
  empty cards are disabled. The network server caps connections at 16 even
  if a larger capacity was configured.
- Each card shows the Steam avatar as its square background, with a transparent
  rendered image of the player's selected hull over it. The outline turns green
  when ready. Crosshair/kills and skull/deaths counters sit below the portrait.
  Names sit above it.
- The bottom-right ready control is a green play triangle with a **ready/total**
  count, for example `6/7`. Clicking it marks the player ready and changes the
  control to a grey stop square. Clicking Stop marks the player unready.
- Ready players cannot edit loadouts: both the editing canvas group and the
  server enforce this. Stop remains clickable outside that group.
- The server starts the match automatically when every connected player is
  ready and has a launchable loadout. One player is sufficient for solo testing;
  an empty lobby cannot start. A host cannot bypass the all-ready gate.
- During a running match, late joiners and dead players deploy individually.
  Death clears readiness; the respawn delay still applies. Match end also
  clears readiness. The host can return everyone to the lobby for another game.

`PlayerState.SteamId` is replicated cosmetic avatar identity, not proof of
authentication. Steam avatar requests report unavailable/failed results and
leave the neutral portrait background visible when Steam or an avatar is
unavailable. The hull and score display work without Steam.

Hull thumbnails render only when a player's hull or colours change. The saved
`LobbyHullRenderer` rig uses a dedicated `LobbyHullPreview` layer, an off-world
camera and light. Its disabled camera renders on demand into transparent
textures; other scene cameras exclude the preview layer. Only mesh renderers
are copied from hull prefabs, never gameplay scripts or physics components.
Cards release avatar and render textures when players leave or the UI is destroyed.

## Fitted-component preview

The main image uses the same isolated hull renderer as lobby thumbnails.
It renders again only when hull or colours change; changing components updates
the 17 pre-created UI marker objects instead of allocating more render textures.
The texture is released when replaced or when the preview is destroyed.

Component markers use each selected definition's **Button Artwork** sprite.
Until artwork is assigned, a small name label identifies the component.
Artwork is positioned using the hull prefab's attachment transforms, projected
through exactly the same hull-local XY frame as the render:

- Main/secondary weapons use the same `SelectPoints` and
  `MaxEmissionPointsConsidered` logic as the live ship.
- Thrusters use the selected thruster's `EmissionPointsUsed` hull points.
- Each fitted subsystem uses its corresponding `HullPoints.SubSystems` slot;
  empty slots have no marker. Missing subsystem anchors emit a warning and
  should be authored on the hull prefab.
- Shields have no dedicated attachment transform, so their marker sits at the
  hull root origin.

These are artwork overlays, not running weapon, particle or subsystem objects.
Incompatible weapon/thruster selections have no matching overlays and continue
to show the existing launch problem.

The transparent `ShipImage/HullDescriptionHoverArea` scene object defines the
description's hit area independently of the rendered image. Resize or reposition
its RectTransform in the Inspector; by default it covers the central 50% of the
image's width and 60% of its height. Navigation buttons remain above the hit area.
The description never intercepts raycasts, so appearing text cannot expand the
active region or cause enter/exit flicker. Its text is display-only rather than
pointer-scrollable. Disabling the hit area or preview hides the description.

## Authoring components

Component metadata belongs on the existing `ShipComponentDef` ScriptableObject
assets, not on the visual prefabs:

- **Display Name**: optional; an empty value uses the asset's name.
- **Description**: text for the hull or component description window.
- **Button Artwork**: optional sprite; an unset sprite leaves a name-only button.
- **Price**: zero means available. A positive value means locked until the
  purchasing/ownership system is implemented. Locked component buttons show
  their prices. The server rejects locked loadouts and deployment as well.

Existing definitions default to free, with no description or artwork. Fill in
these fields in the Inspector; the menu does not invent component descriptions.
Both description panels support scrolling for longer text.

Definitions under `Assets/ShipComponents/Hulls`, `Shields`, `MainWeapons`,
`SecondaryWeapons`, `Thrusters` and `SubSystems` are discovered by the editor.
Importing or moving component assets schedules a catalog sync; builds sync
before proceeding. A manual command is also available:

**PewPewPew > Loadout > Sync component catalog**

New definitions are appended to the `ShipCatalog` asset in `Assets/Resources`.
Existing indices are never sorted or reassigned: loadouts send these indices
over the network, so server and clients must use the same catalog/build.
Missing or duplicate catalog entries and exceeded capacities are reported as
errors. Restore removed definitions rather than silently shifting indices.
Radar definitions used by subsystems are not a separate fitting category.

## Maintenance and validation

Edit existing scene controls directly to customize layout and styling.
**Build static scene UI** is the initial construction tool; it refuses to
overwrite a canvas that already contains `LoadoutMenuController`.

**PewPewPew > Loadout > Validate static scene UI** reopens the saved GameScene,
checks static capacities, serialized references, binding/hover states, category
switching, normalization and lock validation, then discards its temporary test
state. It also checks the 16-card lobby layout, ready-lock behaviour and
non-empty transparent hull renders. Save scene changes before running it.

The same validation can run with Unity batch mode using
`-executeMethod PewPewPew.Editor.LoadoutSceneValidation.Validate`.
Optionally pass `-loadoutPreview <absolute PNG path>` to render a preview.

**PewPewPew > Loadout > Validate lobby in play mode** runs a local Mirror host
and simulated server-side peer. It checks ready/stop, server fitting rejection,
automatic all-ready start, respawn delay, individual redeployment and solo start.
The batch-mode entry point is
`-executeMethod PewPewPew.Editor.LobbyPlayModeValidation.Run` (without `-quit`;
the validator exits after leaving play mode).

The initial **Build static scene UI** command includes the bottom lobby upgrade.
**Upgrade bottom lobby UI** migrates older top-scoreboard layouts while preserving
the existing component buttons. Re-running it on an upgraded scene only repairs
required symbol canvas renderers; it does not rebuild custom layout changes.

**Upgrade rendered ship pane** migrates the former placeholder into the large
rendered preview with static marker objects and hover description. It refuses to
overwrite an already-upgraded preview. The initial construction command also
includes this migration. Neither migration changes the ship-colour controls.

These local checks do not replace testing live Steam avatars and independent
remote clients.
