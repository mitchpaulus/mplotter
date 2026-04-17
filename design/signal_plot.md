# Utilizing Signal Plots

For several of our data streams, we may be able to utilize signal plots in ScottPlot for performance reasons.

## Problem

The current decision about whether to use a ScottPlot `Signal` series is made in the view-model layer with source-level heuristics:

- EnergyPlus sources are assumed to be signal-friendly
- series with `8760` points are assumed to be signal-friendly
- `TimestampData.AddGaps()` mutates the data before plotting, which can destroy information about whether the original series was uniformly sampled

This is workable for a few special cases, but it is the wrong abstraction boundary. Uniform sampling is a property of an individual series instance, not of an `IDataSource`.

Examples:

- a TSV file may contain evenly spaced hourly data and should be eligible for `Signal`
- another TSV file may have missing rows or irregular timestamps and should remain a scatter plot
- a datasource may be able to return one trend as uniform and another as irregular

## Recommendation

Keep `IDataSource` responsible for returning semantic trend data, but make the returned series shape explicit.

Do not push ScottPlot plottable types down into `IDataSource`.
Instead, return a richer application-level series object that can describe either:

- explicit timestamps
- uniform sampling metadata

The plotting layer can then map that object to ScottPlot `Scatter` or `Signal`.

Gaps are purely visual and should not be stored on the canonical series object.

## Proposed Model

Replace or evolve `TimestampData` into a shape that can represent both irregular and uniform time axes.

```csharp
public abstract record TimeAxis;

public sealed record ExplicitTimeAxis(IReadOnlyList<DateTime> DateTimes) : TimeAxis;

public sealed record UniformTimeAxis(
    DateTime Start,
    TimeSpan Step,
    int Count) : TimeAxis;

public sealed record TimeSeriesData(
    TimeAxis Axis,
    IReadOnlyList<double> Values);
```

This can also be expressed with `OneOf`/`Unio`, but a small closed record hierarchy is probably simpler and more idiomatic here.

## Why This Shape

It preserves the distinction between:

- "these timestamps happened to be equally spaced"
- "this series is fundamentally represented as evenly spaced samples"

That matters because ScottPlot `Signal` needs:

- a constant sample period
- no visual gaps represented by `NaN`
- an `xOffset` and fixed sample period rather than a full X array

With an explicit `UniformTimeAxis`, the renderer no longer needs to guess.

## Rendering Boundary

Add a plotting adapter in the UI layer that converts `TimeSeriesData` into plot-ready series.

```csharp
public sealed record GapPolicy(TimeSpan BreakThreshold);

public abstract record PlotSeries;

public sealed record SignalPlotSeries(
    DateTime Start,
    TimeSpan Step,
    IReadOnlyList<double> Values) : PlotSeries;

public sealed record ScatterPlotSeries(
    IReadOnlyList<double> XsOaDate,
    IReadOnlyList<double> Ys) : PlotSeries;

public static class SeriesAdapter
{
    public static PlotSeries ToPlotSeries(TimeSeriesData series, GapPolicy gapPolicy)
    {
        throw new NotImplementedException();
    }
}
```

`SeriesAdapter` is the layer that applies the current global gap rule and decides whether the plot-ready representation is signal-friendly or scatter-with-breaks.

This keeps ScottPlot-specific knowledge out of datasource implementations and out of `TimeSeriesData`.

## Datasource Expectations

Each datasource should return the most precise axis description it knows.

Examples:

- EnergyPlus hourly output:
  return `UniformTimeAxis(start, 1 hour, count)`
- NOAA hourly weather:
  likely `ExplicitTimeAxis`, unless we explicitly normalize and prove no missing records
- Influx:
  generally `ExplicitTimeAxis`
- TSV:
  detect whether the timestamps are uniformly spaced after parsing and trimming; if so, return `UniformTimeAxis`

If a returned series is uniform, it should be marked `UniformTimeAxis` immediately by the datasource.
`ExplicitTimeAxis` should never be promoted later during rendering.

## Detection Rules

For parsed timestamped data, uniformity detection should happen close to where the series is built, before any rendering-specific gap insertion.

Suggested rule:

1. timestamps must be strictly increasing
2. there must be at least 2 samples
3. all deltas must be equal
If all of those hold, return `UniformTimeAxis`.

Use exact `Ticks` equality first. If some data sources produce round-trip parsing noise, we can later add a tolerance-based path, but exact spacing is the safer default.

## Mutation Concern

`TimestampData.AddGaps()` is a rendering-oriented mutation. That is the main thing fighting this design.

Recommended change:

- stop mutating the canonical series object to add gaps
- instead, apply the current global gap policy in `SeriesAdapter`
- derive scatter-only rendering data when the visual gap rule requires line breaks

For example:

```csharp
public static PlotSeries ToPlotSeries(TimeSeriesData series, GapPolicy gapPolicy)
```

That preserves the original uniform-series information for signal rendering while keeping gap behavior global and dynamic.

## Interface Impact

The current `IDataSource` surface can stay mostly the same, but the return type should change from `TimestampData` to the richer series type.

```csharp
Task<TimeSeriesData> GetTimeSeriesData(string trend);
Task<List<TimeSeriesData>> GetTimeSeriesData(List<string> trends);
Task<List<TimeSeriesData>> GetTimeSeriesData(List<string> trends, DateTime startDateInc, DateTime endDateExc);
```

If we want a low-risk migration path, we can:

1. add the new methods alongside `GetTimestampData`
2. add `SeriesAdapter` and `PlotSeries`
3. update plotting to consume the new model
3. remove `GetTimestampData` later

## Why Not Put This On IDataSource Directly

Avoid datasource-level flags like:

- `SupportsSignal`
- `PreferredPlotType`

Those flags are too coarse. They fail for mixed datasets, especially delimited files and any source where filtering or trimming may change whether a given returned series is still uniformly sampled.

## OneOf / Unio vs Interfaces

Either approach works, but the tradeoff is:

- `OneOf`/`Unio`:
  explicit and exhaustive, but adds library-level ceremony
- record hierarchy:
  simpler, native C#, and enough for this small closed set

Recommendation:

Use a small record hierarchy unless we already want union types elsewhere in the codebase.

## Practical Migration Plan

1. Introduce `TimeAxis`, `ExplicitTimeAxis`, `UniformTimeAxis`, and `TimeSeriesData`
2. Add `GapPolicy`, `PlotSeries`, and `SeriesAdapter`
3. Add uniform-spacing detection helpers near the existing `TimestampData` construction paths
4. Update EnergyPlus and obviously uniform sources first
5. Update the plot rendering path to choose `Signal` vs `Scatter` from `PlotSeries`
6. Remove the current source-type and point-count heuristics from `MainViewModel`
7. Remove `AddGaps()` from the canonical data path

## Expected Result

After this change, signal-plot support becomes:

- series-level instead of datasource-level
- explicit instead of heuristic
- compatible with mixed files such as TSV
- driven by a global visual gap policy
- easier to extend later to other ScottPlot-optimized series types
