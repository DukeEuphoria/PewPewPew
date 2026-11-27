# Asteroids

Asteroids use seeded, extruded marching squares rather than spheres. Rigidbody2D
keeps motion and rotation in the XY game plane. Asteroids and released treasure
receive the same server-side world gravity and edge force as other space
objects. Field replenishment still starts rocks with zero velocity and spin;
gravity, impacts and shattering can subsequently move them.

## Geometry and bandwidth

- Maximum resolution is **16x16 samples**, defining **15x15 cells**.
- Each sample is an unsigned byte of solidness. A complete field is at most
  **256 bytes**, independent of asteroid mass. The isosurface is **127.5**:
  **0-127 is outside; 128-255 is solid.** Boundary vertices interpolate between
  samples on opposite sides of this threshold. Generation centres the density
  ramp on 127.5 at the silhouette, saturating at 0 and 255 on either side.
- Each rock is generated from a seeded number of overlapping splats. The
  Inspector's minimum and maximum splat counts default to 2 and 4 (supported
  range 1–16); each splat independently picks a radius from the existing 2–6.5
  cell range. Positions are constrained to the grid and share solid samples
  with earlier splats, so the generated rock is connected. Large splats have
  less room to move, and later splats may be partly or completely covered.
  At each sample splat densities add together, capped at 255: adding a splat
  never reduces existing density. Cell spacing and the 16x16 resolution are unchanged.
- Inspector settings author base cell spacing, physical thickness and material
  density (all initially 1), plus a uniform scale variance (0.9 to 1.1).
  The selected scale affects cell spacing and thickness, not the sample count.
- `m_VertexOffsetFactor` scales each seeded random vector within the unit
  circle by the cell width: 0 leaves a regular grid, 1 allows displacements
  up to one cell width. Candidates are resampled or shortened as needed to
  keep cells convex at every factor. Changing the factor scales the same
  seeded offsets rather than selecting new ones.
  Meshes, colliders and geometric mass use the same displaced sample positions;
  explosion falloff uses their actual world-space positions.
- The generator uses its own fixed integer random sequence. Seed, generator
  version, and physical descriptor reconstruct untouched rocks.
- Damage changes the current field. Late observers receive that field, not
  an unbounded damage history. No mesh vertices are transmitted.
- Generator **version 11** uses seeded overlapping splats and the
  displaced roof lattice, retaining the 127.5 contour. There are no health fields. The
  descriptor includes the splat seed, count, radius range and physical spacing/depth:
  observers need not share Inspector ranges.
  Asteroid wire format **version 13** additionally carries treasure descriptors.
  Mixed wire versions are explicitly rejected; there is no older-format migration.
- Initial asteroid-specific serialization is **73 bytes** without treasure for
  untouched rocks, or at most **331 bytes** for damaged rocks/children. Each
  embedded item adds **17 bytes**, up to 16 items: maximum untouched and damaged
  payloads are **345 and 603 bytes**, respectively.
- Live updates additionally include 17 bytes of effect position, inherited
  velocity, and chip/shatter selection. State and its effect arrive together;
  repeated revisions do not replay an effect, and initial synchronization
  never replays old debris. The maximum live state is **620 bytes**.
- These sizes **exclude** Mirror identity/behaviour framing, network transforms,
  transport overhead, and all other game state.

The front/back mesh and exposed side walls are generated from the same filled
polygons used by a PolygonCollider2D source and a manually rebuilt
CompositeCollider2D. Internal cell seams are merged; drilled holes remain empty.
The empty outer sample border is included within the resolution cap.
Between samples of 0 and 255 the contour crosses halfway along the displaced edge.
Sub-threshold neighbours are preserved during component extraction so splitting
does not shift the surviving interpolated boundary.

## Visual thickness and PC shadows

The camera-facing, negative-Z roof combines a hemisphere for each original
splat by taking the highest dome at each lattice sample, plus seeded angular
relief. Heights are sampled on the displaced lattice and remain faceted, not
smoothly shaded. The positive-Z back remains flat. `m_ReliefFraction` controls
the additional angular relief as a fraction of original logical thickness.
Both dome and relief are render-only: they do not change physical mass,
solidness, or 2D collision.

The original seeded height lattice and its fixed triangle diagonal define
permanent planes. Roof polygons are clipped against overlapping source facets;
walls are split at matching height-grid and diagonal crossings. Front/back UVs
remain in XY, with nondegenerate projected wall UVs.

Children inherit the immutable splat descriptor, surface seed, affine source-frame
mapping, reference visual depth and relief amplitude, not a heightmap.
Children retain parent cell spacing and physical depth, along with
the surviving original roof planes and flat back plane. The surface moves and
rotates with the asteroid, rather than being anchored to world coordinates.
Damage never recentres or regenerates the dome; fragments retain their part
of the original rounded surface.
Temporary debris retains the cheap flat extrusion path.

The asteroid renderer casts and receives shadows. The PC pipeline shadow
distance is 150, covering the game plane from the Z=-100 camera at normal
20-50 orthographic zoom. Mobile pipeline settings are unchanged. Existing scene
lights already cover the asteroid's rendering layers (4 and 8); test lights
must use those layers too. Shadow atlases, cascades, and scene light bias are
unchanged. Increasing PC shadow distance includes other casters as well.

## Solid and cracked rock surface

The asteroid material uses `PewPewPew/AsteroidLit`, with **Solid Texture**
and **Cracked Texture** slots. The starter textures are
`AsteroidIntact.png` and the procedurally generated `AsteroidFractured.png`
in `Assets/Materials`.
Both textures share the solid texture's tiling and source-frame mapping.
The editor menu **PewPewPew/Create Default Asteroid Material** reproduces the
material when the generated solid texture and material do not already exist.
The separate **PewPewPew/Create Default Cracked Asteroid Texture** menu creates
the cracked texture. Neither menu overwrites existing artwork.
The generated cracked texture uses broad, dark fissures to remain visible
when downsampled at game zoom. Small hits and distant rocks can still show only
a subtle change because the texture blend follows the sample density.

The mesh stores a solid-texture weight per vertex: 0 at the 127.5 isosurface
and 1 at density 255, interpolated linearly between them. The shader blends
the cracked and solid textures using this weight, stored as an 8-bit vertex
colour channel. Exposed contour edges are
therefore cracked even on an undamaged rock. Roof facet and wall vertices
interpolate weights from their source polygons; the mesh topology is unchanged.
There is no per-asteroid solidness texture or additional network data.

Triplanar surface sampling covers roofs and walls. The immutable source frame
anchors the rock texture through fragment recentering. A shared material and
per-renderer property block supply the source mapping, with no additional
network data. URP lighting,
cast/received shadows and depth passes are retained.

## Damage and mass

There is no abstract asteroid size or size-to-scale conversion. Mass is occupied
marching-polygon area times physical cell area, thickness and material density.
More occupied cells therefore mean a physically larger, heavier rock at the
same spacing. Excavation never rescales the surviving rock.

There is **no minimum damage threshold**. Any finite positive damage from
bullets or physical impacts can erode solid material, regardless of asteroid
mass. Zero and negative damage do nothing. Nonfinite damage is reported and
rejected.

Damage consumes solidness levels directly, with no conversion multiplier or
per-hit cap. A shared corner intentionally affects its adjacent squares.

- **Radius <= 0.5** is a point impact, lowering the nearest corner. Point bullets
  consume a selected solid corner down to **0**, then pass remaining damage forward along
  the impact vector. Traversal skips corners already below **127.5** and stops at the grid boundary
  or when spent. Point non-projectile damage discards excess at that corner.
- **Radius > 0.5** is a circular explosion. Each corner receives
  `damage * max(0, 1 - worldDistance / radius)`: full damage at the centre,
  linearly decreasing to zero at the edge. Damage is not divided between
  corners, and excess never propagates. No corner inside means no damage;
  explosion hits do not snap to an outside corner.

World-space distances account for the asteroid transform and cell spacing.
The public `TakeAreaDamage` entry accepts a radius without a random crack chance.
Physical collisions use an Inspector-authored `m_CollisionDamageRadius`, initially
**1 world unit**, and cannot randomly crack the asteroid. Ordinary erosion can
still disconnect pieces. Bullet overflow stays inside the hit asteroid; the
bullet object itself still retires on hit.

Fractional damage accumulates per corner on the authoritative server until a
whole byte level can be removed; it is neither rounded up nor discarded.
Remainders survive component extraction and child recentering. Observers only
need current bytes, so this server-only bookkeeping adds no snapshot bandwidth.
Contours remain byte-quantized between complete levels.
Point-projectile propagation requires a finite nonzero vector; invalid radius,
chance or coordinates are reported.

There is **no asteroid health, maximum health or integrity conversion**.
Retirement is determined by empty geometry or remaining pieces below the
minimum physical mass. Children simply inherit the remaining samples and
fractional damage; there is no separate health allocation or health snapshot.

## Shattering and child grids

- Accepted projectile hits use their bullet-authored **Asteroid Crack Chance**
  directly, once per hit, with no dependence on damage amount, remaining
  solidness or radius. Probability ranges from 0 to 1; SimpleBullet starts at
  **0.01 (1%)**. A deterministic server-owned sequence, separate from the visual
  seed, supplies the roll. No-damage hits do not roll.
- A crack propagates through the hit region toward the opposite boundary,
  preferring weakened samples with seeded jitter and a bounded path search.
  Orthogonally joined carved samples match four-neighbour connectivity. Up to
  two paths are attempted; actual surviving connectivity, not the roll, decides
  whether a shatter occurred. Ordinary erosion can also disconnect pieces.
- Simple through-cuts normally yield two substantial children; irregular
  topology can yield more. No arbitrary piece cap discards substantial material.
  Components below `m_MinPieceMass` become cosmetic debris. If just one
  substantial component survives, retain it and play a chip effect; no fake
  multi-piece shatter is reported.
- Each child uses a full 16x16-sample grid containing its existing cut shape.
  Recentering shifts samples by integer cells only: no resampling, fitting,
  enlargement or depth normalization. Holes, solidness and fractional pending
  erosion are preserved exactly. Smaller children simply occupy fewer cells.
- Children preserve their parent's seeded sample offsets through recentering.
- Children inherit cell spacing (including the parent's scale variance),
  thickness and material density. Their mass is therefore the exact retained
  geometric volume, within floating-point tolerance.
- Children inherit parent point velocity/spin and receive an outward impulse on
  the server. Sibling collisions are ignored for 0.35 seconds while they separate,
  then restored.
- The retiring parent's collision/simulation stops immediately. Delayed despawn
  allows its final state/effect to reach observers.

Only real asteroid objects count toward the field target. Fragmentation can
temporarily exceed that target; replenishment waits until below it.

## Embedded treasure and pickups

Gold, silver, and red, green, cyan and purple crystals each have small, medium
and large prefabs. The shared `Resources/AsteroidTreasure` ScriptableObject
contains a weighted list of treasure types, each with its own list of prefabs,
and controls the treasure and peeking chances and maximum item count. Its defaults
are a 70% chance of treasure, one to three requested items per successful rock,
equal type and size weights, and a 30% peeking chance. Size weights apply within
each type. New asteroids select prefabs on the authoritative server using
an independent seeded sequence, then position the items themselves. Placement is
bounded and avoids overlap; a small or crowded rock can receive fewer items than
requested. The default footprint radii are 0.25, 0.4 and 0.6 times the rock's
cell spacing. Each prefab owns its material, shared mesh, type, size, unit count,
and collider radius.

Embedded items are prefab instances with physics disabled: their 2D footprints
overlap the current marching-square rock, and they cannot collide with ships or
be collected.
Peeking is a visual roof placement, not an indication that the footprint is
free. Buried items are not rendered until their centre is excavated, even if
their larger footprint is still supported. After damage, an item releases when its circular footprint no longer
overlaps the surviving solid polygons. Mere tangency is not supporting rock,
within a small geometric tolerance. A fracture through supporting rock releases
an item if its footprint overlaps more than one component; otherwise the item
follows its one retained component, or releases if that component is discarded.
Support is checked again after the component is recentered.
Splitting preserves the item's world position and does not regenerate treasure.

Released pickups are separate server-owned network objects. They inherit the
rock's velocity at the release point and angular velocity, receive world
gravity, and collide physically with world objects and ships. Treasure impacts
do not apply collision damage to ships or asteroids. Server-side ship contact
collects the entire pickup if its cargo stores have room; otherwise it stays
physical and available. The default small, medium and large pickups contain 1, 2 and 3 units; collection
and cargo drops use the unit count authored on each prefab. Each treasure type
must include a one-unit prefab so every cargo remainder can be dropped.
Orbital-body contact still removes a pickup. Pickups
otherwise persist until match teardown, so an active field can accumulate them
indefinitely. Treasure value and universe-wide supply balancing are not yet
implemented.

The spawnable-prefab loader registers every catalog prefab, including prefabs
outside the usual `Resources/SpawnablePrefabs` folder.
The editor menu **PewPewPew/Create Treasure Catalog and Prefabs** creates
missing default assets without replacing existing prefab or catalog settings. If an existing
catalog has a missing prefab reference, reassign the recreated prefab in the
catalog. Embedded descriptors
are sent with asteroid snapshots, including to late observers; released pickups
use their network prefab identity and synchronize their scale.

## Prefab references and presentation

Keep the existing asteroid network asset identity and spawn registration.
Required components are MeshFilter, MeshRenderer, Rigidbody2D,
PolygonCollider2D, CompositeCollider2D, NetworkIdentity, Asteroid, and the
existing server-authoritative NetworkRigidbodyUnreliable2D.

The asteroid has separate **chip explosion**, **shatter explosion**, **chip
sound**, and **shatter sound** references. The current prefab references the
existing damage/ship particle prefabs and existing impact/explosion audio clips.
Physical separation is implemented on the server; particle prefabs do not
provide the authoritative force.

Temporary fragments use local noncolliding motion, a shared small rock mesh,
a two-second lifetime, and a global cap of 96 live fragments. A chip requests
five fragments; a shatter requests twelve. They have no health, pickup,
inventory, authoritative collision, or persistence behavior. Future collectibles
should be separately spawned server-owned objects, not promoted cosmetic debris.

Focused Unity tests cover seeded geometry, byte limits, erosion, hole collisions,
fractional hits at different masses, point overflow, circular falloff, collision
radius, bullet-authored cracking, child material/mass, fixed-spacing recentering,
stationary motion, serialized late-observer reconstruction, effect revisions, persistent
roof planes, actual cast/received shadows at game-camera distance, and projectile
target routing.

## Bullet ratings

Bullet prefabs now author separate **asteroid, shield, armour, hull/components**
ratings. Bullets also author **Asteroid Crack Chance** and **Damage Radius**;
SimpleBullet defaults to **1%** and **0.5** respectively. These affect asteroids
only; ship layering and component splash rules are unchanged. Ratings and impact
settings are prefab data, not extra per-shot network fields.
See [ship/loadout damage rules](ShipLoadout.md#projectile-damage)
for fraction-preserving penetration and zero-layer bypass.

## Performance validation

Focused validation includes real host/remote state, child snapshots,
game-zoom shadows and rendered texture-blend and real-asteroid visibility checks.

The earlier editor 250-rock generation/remesh timing predates the displaced roof
lattice and must be remeasured. Collider path
assignment is batched while the source collider is disabled, avoiding expensive
repeated intermediate shape builds. Height clipping only scans overlapping
source cells, never the whole lattice per polygon. Child grids now use a bounded
integer sample copy instead of the earlier shape-refitting search.
Component extraction reuses the unchanged sample positions, and remeshing
reuses its polygons for mass calculation. Fragment areas are calculated once
per split rather than repeatedly during sorting.

The graphical tests additionally exercise a 32-hit remesh burst, 16 fractures
and their recentered children, plus a 250-renderer shadow-on/off comparison at
game zoom. Timings are reported in the test output. Render timings include
synchronous GPU readback and pixel copying; they are **not isolated GPU timings**.
Unity's frame-timing API returned
no positive GPU samples in this batch environment. Net managed-heap changes are
not total allocation counts. A target-PC gameplay/GPU profile is still needed
for a production frame budget.

Before the prefab-backed treasure refactor, an isolated Editor batch test generating 250 rocks with the treasure chance
forced to 100% and the maximum set to 16 took 2,171 ms in one run, producing
2,233 embedded items and a largest untouched descriptor of 345 bytes. This is
a stress configuration, not the default spawn rate or a gameplay frame budget;
it excludes rendering and long-term accumulation of persistent released pickups.
In a separate 512x512 graphics-enabled Editor test at orthographic size 20,
30 of 100 seeded deposits were visibly different from the bare rock; all 30
selected peeking deposits were visible.

