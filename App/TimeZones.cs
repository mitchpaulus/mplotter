using System;
using System.Collections.Generic;
using System.Linq;

namespace CCLLCDataSync;

public class TimeZones
{
    public readonly Dictionary<string, TimeZoneInfo> AvailableTimeZones;

    private TimeZoneInfo? _central = null;
    private TimeZoneInfo? _utc = null;

    public TimeZones()
    {
        AvailableTimeZones = TimeZoneInfo.GetSystemTimeZones().ToDictionary(info => info.Id, info => info);
    }

    public TimeZoneInfo Central()
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

    public TimeZoneInfo Utc()
    {
        if (_utc is not null) return _utc;

        if (AvailableTimeZones.TryGetValue("Etc/UTC", out var info))
        {
            _utc = info;
            return info;
        }

        if (AvailableTimeZones.TryGetValue("UTC", out info))
        {
            _utc = info;
            return info;
        }

        throw new TimeZoneNotFoundException("UTC Time Zone not found.");
    }
}
