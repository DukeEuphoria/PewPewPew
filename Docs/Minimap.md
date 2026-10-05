# Minimap and Radar

The game scene contains a Minimap prefab instance under HudCanvas. For other
scenes, place `Assets/Prefabs/Minimap.prefab` under a Canvas, just like ShipHudRoot.
It only runs in Play Mode and always hides without a living locally owned ship.

## Minimap Inspector

- Activation: Always On uses the referenced Default Emitter asset, without power
  cost. Requires Subsystem combines all active mounted radar subsystems. Off
  disables the display.
- RectTransform: screen size and placement. The display remains circular even
  when its rectangle is not square.
- Display Range: radius in world units; contacts outside this radius are hidden.
  Each emitter also has its own detection range.
- Display Refresh Rate: UI redraws per second, independent of detection timing.
- Background Opacity, Marker Opacity, Border Width and colours: visual tuning.
- Fade Contacts: progressively fade stale detections. Disabling it keeps their
  opacity constant until expiry; it does not enable live tracking.
- Contact Lifetime: seconds until an unrefreshed contact disappears.
- Show Scan: display pulse rings, sweeping lines and cone boundaries.

## Emitter Assets

Create settings with Assets > Create > PewPewPew > Radar Emitter. The scene
minimap and subsystem definitions reference these assets. Runtime scan clocks
are independent even when several subsystems reference the same asset.

- Refresh: refresh every contact in range and cone at Refresh Rate per second.
- Pulse: launch expanding pulses at Refresh Rate per second, travelling at
  Pulse Speed world units per second. Overlapping pulses are supported.
- Sweep: rotate continuously at Sweep Speed degrees per second for a full
  circle, or sweep back and forth for a restricted cone.
- Sample Rate: detection updates per second in Pulse and Sweep modes. Crossings
  are checked over the elapsed interval, including scan wraparound.
- Half Angle: coverage either side of the mount's +Y direction. 180 means full
  circle; 45 means a 90-degree cone. There is no separate bearing offset.

Detected contacts retain their last observed world position and heading. Those
positions are displayed relative to the ship's current position. Ships use
directional markers, orbital bodies use scaled circles, and asteroids use dots.
Any emitter may refresh the shared contact; overlapping cones do not duplicate
markers. Detection uses client-known objects, not server-side fog of war.

## Subsystems and Mounts

Basic SubSystem Radar and Basic SubSystem Radar Cone are appended to the ship
catalog. Both are toggle systems with 0 passive and 3 active power/second.
Use the existing slot keys (1-4) to toggle installed systems. Destruction or
insufficient power switches a radar off through normal subsystem activation.
If another radar remains active, existing contacts continue fading. If none
remain active, the display hides and its contacts are cleared.

HullPoints now has a Sub Systems attachment array indexed by loadout slot,
including empty slots. Configure child transforms on the hull prefab; their
world +Y (`Transform.up`) sets emitter bearing and their positions set scan
origins. The Basic Hull has four named mounts facing forward, right, back and
left. Its existing two-slot loadout limit is unchanged.

A radar loadout requires a valid mount in its chosen slot. Non-radar subsystems
continue to work without mounts. Mounts also locate subsystem proximity damage,
using the same ShipSystem point handling as weapons.

## Verification

RadarMathTests in the existing EditMode test assembly cover delayed pulse
crossings, low-frequency updates, sweep wraparound, reflected cone sweeps,
contact fade expiry, and the radar's active power drain and shutdown.

For a multiplayer smoke test, check both a host and a remote client: toggle
each radar, rotate the ship with two different mounted cones, exhaust power,
destroy the ship, and respawn. The minimap uses replicated subsystem HUD state
on clients rather than attempting to step server-side activation locally.