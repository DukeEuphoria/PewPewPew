# Asteroids

Asteroids use seeded, extruded marching squares rather than spheres. Rigidbody2D
keeps motion and rotation in the XY game plane. Asteroid gravity is disabled for
now, and field replenishment starts rocks with zero velocity and spin. Impacts
and shattering can still move them. Ship and orbital-body gravity is unchanged.

## Geometry and bandwidth

- Maximum resolution is **16x16 samples**, defining **15x15 cells**.
- Each sample is an unsigned byte of solidness. A complete field is at most
  **256 bytes**, independent of asteroid mass. The isosurface is **127.5**:
  **0-127 is outside; 128-255 is solid.** Boundary vertices interpolate between
  samples on opposite sides of this threshold. Generation centres the density
  ramp on 127.5 at the silhouette, saturating at 0 and 255 on either side.
- Smaller rocks occupy fewer cells of the same full grid. Seeded occupied
  radius ranges from 2 to 6.5 cells; increasing coverage does not change spacing.
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
- Generator/format **version 10** uses the displaced roof lattice, retaining the
  127.5 contour. There are no health fields. The
  descriptor includes the selected occupied radius and physical spacing/depth:
  observers need not share Inspector ranges.
  Mixed versions are explicitly rejected; there is no older-format migration.
- Initial asteroid-specific serialization is **67 bytes** for untouched rocks,
  or at most **325 bytes** for damaged rocks/children (67 + 2 dimensions + 256
  samples). At 250 rocks those states total **16,750 or 81,250 bytes**.
- Live updates additionally include 17 bytes of effect position, inherited
  velocity, and chip/shatter selection. State and its effect arrive together;
  repeated revisions do not replay an effect, and initial synchronization
  never replays old debris. The maximum live state is **342 bytes**.
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

The camera-facing, negative-Z roof has a broad hemispherical rise toward its
original centre, plus seeded angular relief. The dome radius is the furthest
original silhouette vertex from that centre, so smaller rocks have smaller
domes. Heights are sampled on the displaced lattice and remain faceted, not
smoothly shaded. The positive-Z back remains flat. `m_ReliefFraction` controls
the additional angular relief as a fraction of original logical thickness.
Both dome and relief are render-only: they do not change physical mass,
solidness, or 2D collision.

The original seeded height lattice and its fixed triangle diagonal define
permanent planes. Roof polygons are clipped against overlapping source facets;
walls are split at matching height-grid and diagonal crossings. Front/back UVs
remain in XY, with nondegenerate projected wall UVs.

Children inherit a separate immutable surface seed, affine source-frame mapping,
reference visual depth, relief amplitude, and original dome radius. These occupy
32 bytes, not a
heightmap. Children retain parent cell spacing and physical depth, along with
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

## Single-texture rock surface

The asteroid material uses `PewPewPew/AsteroidLit`, with one **Rock Texture**
slot. The editable procedural starter texture is `AsteroidIntact.png` in
`Assets/Materials`. Replace it on `Asteroid.mat` to change the art.
Default material tiling is 0.25 on both axes: each repeat spans four source
cells, twice the previous texture width and height.
The editor menu **PewPewPew/Create Default Asteroid Material** reproduces the
defaults when those generated assets do not already exist; it refuses to
overwrite existing artwork.

Solidity-based intact/cracked blending is disabled. There is no per-asteroid
solidness texture allocation or upload. Damage and hard edges are represented
only by the marching-squares mesh and collider; the texture does not fill holes.
Threshold-based texture switching is not implemented in this rollback.

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
game-zoom shadows and a rendered single-texture check.

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

