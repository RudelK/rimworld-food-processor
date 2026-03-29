# Food Process Integration Examples

These files are developer-facing examples for connecting custom buildings to the Food Process toner pipe network.

They are intentionally stored outside `Source/` and `Common/Defs/` so they are not:

- compiled into `FoodProcess.dll`
- auto-loaded by RimWorld
- treated as live mod content

Copy and adapt the snippets into your own mod if you want to build toner-network-aware buildings.

## Verified Integration Path

The current supported pattern in this codebase is:

1. Add `FoodPrinterSystem.CompProperties_TonerNode` to your building def so the building joins the toner pipe network through `CompTonerNode : CompPipe`.
2. Add `FoodSystemPipe.PlaceWorker_EmbeddedPipePreview` if the building should show pipe-aware ghost overlays and duplicate-pipe placement validation.
3. Use `TonerPipeNetManager.GetSummary(...)` for inspect/status text.
4. Use `TonerPipeNetManager.TryDrawToner(...)` for consumers.
5. Use `TonerPipeNetManager.TryAddToner(...)` for producers.
6. Use `TonerPipeNetManager.DistributeIngredients(...)` when a producer should contribute ingredient provenance.

`Building_EmbeddedPipeMachine` exists in the source, but the shipped buildings currently use `CompProperties_TonerNode` directly. These examples intentionally teach that same pattern.

If you choose to subclass `Building_NutrientPasteDispenser` for vanilla food-search compatibility, do not blindly pass through `base.GetGizmos()`. Other dispenser mods may inject inherited gizmos onto your building, so you should explicitly filter or whitelist the commands you want to keep.

## Example Files

`CSharp/ExampleTonerConsumer.cs`

- minimal consumer building
- consumes toner on `TickRare`
- shows the connected network summary in inspect text

`CSharp/ExampleTonerProducer.cs`

- minimal producer building
- adds toner on `TickRare`
- contributes final ingestible ingredient provenance with `DistributeIngredients(...)`

`CSharp/ExampleEmbeddedTonerConsumer.cs`

- consumer example for a larger building that visually participates in the embedded-pipe preview flow
- demonstrates that the embedded behavior comes from XML comps and place workers, not a special runtime base class

`CSharp/ExamplePlaceWorker_AdjacentTonerNode.cs`

- custom place worker that subclasses `FoodSystemPipe.PlaceWorker_EmbeddedPipePreview`
- keeps the existing pipe-aware preview behavior
- adds one extra placement rule: the building must be placed next to existing toner infrastructure

## XML Examples

`XML/ExampleTonerConsumer.xml`

- basic consumer def using `CompProperties_TonerNode`

`XML/ExampleTonerProducer.xml`

- basic producer def using `CompProperties_TonerNode`

`XML/ExampleEmbeddedTonerConsumer.xml`

- multi-cell consumer that uses `PlaceWorker_EmbeddedPipePreview`

`XML/ExampleEmbeddedTonerConsumer_WithPlaceWorker.xml`

- same general embedded example, but wired to the custom place worker sample

## Sample Translation Key

If you copy the custom place worker example into a live mod, add a keyed string like this to your language file:

```xml
<LanguageData>
  <FPS_Example_MustPlaceAdjacentToTonerNode>Must be placed next to existing toner infrastructure.</FPS_Example_MustPlaceAdjacentToTonerNode>
</LanguageData>
```

## Notes

- These snippets are examples, not framework APIs.
- The examples use `ThingDefOf.RawPotatoes` for provenance because it is a final ingestible ingredient def.
- If provenance matters for your building, do not convert ingredients into butcher source defs, race defs, or parent defs.
- The producer example increments its success counter only after `TryAddToner(...)` succeeds, then distributes provenance, matching the current runtime order used in the main mod.
