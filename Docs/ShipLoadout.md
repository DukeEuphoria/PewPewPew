# Ship loadout menu

The `ShipLoadoutMenu` canvas in `Assets/Scenes/GameScene.unity` provides the
saved preview, lobby and control templates. The catalog layout and component
buttons are constructed at runtime.
`MatchHud` provides the in-flight timer and end-of-match host controls.
The canvas owns the loadout panes at the top and the lobby strip at the bottom.

## Layout and fitting

- The equally sized panels show a scrollable, price-sorted component catalog
  and the ship preview. Page Up and Page Down move between categories. The
  details text follows the catalog item under the cursor (component or colour)
  only once the cursor has rested for the controller's **Component Hover Delay**
  (0.25s by default); resting over no item restores the usual fallback details.
  Sweeping across icons never flashes the hull text in between. The controller's **Catalog
  layout** settings set **Component Columns** and **Colour Columns** per row; each
  icon's width is the panel width, less **Category Padding** on both sides and the
  **Icon Gap** between icons, divided by the column count. **Category Gap** and
  **Category Title Height** set the spacing between categories and their title bars.
  Each component's icon is centred in its button and scaled by its size class,
  using the same small:medium:large ratios as the preview's **Marker Sizes**.
  **Icon Inset** sets the space between the button edge and a large component's
  icon; medium and small icons scale down from that size.
- Select a component, then click a highlighted compatible point to fit it.
  Hold Shift to retain the component selection; right-click clears it. Or select
  a point first, then choose a compatible component from the filtered catalog.
  A fitted component is replaced. Delete, Backspace or the Delete button clears
  the selected point. Clicking the right panel outside a point deselects it.
- Points accept the types specified on the prefab, and a component must be no
  larger than the point. Locked components remain inspectable but cannot be
  fitted.
- A gun's fire role belongs to the point; a thruster's control is determined
  by its point's orientation. Multiple copies can be fitted at compatible points.
- Hull navigation uses backward/forward triangle buttons beside the hull name.
  The details panel shows hull, point, component or colour details based on
  hover and selection.
  Changing hulls replaces the fitting with that hull's authored defaults.
- Locked hulls can be browsed, with prices shown with the overlaid hull name,
  but cannot be submitted or deployed.
- The main hull image uses a 1024-by-744 transparent render, with aspect-correct framing.
  The bottom lobby cards retain their own smaller hull thumbnails.
- Ship colours retain the two saved 64-colour palettes. The eight bright hues
  and eight greys are free; the remaining colours cost 100 and cannot be used
  until purchasing is implemented. The palette contains neither exact green
  (`#00FF00`) nor exact magenta (`#FF00FF`), which are hull shader keys.
  Primary and secondary swatches appear in
  separate catalog sections when no point is selected; clicking a free swatch
  applies it immediately. Saved locked or invalid colours are replaced
  with the free red primary or white secondary default. The server also rejects
  locked colours. Ready/deploy observes the match state, respawn timer and
  catalog validation.
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

The image is centred horizontally in the ship panel, and the render frame is
centred horizontally on the hull mesh even when points extend further to one side.
Scroll over the ship image or a slot to zoom about the centre of the view; hold
the right mouse button and drag to pan. Zooming out stops at the full-ship
frame, including its border, and panning cannot leave that frame. Changing hulls
restores the full view. The preview's **Max Zoom** and **Zoom Sensitivity**
fields control magnification and wheel response. Slot boxes remain the same
screen size while their positions follow the image. Right-clicking also clears
the selected catalog component as usual.

Every point has a clickable square marker, including empty points. An empty
marker that accepts one component type shows that type's symbol centred and
sized to the slot. One that accepts several types divides into four fixed
quadrants: shield top-left, gun top-right, thruster bottom-left and subsystem
bottom-right. Fitted markers show the definition's tinted **Button Artwork**, or
the matching component-type symbol, centred at its own size class: a large
component fills a large slot, while a medium or small component is drawn
smaller inside it. Large and medium slots faintly outline the smaller slot
sizes inside them using the replaceable `SlotGuide` texture (9-sliced, so the
line stays thin).

Each marker is an instance of `Assets/Prefabs/HUD/LoadoutSlotMarker.prefab`.
Edit that prefab to change the slot background, outline width and idle,
selected and compatible outline colours, the size-guide colour, and the type
symbols. At runtime the marker only positions, sizes, shows and tints these
authored parts; it creates nothing. The **PewPewPew > Loadout > Use slot marker
prefab** menu replaces any older non-prefab markers in GameScene.
The preview's **Marker Sizes** sets the small, medium and large square sizes;
**Slot Icon Padding** sets the pixels between the slot edge and its icons, and
between quadrant icons.
Markers are projected directly from the authored hull points and rotated so each
box's up matches its hull point's up vector as drawn in the render. Size guides
and empty-slot symbols rotate with the box. Fitted gun icons and directional
radar and shield icons follow the point; thruster icons face opposite the point.
Other fitted icons stay upright. The paperdoll uses the same icon orientations.
Markers can overlap, and all points are shown regardless of the number of saved
marker templates.

The hull, marker and catalog hover targets temporarily override the selected
item's details. The hull details include fitted mass, power and storage totals;
active subsystem effects are described separately rather than added to passive
totals.

## Authoring components

### Projectile damage

Gun details display four damage ratings authored on the **bullet prefab**, not
the gun definition: asteroid, shield, armour, and hull/components.

A projectile carries one surviving fraction, initially 1. Shields receive
`shieldRating * fraction`, preserving directional coverage, overlapping shields,
charge, and per-impact caps. Residual damage divided by the full shield rating
becomes the fraction reaching armour. Armour receives `armourRating * fraction`;
its residual divided by the full armour rating reaches hull/components.
Those systems receive `hullComponentRating * fraction` with existing
full-damage/proximity weighting.

Zero shield or armour ratings **bypass that layer** without changing its health
or the surviving fraction. Zero hull/components damage can strip defences
without damaging internals. An all-zero hit does nothing. Ratings must be finite
and nonnegative. Asteroids use only the asteroid rating.

Projectile armour damage directly depletes base armour health, not the hull's
wear ratio. With a strength boost `s >= 1`, armour absorbs up to
`currentArmour * s` effective damage, losing `absorbed / s` base health.
Physical collisions retain their existing blocking and armour-wear rules.

For shield/armour/hull ratings 20/10/8, shields absorbing 10 leave half a hit.
Armour then receives 5; if it absorbs 3, hull/components receive 1.6.

The existing bullet starts at **10 asteroid / 10 shield / 1 armour / 10
hull-components**. Armour 1 approximates the old unboosted 0.1-wear armour loss,
but does not exactly reproduce Bulwark's 0.08 wear or low-armour penetration.
Intact armour now blocks until its effective health is exhausted.

### Component metadata

Component metadata belongs on the existing `ShipComponentDef` ScriptableObject
assets, not on the visual prefabs:

- **Display Name**: optional; an empty value uses the asset's name.
- **Description**: text for the hull or component description window.
- **Button Artwork**: square PNG texture from `Assets/Art/Loadout Icons/Components`,
  imported as a single Sprite. Its aspect is preserved and it is drawn centred at
  its size class, in both the catalog and on fitted slots. An unset value falls
  back to the type symbol.
- **Tint**: multiplied with the artwork in the catalog and on fitted slots. Guns
  use red, shields purple, armour grey and energy components (thrusters, power,
  storage, radar and energy boosters) cyan. Locked catalog icons are greyed.
- **Size**: small, medium or large. The component type is determined by its
  definition class.
- **Price**: zero means available. A positive value means locked until the
  purchasing/ownership system is implemented. Locked component buttons show
  their prices. The server rejects locked loadouts and deployment as well.

Fill in these fields in the Inspector; the menu does not invent component
descriptions.

Add a `HullPoint` component to each attachment transform under the hull's
`HullPoints` component. Points are collected automatically, including inactive
children, in hierarchy order. Set each point's description and accepted
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

Subsystem definitions share activation and HUD settings but use separate
**Stat Booster**, **Radar**, and **Tractor** asset types. Stat boosters own
storage and multipliers, radar definitions reference their radar emitter, and
tractor definitions own pull settings and a **Visual** prefab reference. All
remain compatible with subsystem hull points.

Each gun fires all bullets in a burst from its own hull point. Gun angles and
thruster effects use their point's local Up direction; thruster force acts in
the opposite direction. A point can accept both guns and thrusters. The closest
force direction among forward,
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

Each shield visual prefab selects either the original **Grid** mesh or a
**Geodesic** mesh. Grid resolution is the number of latitude segments (3–64);
longitude density scales with it. Geodesic resolution is the number of
icosahedron subdivision steps (0–5). Full grid shields keep the prefab's sphere;
full geodesic shields use a complete geodesic sphere.

**Max Alpha** controls opacity before the edge fade, **Edge Alpha** controls
opacity at the arc boundary, and **Fade Start Proportion** sets the fade start
as a fraction of the definition's half-angle, measured from the point's Up
direction. These settings belong to the visual prefab. The fade is linear, with
its start strictly inside the half-angle and edge alpha no greater than maximum alpha. A 180-degree shield
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

## In-game ship HUD

The bottom-left paperdoll shows the fitted hull and installed components at
their loadout mount positions. Click a component, or press 1–0 and then
Shift+1–0, to toggle it or trigger a timed subsystem. Shortcuts follow occupied
mount order; components after the first 20 are mouse-only. Overlapping markers
retain their authored positions. Disabled or destroyed components are grey.
Subsystem mounts use their authored HUD gauges for activation and cooldown
feedback; a missing gauge falls back to the component artwork.

The paperdoll is 360 UI units wide by default. Its component icons and cargo
grids use their own marker scale, independently of the paperdoll size.

Every empty hull point stores cargo: small, medium and large points hold 16,
25 and 49 units, respectively. Fitting any component replaces that point's
storage. The in-game paperdoll shows one separated square per unit on each
empty point; dim squares are free, and occupied squares use the material's
colour. These grids do not appear in the loadout screen. Small, medium and large
pickups contain 1, 2 and 3 units, and a pickup must fit in full; its units can
span multiple empty points. The server stores material quantities per point and
sends them only to the ship owner.

On hull destruction, the server drops a configurable fraction of each material
(30% by default), rounded to the nearest unit per material with midpoint values
rounded up. The dropped units become randomly sized pickups; the rest is lost.
Empty points have no separate health and do not drop cargo from component damage.
Cargo is not retained between ships.

The tractor beam is an automatic subsystem: while enabled and healthy it pulls
the nearest visible pickup that fits the remaining cargo capacity. Its range,
forward cone, pull force and power use are configurable; the starter beam has a
range of 15, a 90-degree cone, force 5 and draws 2 power per second only while
pulling. Asteroids, orbital bodies and other ships block its beam. Beams are
visible to other players and may target the same pickup.
Each visible beam widens from its mount to the pickup's diameter.
`TractorBeams` handles targeting and pulling; `Ship` retains the synchronized
target IDs. Each tractor definition references a `TractorVisual` prefab. Its
material, starting width, width fraction, depth offset, scroll speed and repeat
interval are authored on that prefab; clients instantiate one visual per fitted tractor.
Assign a repeating texture to the material's Base Map to see it scroll towards
the mount. A missing visual prevents launch.

Disabled components draw no power and provide no effects. Thrusters ramp down
when disabled. Shield icons and bars toggle their shields; holding Space can
temporarily raise a disabled shield. A disabled power store keeps its charge,
but that charge and capacity are unavailable until it is enabled again.
Triggered subsystems remain enabled and start idle. Click or press their
shortcut to trigger them; further presses during their effect or cooldown have
no effect. Once the cooldown ends, they are ready to trigger again. Their
passive power draw still applies while they are enabled.

On the `ShipHudRoot` component in GameScene, **Paperdoll Size** changes the
square panel and shifts the bars alongside it. **Paperdoll Brightness** dims
the hull without dimming the icons, and **Paperdoll Marker Scale** adjusts
their size.

## Maintenance and validation

The controller arranges the saved pane and preview templates and builds the
catalog at runtime; edit the controller and catalog view to customize that
layout. Other saved scene controls can still be edited directly.
**Build static scene UI** is the initial construction tool; it refuses to
overwrite a canvas that already contains `LoadoutMenuController`.

**PewPewPew > Loadout > Validate static scene UI** reopens the saved GameScene,
checks point compatibility and sizes, defaults, network serialization, preview
selection and projection, catalog layout, hover details, fitted stats, thruster
control directions and effect alignment, then discards its temporary test state.
Save scene changes before running it.

The same validation can run with Unity batch mode using
`-executeMethod PewPewPew.Editor.LoadoutSceneValidation.Validate`.

**PewPewPew > Loadout > Validate lobby in play mode** runs a local Mirror host
and simulated server-side peer. It checks preview hit targets on all hulls,
fitting and selection shortcuts, colour migration and locking, HUD hit targets,
multiple shields and subsystems, ready/stop, server fitting rejection, thruster
force direction and controls, automatic start, respawn delay and redeployment.
The batch-mode entry point is
`-executeMethod PewPewPew.Editor.LobbyPlayModeValidation.Run` (without `-quit`;
the validator exits after leaving play mode).

Lobby and loadout pictograms (play/stop, kills, deaths, lock, delete and the
component-type category symbols) are `LobbySymbol` graphics. They draw a square
texture chosen by their **Kind** from `Assets/Resources/LobbySymbols.asset`. Replace
the PNGs in `Assets/Art/Loadout Icons/Symbols` to restyle them; no code changes are
needed. The textures are white on transparency, so each graphic's colour tints them.

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
