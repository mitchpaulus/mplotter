# National Weather Service Data

Our NOAA stream has stopped pushing data.
The National Weather Service has their own API that can work.

It is relatively slow on initial requests, and historical data is unlikely to change, so it makes sense to apply strong caching.

I want to store data into a sqlite database.
The name should be `mplotter.db` in the same location as the rest of the MPlotter files,
normally `%LOCALAPPDATA%/mplotter` on Windows.

Table schema:

Table: NwsRequests

```
TimestampUtc DateTime (2026-04-17T13:25:00Z)
StationId Str
StartDateIncUtc DateTime
EndDateExcUtc DateTime
```

Table: NwsData

```
StationId
TimestampUtc
TemperatureDegC
DewpointDegC
RelativeHumidity
WindSpeedKmh
WindDirectionDeg
```

(StationId, TimestampUtc) should be unique primary key

We will need a dedicated internal library for the NWS API. This should be robust to network failures/timeouts.
Each request is typically limited to 500 items at a time.

Present data in user's local timestamp for now.
