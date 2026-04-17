using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ScottPlot.Avalonia;

namespace csvplot;

public static class MainChart
{
}

public interface IDataSource
{
    Task<List<Trend>> Trends();

    Task<List<double>> GetData(string trend);

    string Header { get; }

    string ShortName { get; }

    Task<DataSourceType> DataSourceType();

    Task<TimestampData> GetTimestampData(string trend);

    Task<List<TimestampData>> GetTimestampData(List<string> trends);

    Task<List<TimestampData>> GetTimestampData(List<string> trends, DateTime startDateInc, DateTime endDateExc);

    async Task<TimeSeriesData> GetTimeSeriesData(string trend)
    {
        TimestampData timestampData = await GetTimestampData(trend);
        return TimeSeriesDataFactory.FromTimestampData(timestampData);
    }

    async Task<List<TimeSeriesData>> GetTimeSeriesData(List<string> trends)
    {
        List<TimestampData> timestampData = await GetTimestampData(trends);
        return timestampData.Select(TimeSeriesDataFactory.FromTimestampData).ToList();
    }

    async Task<List<TimeSeriesData>> GetTimeSeriesData(List<string> trends, DateTime startDateInc, DateTime endDateExc)
    {
        List<TimestampData> timestampData = await GetTimestampData(trends, startDateInc, endDateExc);
        return timestampData.Select(TimeSeriesDataFactory.FromTimestampData).ToList();
    }

    string GetScript(List<string> trends, DateTime startDateInc, DateTime endDateExc);

    public Task UpdateCache();
}

public interface IEditableTrendUnitSource
{
    Task SetUnit(Trend trend, string? unit);

    async Task SetUnits(IEnumerable<Trend> trends, string? unit)
    {
        foreach (Trend trend in trends)
        {
            await SetUnit(trend, unit);
        }
    }
}

public interface IEditableTrendTagSource
{
    Task SetTags(Trend trend, IReadOnlyList<string>? tags);

    async Task SetTags(IEnumerable<Trend> trends, IReadOnlyList<string>? tags)
    {
        foreach (Trend trend in trends)
        {
            await SetTags(trend, tags);
        }
    }
}

public enum GapState
{
    HasGaps,
    NoGaps,
    Unknown,
}

public abstract record TimeAxis;

public sealed record ExplicitTimeAxis(IReadOnlyList<DateTime> DateTimes) : TimeAxis;

public sealed record UniformTimeAxis(DateTime Start, TimeSpan Step, int Count) : TimeAxis;

public sealed record TimeSeriesData(TimeAxis Axis, IReadOnlyList<double> Values);

public sealed record GapPolicy(TimeSpan BreakThreshold)
{
    public static GapPolicy Default { get; } = new(TimeSpan.FromHours(1));
}

public abstract record PlotSeries;

public sealed record SignalPlotSeries(DateTime Start, TimeSpan Step, IReadOnlyList<double> Values) : PlotSeries;

public sealed record ScatterPlotSeries(IReadOnlyList<double> XsOaDate, IReadOnlyList<double> Ys) : PlotSeries;

public static class TimeSeriesDataFactory
{
    public static TimeSeriesData FromTimestampData(TimestampData timestampData)
    {
        return new TimeSeriesData(new ExplicitTimeAxis(timestampData.DateTimes.ToList()), timestampData.Values.ToList());
    }

    public static TimeSeriesData CreateUniform(DateTime start, TimeSpan step, IReadOnlyList<double> values)
    {
        return new TimeSeriesData(new UniformTimeAxis(start, step, values.Count), values.ToList());
    }

    public static TimeSeriesData CreateExplicit(IReadOnlyList<DateTime> dateTimes, IReadOnlyList<double> values)
    {
        return new TimeSeriesData(new ExplicitTimeAxis(dateTimes.ToList()), values.ToList());
    }

    public static TimeSeriesData CreateFromDateTimes(IReadOnlyList<DateTime> dateTimes, IReadOnlyList<double> values)
    {
        if (dateTimes.Count == values.Count && TryGetUniformAxis(dateTimes, out UniformTimeAxis? axis) && axis is not null)
        {
            return new TimeSeriesData(axis, values.ToList());
        }

        return CreateExplicit(dateTimes, values);
    }

    public static bool TryGetUniformAxis(IReadOnlyList<DateTime> dateTimes, out UniformTimeAxis? axis)
    {
        axis = null;
        if (dateTimes.Count < 2) return false;

        TimeSpan step = dateTimes[1] - dateTimes[0];
        if (step <= TimeSpan.Zero) return false;

        for (int i = 2; i < dateTimes.Count; i++)
        {
            if (dateTimes[i] - dateTimes[i - 1] != step)
            {
                return false;
            }
        }

        axis = new UniformTimeAxis(dateTimes[0], step, dateTimes.Count);
        return true;
    }
}

public static class SeriesAdapter
{
    public static PlotSeries ToPlotSeries(TimeSeriesData series, GapPolicy gapPolicy)
    {
        return series.Axis switch
        {
            UniformTimeAxis axis when !RequiresVisualGaps(axis, gapPolicy, series.Values.Count) && !series.Values.Any(double.IsNaN) =>
                new SignalPlotSeries(axis.Start, axis.Step, series.Values),
            UniformTimeAxis axis => CreateScatterPlotSeries(axis, series.Values, gapPolicy),
            ExplicitTimeAxis axis => CreateScatterPlotSeries(axis, series.Values, gapPolicy),
            _ => throw new InvalidOperationException($"Unsupported axis type: {series.Axis.GetType().Name}")
        };
    }

    private static bool RequiresVisualGaps(UniformTimeAxis axis, GapPolicy gapPolicy, int valueCount)
    {
        return valueCount != axis.Count || axis.Step > gapPolicy.BreakThreshold;
    }

    private static ScatterPlotSeries CreateScatterPlotSeries(UniformTimeAxis axis, IReadOnlyList<double> values, GapPolicy gapPolicy)
    {
        List<double> xs = new(values.Count);
        List<double> ys = new(values.Count);

        if (values.Count == 0)
        {
            return new ScatterPlotSeries(xs, ys);
        }

        DateTime current = axis.Start;
        xs.Add(current.ToOADate());
        ys.Add(values[0]);

        for (int i = 1; i < values.Count; i++)
        {
            DateTime next = axis.Start.AddTicks(axis.Step.Ticks * i);
            if (next - current > gapPolicy.BreakThreshold)
            {
                xs.Add(new DateTime((next.Ticks + current.Ticks) / 2).ToOADate());
                ys.Add(double.NaN);
            }

            xs.Add(next.ToOADate());
            ys.Add(values[i]);
            current = next;
        }

        return new ScatterPlotSeries(xs, ys);
    }

    private static ScatterPlotSeries CreateScatterPlotSeries(ExplicitTimeAxis axis, IReadOnlyList<double> values, GapPolicy gapPolicy)
    {
        List<double> xs = new();
        List<double> ys = new();
        int count = Math.Min(axis.DateTimes.Count, values.Count);
        if (count == 0)
        {
            return new ScatterPlotSeries(xs, ys);
        }

        xs.Add(axis.DateTimes[0].ToOADate());
        ys.Add(values[0]);

        for (int i = 1; i < count; i++)
        {
            DateTime previous = axis.DateTimes[i - 1];
            DateTime current = axis.DateTimes[i];
            if (current - previous > gapPolicy.BreakThreshold)
            {
                xs.Add(new DateTime((current.Ticks + previous.Ticks) / 2).ToOADate());
                ys.Add(double.NaN);
            }

            xs.Add(current.ToOADate());
            ys.Add(values[i]);
        }

        return new ScatterPlotSeries(xs, ys);
    }
}

public class TimestampData
{
    public readonly List<DateTime> DateTimes;
    public readonly List<double> Values;

    public TimestampData(List<DateTime> dateTimes, List<double> values)
    {
        DateTimes = dateTimes;
        Values = values;
    }

    public bool Gapped { get; private set; } = false;

    public GapState HasGaps = GapState.Unknown;

    public void AddGaps()
    {
        if (Gapped) return;
        for (int i = 1; i < DateTimes.Count; i++)
        {
            if ((DateTimes[i] - DateTimes[i - 1]).Ticks > TimeSpan.TicksPerHour)
            {
                DateTimes.Insert(i, new DateTime( (DateTimes[i].Ticks + DateTimes[i - 1].Ticks) / 2));
                Values.Insert(i, double.NaN);
                i++;
                HasGaps = GapState.HasGaps;
            }
        }

        if (HasGaps == GapState.Unknown) HasGaps = GapState.NoGaps;
        Gapped = true;
    }

    public bool LengthsEqual => DateTimes.Count == Values.Count;

    public void Sort()
    {
        var zipped = DateTimes.Zip(Values, (dt, v) => (dt, v)).ToList();
        zipped.Sort((a, b) => a.dt.CompareTo(b.dt));
        DateTimes.Clear();
        Values.Clear();
        foreach (var (dt, v) in zipped)
        {
            DateTimes.Add(dt);
            Values.Add(v);
        }
    }

    public void TrimDates(DateTime startDateInc, DateTime endDateExc)
    {
        for (int i = Math.Min(DateTimes.Count, Values.Count) - 1; i >= 0; i--)
        {
            if (DateTimes[i] < startDateInc || DateTimes[i] >= endDateExc)
            {
                DateTimes.RemoveAt(i);
                Values.RemoveAt(i);
            }
        }
    }

    public void AlignToMinuteInterval(DateTime startDateInc, DateTime endDateExc, int minuteInterval)
    {
        List<DateTime> interpolatedDateTimes = new List<DateTime>();
        List<double> newValues = new();

        DateTime currentDate = startDateInc;
        int currentIndex = 0;

        while (currentDate < endDateExc)
        {
            if (!(TimeZoneInfo.Local.IsAmbiguousTime(currentDate) || TimeZoneInfo.Local.IsInvalidTime(currentDate)))
            {
                interpolatedDateTimes.Add(currentDate);
                while (true)
                {
                    if (currentIndex >= Values.Count) break;
                    if (DateTimes[currentIndex] >= currentDate) break;
                    currentIndex++;
                }

                if (currentIndex >= Values.Count)
                {
                    newValues.Add(double.NaN);
                }
                else if (currentIndex == 0)
                {
                    // Check if right on border.
                    newValues.Add(currentDate == DateTimes[currentIndex] ? Values[currentIndex] : double.NaN);
                }
                else
                {
                    var slope = (Values[currentIndex] - Values[currentIndex - 1]) / ((double)DateTimes[currentIndex].Ticks - DateTimes[currentIndex - 1].Ticks);
                    var value = Values[currentIndex - 1] + (currentDate.Ticks -  DateTimes[currentIndex - 1].Ticks) * slope;
                    newValues.Add(value);
                }
            }

            currentDate = currentDate.AddTicks(minuteInterval * TimeSpan.TicksPerMinute);
        }

        DateTimes.Clear();
        DateTimes.AddRange(interpolatedDateTimes);
        Values.Clear();
        Values.AddRange(newValues);
        if (interpolatedDateTimes.Count != newValues.Count)
        {
            throw new InvalidDataException();
        }
    }
}
