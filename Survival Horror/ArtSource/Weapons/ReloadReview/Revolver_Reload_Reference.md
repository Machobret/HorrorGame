# Revolver reload animation reference

The model is the existing six-inch Colt Python asset. The animation follows the swing-out cylinder silhouette shown in Colt's 2020 Python operator manual: cylinder opens toward the left, cases leave the cylinder rear, ammunition enters the rear, and the support hand closes the cylinder deliberately rather than flicking the whole gun.

Reference: https://www.colt.com/wp-content/uploads/2023/02/2020pythonmanual.pdf (loading pages21-24; unloading pages31-32).

`RevolverReloadVisual` creates the effect from the actual imported cylinder bounds. Its hinge sits below the cylinder and turns around the barrel axis. Both arms use procedural two-bone posing on top of the steady handgun-ready clip. The existing walking reload layer remains in use.

The three-second timeline lowers the firing hand, opens the cylinder, ejects the missing chambers' spent cases, brings in only the reload transaction's round count, and closes the cylinder before returning to idle. A loader ring appears for a full-cylinder reload; partial reloads keep their remaining live rounds and display only the replacement cartridges. Gameplay ammunition still changes only through PlayerStateReload. Cancellation, unequip, and component disable restore the cylinder and hide transient loading meshes.

Reference describes the visual action, not a simulation of each internal mechanical part. The source cylinder mesh and texture remain unchanged.
