# Food Process

Food Process is a RimWorld 1.6 mod that adds a centralized food-processing network.

Raw food and meals can be broken down into liquid toner, stored in connected tanks, and then printed back out as meals or animal feed through a shared pipe network.

## Features

- Food Disintegrator that converts adjacent food into toner
- Toner Tanks for shared network storage
- Food Printer that produces researched meal tiers from stored toner
- Animal Feeder that turns toner into kibble
- Toner pipes and underground pipes for network routing
- In-game mod settings for power use, print costs, tank capacity, and related balance values
- Harmony-based integration with RimWorld food-search logic
- Optional compatibility patch for `Adamas.VendingMachines`

## Research Progression

The current research chain is:

- `Food Processing` -> requires `Electricity`
- `Simple Meal Printing` -> requires `Food Processing` and `Electricity`
- `Fine Meal Printing` -> requires `Simple Meal Printing` and `MicroelectronicsBasics`
- `Lavish Meal Printing` -> requires `Fine Meal Printing` and `MultiAnalyzer`

## How It Works

1. Place a `Food Disintegrator` next to stored or loose ingestible food.
2. Connect it to `Toner Tanks`, `Food Printers`, and `Animal Feeders` with food pipes.
3. Store toner in the network.
4. Let colonists or prisoners use the `Food Printer`, or let the `Animal Feeder` generate kibble automatically.

## Power And Network Notes

- Functional buildings act as toner-network nodes.
- Functional buildings also transmit power, so they can bridge adjacent powered tiles like conduit-capable buildings.
- Toner tanks spoil their stored contents after extended power loss.
- Food printers respect research locks for meal tiers.

## Compatibility

Required:

- RimWorld 1.6
- Harmony (`brrainz.harmony`)

Optional:

- `Adamas.VendingMachines` for the included compatibility patch in `ModSupport/VendingMachines`

## Repository Layout

```text
About/                  Mod metadata
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
