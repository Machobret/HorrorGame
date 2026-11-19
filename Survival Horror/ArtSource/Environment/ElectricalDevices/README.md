# Wall-mounted interactable devices

[Props.blend](../Props.blend) is the editable source for Security Screen, Intercom Speaker,
Control Button, Keycard Reader, Electricity Box, and their antennas and post. Run the export
from the project root with Blender 5.0 or later.

```powershell
& blender --background --factory-startup --disable-autoexec ArtSource/Environment/Props.blend --python-exit-code 1 --python ArtSource/Environment/ElectricalDevices/export_props.py
```

The export stages FBXs and both packed base-color textures in
`Exports/Reviews/InteractableDevices/Models`. The box lives in `Assets/Game/Environment/ElectricalBox`
and the smaller devices live in `Assets/Game/Environment/ElectricalDevices`. Replace exported
models in place to retain their Unity metadata and material remaps. The four wall-mounted
devices pivot at the centre of the rear mounting plane. Local +Z faces away from the wall.

Check **Post Mount** on the root's **Device Indicator** component for freestanding placement. The pivot
stays on the device. Position it about 1.50 m above grade to leave about 0.56 m of the post
underground. The shared `DevicePost` prefab uses a copy of the packed atlas imported with
Repeat wrapping to preserve the shaft's UVs.

One `IndicatorLamp` binds each named emissive material slot to its own short-range realtime
point light. `Light_Universal` takes the caller's tint. `Light_Red` and `Light_Green` use their
fixed tints. Zero strength switches off emission and the corresponding point light.

For the four devices, call `DeviceIndicator.BodyIndicator.SetColor(color, strength)` or
`DeviceIndicator.AntennaIndicator.SetColor(color, strength)` independently. The existing
`SetIndicator(color, strength)` call controls the antenna, and `SetAntennaEnabled(bool)`
shows or hides the entire antenna.

For the electricity box, call `ElectricalBoxIndicator.SetPowered(bool)`. The cabinet
shows green when powered and red when unpowered. Its universal antenna light defaults off
and is controlled separately through `AntennaIndicator`. For a Chapter 1 installation,
assign the grid and circuit on the optional `CircuitDrivenElectricalBox` component.

Find the devices in **Window → Game → Asset Library**, under the **Interactables** category.
