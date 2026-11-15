# Arrowhead fighter

The OBJ is a single low-poly, closed mesh with integrated twin guns and a
single UV-mapped texture. The ship faces local +Y; its visible top faces -Z,
matching the game's XY plane and camera. It is 4.25 units long and 3.8 units
wide before scaling.

The material uses `PewPewPew/PlayerColourLit`: magenta (`#FF00FF`) is player
colour 0 and green (`#00FF00`) is player colour 1. Texture compression and
mipmaps are disabled to preserve those keys. The OBJ material is remapped to
the supplied Unity material; the MTL also references the PNG for other tools.
The existing shader applies the two replacements sequentially, so a player
colour 0 close to the green key is also replaced by player colour 1.

This is a visual asset only. It does not replace an existing hull or provide
gameplay attachment points, colliders, or weapon firing behaviour. Scale it and
add those to a hull prefab when fitting it into a loadout.
