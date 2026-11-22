# Ship loadout menu

The `ShipLoadoutMenu` canvas in `Assets/Scenes/GameScene.unity` contains all
loadout controls as saved scene objects. Additional component and hull-point
buttons are instantiated when needed.
`MatchHud` provides the in-flight timer and end-of-match host controls.
The canvas owns the loadout panes at the top and the lobby strip at the bottom.

## Layout and fitting

- Select a hull point on the ship preview, then select a shield, gun, thruster,
  or subsystem in the left panel. Each point holds at most one component. The
  selected point's button below the categories clears its component.
- Points accept the component types specified on the prefab, and a component must
  be no larger than the point (small, medium, or large). The selected point's
  size and each component's size are shown in the menu. Incompatible or locked
  components are disabled and show the reason on hover.
- A gun's fire role belongs to the point; a thruster's control is determined
  by its point's orientation. Multiple copies can be fitted at compatible points.
- Hull navigation uses small square backward/forward triangle buttons inside
  the right-hand rendered image, without word labels. The hull name overlays
  the image; its description overlays the bottom only while the separate
  `HullDescriptionHoverArea` rectangle is hovered.
  Changing hulls replaces the fitting with that hull's authored defaults.
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
the UI markers instead of allocating more render textures.
The texture is released when replaced or when the preview is destroyed.

Every point has a clickable marker, including empty points. Fitted markers use
the definition's **Button Artwork** sprite or its name when no artwork is set.
Markers are projected from the authored hull points and separated when they
overlap. If separation is not possible they use a grid instead. Hulls with more
than 20 points have previous/next page controls; the point count is not limited
to the number of saved marker templates.

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
- **Size**: small, medium or large. The component type is determined by its
  definition class.
- **Price**: zero means available. A positive value means locked until the
  purchasing/ownership system is implemented. Locked component buttons show
  their prices. The server rejects locked loadouts and deployment as well.

Fill in these fields in the Inspector; the menu does not invent component
descriptions.

Add a `HullPoint` component to each attachment transform under the hull's
`HullPoints` component. Points are collected automatically, including inactive
children, in hierarchy order. Set each point's accepted
component-type flags, size, gun control role, and optional default
component. The default must be in the catalog and fit that point. Hierarchy
order determines network assignment, shield absorption, and power-storage
drain order; do not reorder points independently across client and server builds.

With Gizmos enabled, hull points show a translucent marker and an arrow pointing
along the point's local Up direction. The marker is a cube when the point accepts
subsystems and a sphere otherwise. It is red for guns, yellow for thrusters, cyan
for shields, green for subsystems, white for multiple accepted types, and grey
when no type is accepted. Gun points have a dotted red line forward; thruster
points have yellow exhaust lines diverging 30 degrees from the local Up direction.
Mixed points show the indicators for every accepted type.

Definitions under `Assets/ShipComponents/Hulls`, `Shields`, `Weapons`,
`Thrusters` and `SubSystems` are discovered by the editor.
Importing or moving component assets schedules a catalog sync; builds sync
before proceeding. A manual command is also available:

**PewPewPew > Loadout > Sync component catalog**

New definitions are appended to the unified component array in the `ShipCatalog`
asset in `Assets/Resources`. Existing indices are not sorted or reassigned:
loadouts send these indices
over the network, so server and clients must use the same catalog/build.
Missing or duplicate catalog entries are reported as errors. Restore removed
definitions rather than silently shifting indices.
Radar definitions used by subsystems are not a separate fitting category.

Guns fire and thruster effects emit along a point's
local Up direction; thruster force acts in the opposite direction. A point can
accept both guns and thrusters. The closest force direction among forward,
reverse, left and right selects W, S, A or D; exact diagonal ties favour
forward/reverse. A point pointing perpendicular to the movement plane is invalid.
Thruster effect prefabs should emit along their local +Y axis.

## Ship controls

W fires forward-facing thrusters, S reverse-facing, and A/D left/right. Left and right
mouse fire main-role and secondary-role guns respectively. Each fitted gun or
thruster operates independently, subject to power and damage.

Subsystems 1–9 and 0 use those number keys; Shift plus the same keys controls
subsystems 11–20. All subsystem HUD icons can also be clicked.

Each fitted shield has a clickable HUD bar and starts switched on. Hold Space to
temporarily activate all shields, restoring their individual toggle states on
release. While held, working shield visuals stay visible with their current
charge colour, then fade normally on release; destroyed generators cannot
produce a held visual. Shift+Space toggles all shields off if all are on, otherwise on.
Inactive shields retain their charge but do not absorb damage. Active shields
absorb in hull-point order, each subject to its own charge and impact limit,
before remaining damage reaches armour. Shields recharge automatically even
while switched off, subject to power and generator health.

Shield definitions set **Half Angle** around the hull point's local Up direction.
180 degrees covers the whole ship; the supplied directional variants cover
90, 45, or 22.5 degrees on either side. Coverage is measured from the centre
of the hull bounds to the impact point. Only shields covering that direction
absorb the hit, in hull-point order; uncovered damage reaches armour. Their
flashes show the same coverage on the shield ellipse.

Each shield definition selects either the original **Grid** mesh or a
**Geodesic** mesh. Grid resolution is the number of latitude segments (3–64);
longitude density scales with it. Geodesic resolution is the number of
icosahedron subdivision steps (0–5). Full grid shields keep the prefab's sphere;
full geodesic shields use a complete geodesic sphere.

**Max Alpha** controls opacity before the edge fade, **Edge Alpha** controls
opacity at the arc boundary, and **Fade Start Angle** is measured from the
point's Up direction. The fade is linear, with its start strictly inside the
half-angle and edge alpha no greater than maximum alpha. A 180-degree shield
uses maximum alpha uniformly around its circumference. On the ShieldVisual
prefab, **Latitude Fade Angle** sets the latitude above and below the ship's
equator where the shield becomes transparent (0.1–90 degrees, default 45).
Opacity falls linearly from the equator to that latitude, producing a band.
These fades multiply the hexagonal texture's alpha and the existing hit-flash
fade; they do not affect damage coverage.

The power bar shows only the hull's storage. Power-storage subsystem gauges show
their own share of the same combined power bank: they drain in hull-point order
before the hull bar drains. On recharge, the hull fills first and the subsystem
gauges fill in reverse order. These segments are display-only; power calculations
continue to use the combined bank.

## Ship friction and gravity

Hull **Friction** is the flat-space quadratic drag coefficient; both supplied
hulls use `0.05`. On the server, ship drag opposes velocity with magnitude
`Friction * speed^2 * frictionScale`. Releasing thrust in flat space slows the
ship toward rest; quadratic drag approaches zero speed asymptotically.

**Friction Gravity Threshold** on `GameWorld`, alongside the gravity factors,
defaults to `1` acceleration unit. Full friction applies at or below it. Above
it, `frictionScale = threshold / netGravityAcceleration`: twice the threshold
gives half friction, ten times gives one tenth. A zero threshold keeps full
friction only where net gravity is zero.

The net acceleration is the magnitude of the summed gravitational force,
including the world edge fence, divided by ship mass. Opposing pulls cancel,
and ships of different masses receive the same friction scale in the same
field. Thrust and rotational damping are unchanged.

## Ship camera size hints

Attach `CameraSizeHint` to a large object's root to request a **Minimum Size**
(camera orthographic half-height in world units). **Radius** is its circular
world-space footprint centered on that transform; it does not inherit scale.
Disabled or inactive hints are ignored, as are camera-excluded layers and
objects outside the camera's depth range.

`ShipCamera` takes the largest request and its normal speed-based zoom, then
uses its existing zoom smoothing. Hints may exceed the speed-zoom maximum.
They apply fully when the footprint intersects the speed-based view and blend
in across **Size Hint Margin** outside it (currently `50` world units). Testing
against the speed-based view, not the expanded view, prevents zoom from sticking
out after an object passes. Aspect ratio and camera rotation affect that view.

The saved planet and moon have hints alongside their gravity sources: minimum
sizes `20` and `5`, with radii `12.5` and `4.75`. These requested absolute
minimums do not add zoom while the ship camera's own minimum remains `20`;
raise them in the Inspector to produce additional large-object zoom.

## Maintenance and validation

Edit existing scene controls directly to customize layout and styling.
**Build static scene UI** is the initial construction tool; it refuses to
overwrite a canvas that already contains `LoadoutMenuController`.

**PewPewPew > Loadout > Validate static scene UI** reopens the saved GameScene,
checks point compatibility and sizes, defaults, network serialization, preview
selection and paging, thruster control directions and effect alignment, then
discards its temporary test state. Save scene changes before running it.

The same validation can run with Unity batch mode using
`-executeMethod PewPewPew.Editor.LoadoutSceneValidation.Validate`.

**PewPewPew > Loadout > Validate lobby in play mode** runs a local Mirror host
and simulated server-side peer. It checks preview hit targets on all hulls,
fitting, HUD hit targets, multiple shields and subsystems, ready/stop, server
fitting rejection, thruster force direction and controls, automatic start,
respawn delay and redeployment.
The batch-mode entry point is
`-executeMethod PewPewPew.Editor.LobbyPlayModeValidation.Run` (without `-quit`;
the validator exits after leaving play mode).

The initial **Build static scene UI** command includes the bottom lobby upgrade.
**Upgrade bottom lobby UI** migrates older top-scoreboard layouts while preserving
the existing component buttons. Re-running it on an upgraded scene only repairs
required symbol canvas renderers; it does not rebuild custom layout changes.

**Upgrade rendered ship pane** migrates the former placeholder into the large
rendered preview with marker templates and hover description. It refuses to
overwrite an already-upgraded preview. The initial construction command also
includes this migration. Neither migration changes the ship-colour controls.

`PewPewPew.Editor.SoloMenuPlayModeValidation.Run` checks hull point discovery after
a script reload, Solo scene transition, visible and selectable points on each hull,
readiness and ship spawn. `PewPewPew.Editor.MountRemoteValidationBuild.Build`
builds a separate host/client validation player using `-mountBuildPath`; this
development build enables a validation-only network harness.
