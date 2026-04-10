using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace csvplot;

public static class TimeZones
{
    public static readonly Dictionary<string, TimeZoneInfo> AvailableTimeZones;

    private static TimeZoneInfo? _central = null;
    // private static TimeZoneInfo? _utc = null;

    static TimeZones()
    {
        AvailableTimeZones = TimeZoneInfo.GetSystemTimeZones().ToDictionary(info => info.Id, info => info);
    }

    public static TimeZoneInfo Central()
    {
        if (_central is not null) return _central;

        // First try 'America/Chicago' for Linux
        if (AvailableTimeZones.TryGetValue("America/Chicago", out var info))
        {
            _central = info;
            return info;
        }

        if (AvailableTimeZones.TryGetValue("Central Standard Time", out info))
        {
            _central = info;
            return info;
        }

        throw new TimeZoneNotFoundException("Central Time Zone not found.");
    }

    public static TimeZoneInfo Utc() => TimeZoneInfo.Utc;
}

public readonly struct UtcDateTime : IFormattable
{
    public DateTime Value { get; }

    public UtcDateTime(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Expected UTC DateTime.", nameof(value));

        Value = value;
    }

    public static UtcDateTime Now() => new(DateTime.UtcNow);

    public string ToRfc3339Second() => Value.ToString("yyyy-MM-dd'T'HH:mm:ssK");

    public string ToString(string? format, IFormatProvider? formatProvider) => Value.ToString(format, formatProvider);

    public override string ToString() => Value.ToString(CultureInfo.CurrentCulture);
}

public readonly struct LocalDateTime : IFormattable
{
    public DateTime Value { get; }

    public LocalDateTime(DateTime value)
    {
        if (value.Kind != DateTimeKind.Local)
            throw new ArgumentException("Expected local DateTime.", nameof(value));

        Value = value;
    }

    public static LocalDateTime Now() => new(DateTime.Now);

    public string ToString(string? format, IFormatProvider? formatProvider) => Value.ToString(format, formatProvider);

    public override string ToString() => Value.ToString(CultureInfo.CurrentCulture);
}
