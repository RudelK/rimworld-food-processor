# Food Process

Food Process is a RimWorld 1.6 mod that adds a centralized food-processing network.

Raw food and meals can be broken down into liquid toner, stored in connected tanks, and then printed back out as meals or animal feed through a shared pipe network. Ingredient provenance is preserved through the toner network, so printed food can still reflect meat, vegetable, and human-meat origin for downstream food-type checks.

## Features

- INGR Disintegrator that converts adjacent food into toner
- Small, medium, and large Toner Tanks for shared network storage
- Food Printer that produces researched meal tiers from stored toner without nutrient paste hoppers
- Animal Feeder that turns toner into kibble
- Nutrient feeder that directly feeds humanlike bed occupants from the toner network
- Toner pipes and underground toner pipes for network routing
- Pipe-specific build and deconstruct tools
- Grouped toner tank build menu with size-based research progression
- In-game mod settings for power use, print costs, tank capacity, and related balance values
- Harmony-based integration with RimWorld food-search logic and ingest jobs
- Pawn-aware printer filtering based on connected ingredient provenance
- Dynamic discovery of valid modded meal defs for simple, fine, and lavish meal tiers
- Shared floor-level pipe rendering and architect-category overlay previews
- Persistent right-side warning when toner-consuming buildings are not connected to toner tank storage
- Live localization for English, Korean, Japanese, and Simplified Chinese
- Optional compatibility patch for `Adamas.VendingMachines`

## Research Progression

The current research chain is:

- `Food Processing` -> requires `Electricity` and `Nutrient paste meal`
- `Simple Meal Printing` -> requires `Food Processing` and `Electricity`
- `Fine Meal Printing` -> requires `Simple Meal Printing` and `MicroelectronicsBasics`
- `Lavish Meal Printing` -> requires `Fine Meal Printing` and `MultiAnalyzer`

Toner tank unlocks are:

- `Small toner tank` -> requires `Food Processing`
- `Medium toner tank` -> requires `Food Processing` and `Machining`
- `Large toner tank` -> requires `Food Processing` and `MicroelectronicsBasics`

## How It Works

1. Place a `Food Disintegrator` next to stored or loose ingestible food.
2. Connect it to `Toner Tanks`, `Food Printers`, `Animal Feeders`, and `Nutrient feeders` with toner pipes.
3. Disintegrated food adds toner and preserves its final ingestible ingredient defs in connected tanks.
4. Let colonists or prisoners use the `Food Printer`, let the `Animal Feeder` generate kibble automatically, or use a `Nutrient feeder` for adjacent beds.
5. Pawns evaluate printers against the connected network's current ingredient profile before selecting them as a food source.
6. The printer can automatically include valid modded meal defs from loaded mods, as long as they are real meal ingestibles and match an unlocked meal tier.

## Power And Network Notes

- Functional buildings act as toner-network nodes.
- Functional buildings also transmit power, so they can bridge adjacent powered tiles like conduit-capable buildings.
- Toner tanks spoil their stored contents after extended power loss.
- Toner storage contributes to RimWorld's low-food accounting through the colony nutrition counter.
- Food printers use the toner network as their real input source and no longer rely on nutrient paste hoppers.
- Food printers respect research locks for meal tiers and current connected toner availability.
- Printer food-type prediction is based on stored ingredient provenance in connected toner tanks.
- Printer random selection excludes meal defs directly forbidden by the current pawn food policy.
- Printer meal discovery excludes non-meal ingestibles such as insect jelly even if their preferability is high.
- Toner-consuming buildings show a persistent `Need to connect toner tank` warning when they are not connected to a toner network with at least one tank.
- `Deconstruct pipes` only targets placed toner pipes and ignores walls, machines, and other non-pipe buildings that merely share pipe visuals.

## Printer Food Rules

- Vegetarian restrictions are treated as hard blocks for incompatible printers.
- Meat and human-meat preferences can be used for printer ranking, and can optionally become hard blocks through the mod settings.
- If Ideology is disabled, or ideology resolution is unavailable for a pawn, printer policy falls back to neutral instead of blocking use.
- Printer category defaults follow current research state, so unavailable tiers are not shown as enabled before they are unlocked.
- Printer mask color follows the room used at the printer interaction cell, and updates when a linked bed switches between colonist and prisoner use.

## Mod Settings

- All settings include hover tooltips in the mod settings window.
- Main settings cover toner costs, feeder output, tank capacities, and power draw values.
- A `Reset to defaults` button restores the mod's settings to their default values.
- The `Debug` section is folded by default and currently contains:
  - `Debug log`
  - `Hard check food type`

## Developer Examples

- Developer-facing integration examples live under `Example/`.
- See `Example/README.md` for toner consumer, toner producer, embedded-preview, and custom place-worker examples.
- The example set includes a custom `PlaceWorker_EmbeddedPipePreview` subclass pattern for buildings that need extra placement rules.

## Localization

- Live keyed and DefInjected translations are included for:
  - English
  - Korean
  - Japanese
  - Simplified Chinese

## Compatibility

Required:

- RimWorld 1.6
- Harmony (`brrainz.harmony`)

Optional:

- `Adamas.VendingMachines` for the included compatibility patch in `ModSupport/VendingMachines`

## Repository Layout

```text
About/                  Mod metadata
Example/                Developer-facing C# and XML integration examples
Common/                 Shared defs, languages, textures, and XML content
1.6/                    Version-specific output folder and assemblies
ModSupport/             Optional compatibility patches
Source/FoodPrinterSystem/ C# source project
LoadFolders.xml         RimWorld load folder mapping
food print system.sln   Visual Studio solution
```

## Building The Mod

The C# project is:

- `Source/FoodPrinterSystem/FoodProcess.csproj`

Release build:

```powershell
dotnet build .\Source\FoodPrinterSystem\FoodProcess.csproj -c Release
```

Output assembly:

- `1.6/Assemblies/FoodProcess.dll`

The project expects a local RimWorld install so it can reference `Assembly-CSharp.dll` and Unity assemblies. Harmony is referenced from a local RimWorld or Workshop install.

## Installation

1. Copy the mod folder into your RimWorld `Mods` directory.
2. Make sure Harmony loads before Food Process.
3. Enable the mod in RimWorld.

## License

See `LICENSE`.
