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
    Task<List<string>> Trends();

    Task<List<double>> GetData(string trend);

    string Header { get; }

    string ShortName { get; }

    Task<DataSourceType> DataSourceType();

    Task<TimestampData> GetTimestampData(string trend);

    Task<List<TimestampData>> GetTimestampData(List<string> trends);

    Task<List<TimestampData>> GetTimestampData(List<string> trends, DateTime startDateInc, DateTime endDateExc);

    string GetScript(List<string> trends, DateTime startDateInc, DateTime endDateExc);

    public Task UpdateCache();
}

public enum GapState
{
    HasGaps,
    NoGaps,
    Unknown,
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
