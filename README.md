# Food Process

Food Process is a RimWorld 1.6 mod that adds a centralized food-processing network.

Raw food and meals can be broken down into liquid toner, stored in connected tanks, and then printed back out as meals or animal feed through a shared pipe network. Ingredient provenance is preserved through the toner network, so printed food can still reflect meat, vegetable, and human-meat origin for downstream food-type checks.

## Features

- Food Disintegrator that converts adjacent food into toner
- Toner Tanks for shared network storage
- Food Printer that produces researched meal tiers from stored toner
- Animal Feeder that turns toner into kibble
- Nutrient feeder that directly feeds humanlike bed occupants from the toner network
- Toner pipes and underground pipes for network routing
- In-game mod settings for power use, print costs, tank capacity, and related balance values
- Harmony-based integration with RimWorld food-search logic and ingest jobs
- Pawn-aware printer filtering based on connected ingredient provenance
- Shared floor-level pipe rendering and architect-category overlay previews
- Optional compatibility patch for `Adamas.VendingMachines`

## Research Progression

The current research chain is:

- `Food Processing` -> requires `Electricity` and `Nutrient paste meal`
- `Simple Meal Printing` -> requires `Food Processing` and `Electricity`
- `Fine Meal Printing` -> requires `Simple Meal Printing` and `MicroelectronicsBasics`
- `Lavish Meal Printing` -> requires `Fine Meal Printing` and `MultiAnalyzer`

## How It Works

1. Place a `Food Disintegrator` next to stored or loose ingestible food.
2. Connect it to `Toner Tanks`, `Food Printers`, and `Animal Feeders` with toner pipes.
3. Disintegrated food adds toner and preserves its final ingestible ingredient defs in connected tanks.
4. Let colonists or prisoners use the `Food Printer`, let the `Animal Feeder` generate kibble automatically, or use a `Nutrient feeder` for adjacent beds.
5. Pawns evaluate printers against the connected network's current ingredient profile before selecting them as a food source.

## Power And Network Notes

- Functional buildings act as toner-network nodes.
- Functional buildings also transmit power, so they can bridge adjacent powered tiles like conduit-capable buildings.
- Toner tanks spoil their stored contents after extended power loss.
- Toner storage contributes to RimWorld's low-food accounting through the colony nutrition counter.
- Food printers respect research locks for meal tiers and current connected toner availability.
- Printer food-type prediction is based on stored ingredient provenance in connected toner tanks.

## Printer Food Rules

- Vegetarian restrictions are treated as hard blocks for incompatible printers.
- Meat and human-meat preferences can be used for printer ranking, and can optionally become hard blocks through the mod settings.
- If Ideology is disabled, or ideology resolution is unavailable for a pawn, printer policy falls back to neutral instead of blocking use.

## Mod Settings

- All settings include hover tooltips in the mod settings window.
- Main settings cover toner costs, feeder output, tank capacities, and power draw values.
- The `Debug` section is folded by default and currently contains:
  - `Debug log`
  - `Hard check food type`

## Developer Examples

- Developer-facing integration examples live under `Example/`.
- See `Example/README.md` for toner consumer, toner producer, embedded-preview, and custom place-worker examples.

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
