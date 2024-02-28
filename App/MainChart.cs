using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
}
