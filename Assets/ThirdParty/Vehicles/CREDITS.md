# Detailed vehicle models

These 3D models are used under the [Creative Commons Attribution 4.0](https://creativecommons.org/licenses/by/4.0/)
licence (CC BY 4.0), downloaded from Sketchfab on 14 November 2026. Each folder keeps the model as downloaded and a
`license.txt` with its credit line.

| Vehicle in Coupled Sim | Model | Author | Folder |
|---|---|---|---|
| `DrivableToyotaCorolla` | ["2014 Toyota Corolla E180 EU (with interior)"](https://sketchfab.com/3d-models/2014-toyota-corolla-e180-eu-with-interior-36f95efb0585464cae43a25a3b3392e8) | [Armored Wave](https://sketchfab.com/armoredwave) | `ToyotaCorolla/` |
| `DrivableLandRoverDefender` | ["Land Rover Defender 110"](https://sketchfab.com/3d-models/land-rover-defender-110-5773ec04488b46148816b5f7a9f3f44f) | [meeww](https://sketchfab.com/meeww) | `LandRoverDefender/` |
| `DrivableArticulatedBus` | ["A long City Bus (FULL INTERIOR)"](https://sketchfab.com/3d-models/a-long-city-bus-full-interior-43d21ebcf51f45da9da0ccb0e6aad19a) | [Mixmamo.studio](https://sketchfab.com/mixmamo.studio) | `CityBus/` |
| `DrivableDafXF95` | ["DAF XF 95"](https://sketchfab.com/3d-models/daf-xf-95-1e61dfadf7114ad38076f1a6a871ccf1) | [Kabourek Roman](https://sketchfab.com/Krakatit) | `DafXF95/` |
| `DrivableBoxTruck` | ["Box-truck"](https://sketchfab.com/3d-models/box-truck-b0e8788b6b774fe3a18eda1895a2b8fe) | [Cristian Rus](https://sketchfab.com/CristianRus) | `BoxTruck/` |

## Changes made

The models themselves are unchanged in their folders. The vehicle builder (`Assets/Editor/VehicleBuilder.cs` and
`VehicleModelRig.cs`) makes the vehicles from them, with these changes:

- scaled to the real vehicle's length;
- wheels cut out of the body meshes so they can turn, and the rest of the body merged into one mesh per material
  (`Assets/Prefabs/Cars/Vehicles/Meshes/`);
- triangles of see-through materials whose texture is fully opaque given an opaque copy of the material;
- the bus's rear section cut off at the joint (it follows the front section as a trailer);
- left out: the bus's seated driver figure (the simulator's own driver takes the seat) and a helper object of the
  Land Rover Defender (`Object_669`);
- the simulator's lamps, displays, cameras and driver added.

## Trademarks

Toyota, Corolla, Land Rover, Defender, DAF and Volvo are trademarks of their owners. The models show these vehicles
for research purposes only; the owners of these trademarks do not endorse Coupled Sim.
