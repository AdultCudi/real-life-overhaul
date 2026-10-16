# Districts

## In a sentence

The district view on the city map: ten layers in its filter list tint each district, with a key, a card and the districts in order.

## Where the code is

- Folder: [`Scripts/Districts`](../../Assets/Mods/RealLifeOverhaul/Scripts/Districts/).
- Read first: `DistrictRules.cs` (the layers, bands and colours, game-free: EconomyCheck runs it), `DistrictView.cs` (the one component) and `DistrictBridges.cs`.

| File | What it is |
| --- | --- |
| `DistrictRules` | the layers, their scales and five bands, the colours, the key's ranges; DistrictLog, the crimes reported by district (game-free: EconomyCheck runs it) |
| `DistrictBridges` | what it asks of the others, one reading per district (Wiring/DistrictLinks fills them in) |
| `DistrictGame` | everything it touches of the game: the map's district zones and their colour, the figures from the buildings, the mouse on the map |
| `DistrictPanel` | its window, a copy of the map's filter window beside it: the key, the card, the districts |
| `DistrictView` | the one component: the layer ticked (Core/MapPlaces.RegisterChoice), the map's opening and the day, the tint, the card, the crime log; the test buttons |
| `DistrictGuide` | its options block and its feature entry (Guide figures from the rules) |

## How it talks to other features

- `Wiring/DistrictLinks.cs` gives one reading per district: the city's economy's income and footfall, Standing, the player's power and water and internet, and the council's building sites and voters.
- The layers are rows of the map's filter list through `Core/MapPlaces.RegisterChoice`; see [Map places](../reference/map-places.md).
