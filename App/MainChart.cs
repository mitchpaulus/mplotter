using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

    List<double> GetData(string trend);

    string Header { get; }

    string ShortName { get; }

    DataSourceType DataSourceType { get; }

    TimestampData GetTimestampData(string trend);

    List<TimestampData> GetTimestampData(List<string> trends, DateTime startDateInc, DateTime endDateExc);
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
            interpolatedDateTimes.Add(currentDate);
            while (true)
            {
                if (currentIndex >= Values.Count) break;
                if (DateTimes[currentIndex] >= currentDate) break;
                currentIndex++;
            }

            if (currentIndex == 0)
            {
                // Check if right on border.
                newValues.Add(currentDate == DateTimes[currentIndex] ? Values[currentIndex] : double.NaN);
            }
            else if (currentIndex >= Values.Count)
            {
                newValues.Add(double.NaN);
            }
            else
            {
                var slope = (Values[currentIndex] - Values[currentIndex - 1]) / ((double)DateTimes[currentIndex].Ticks - DateTimes[currentIndex - 1].Ticks);
                var value = Values[currentIndex - 1] + (currentDate.Ticks -  DateTimes[currentIndex - 1].Ticks) * slope;
                newValues.Add(value);
            }

            currentDate = currentDate.AddTicks(minuteInterval * TimeSpan.TicksPerMinute);
        }

        DateTimes.Clear();
        DateTimes.AddRange(interpolatedDateTimes);
        Values.Clear();
        Values.AddRange(newValues);
    }
}
