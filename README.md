# Food Process

Food Process is a RimWorld 1.6 mod that adds a toner-based food processing network.

Raw food is disintegrated into toner, stored in connected tanks, and reused by printers and feeders. Ingredient provenance is preserved through the network so printed food can still participate in food-type checks.

## Core Features

- `INGR Disintegrator` converts adjacent raw food into toner in batches of up to 3 items per cycle
- `Toner Tanks` store shared toner and preserve ingredient provenance
- `Food Printer` prints researched meal tiers from toner without nutrient paste hoppers
- `Animal Feeder` converts toner into kibble and has a per-building production toggle
- `Nutrient Feeder` automatically feeds linked bed occupants from the toner network
- Toner pipes, underground pipes, build/deconstruct pipe tools, and architect overlay support
- Mod settings for balance values, printer options, and external mod meal control

## How It Works

1. Place an `INGR Disintegrator` next to raw food.
2. Connect it to `Toner Tanks`, `Food Printers`, `Animal Feeders`, and `Nutrient Feeders` with toner pipes.
3. Disintegrated food adds toner and ingredient provenance to the connected network.
4. Printers and feeders consume toner from that shared network.

## Important Behavior

- Food printers are valid food sources for humanlike pawns and integrate into vanilla ingest/search flow.
- Printer meal discovery excludes non-printable meal-like ingestibles such as insect jelly, pemmican, and survival meals.
- External mod meals can be enabled or disabled from mod settings.
- If `Enable random meal selection` is off, external mod meal defs are hidden from printer output lists.
- Default printer toner costs are `6 / 10 / 14 / 20` for paste / simple / fine / lavish.
- Food printers filter inherited dispenser gizmos so external nutrient-paste-dispenser mod gizmos do not appear on printer UI.
- Toner-consuming buildings show a warning when they are not connected to a network with tank storage.

## Research

- `Food Processing`
- `Simple Meal Printing`
- `Advanced Food Processing`
- `Fine Meal Printing`
- `Lavish Meal Printing`
- `Expanded Toner Storage`

## Mod Settings

- Toner costs, power draw, feeder output, and tank capacities
- `Printer Options` external meal picker with search and per-def toggles
- Persistent pruning-safe storage for disabled external meal defs
- Folded `Debug` section with `Debug log` and `Hard check food type`

## Compatibility

Required:

- RimWorld 1.6
- Harmony (`brrainz.harmony`)

Optional:

- `Adamas.VendingMachines`

## Developer Notes

- Integration examples live under `Example/`
- Source project: `Source/FoodPrinterSystem/FoodProcess.csproj`
- Output assembly: `1.6/Assemblies/FoodProcess.dll`

Build:

```powershell
dotnet build .\Source\FoodPrinterSystem\FoodProcess.csproj -c Release
```

## Installation

1. Copy the mod folder into RimWorld's `Mods` directory.
2. Load Harmony before Food Process.
3. Enable the mod in-game.

## License

See `LICENSE`.
