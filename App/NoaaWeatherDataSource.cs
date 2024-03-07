using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace csvplot;

public record NoaaStation
{
    // USAF = Air Force station ID. May contain a letter in the first position. Cols 1-6
    // WBAN = NCDC WBAN number. Cols 8-12
    // STATION NAME = Name of station. Cols 14-42
    // CTRY = FIPS country ID (see country-list.txt). Cols 44-47
    // ST = State for US stations Cols 49-50
    // ICAO = ICAO ID 4-letter station identifier. Cols 52-56
    // LAT = Latitude in thousandths of decimal degrees Cols 58-64
    // LON = Longitude in thousandths of decimal degrees Cols 66-73
    // ELEV = Elevation in meters Cols 75-81
    // BEGIN = Beginning Period Of Record (YYYYMMDD). There may be reporting gaps within the P.O.R. Cols 83-90
    // END = Ending Period Of Record (YYYYMMDD). There may be reporting gaps within the P.O.R. Cols 92-99
    public string Usaf { get; set; }
    public string Wban { get; set; }
    public string StationName { get; set; }
    public string Ctry { get; set; }
    public string St { get; set; }
    public string Icao { get; set; }
    public double? Lat { get; set; }
    public double? Lon { get; set; }
    public double? Elev { get; set; }
    public DateTime Begin { get; set; }
    public DateTime End { get; set; }
}


public class NoaaWeather
{
    public static double? ParseFloat(string input) => double.TryParse(input, out var parsed) ? parsed : null;

    public static async Task<List<NoaaStation>> GetStations()
    {
        // Download from: https://www1.ncdc.noaa.gov/pub/data/noaa/isd-history.txt

        var stations = new List<NoaaStation>();

        HttpClient client = new HttpClient();
        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync("https://www1.ncdc.noaa.gov/pub/data/noaa/isd-history.txt");
        }
        catch
        {
            return new List<NoaaStation>();
        }

        if (!response.IsSuccessStatusCode)
        {
            return new List<NoaaStation>();
        }

        // Read file as UTF-8
        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (await reader.ReadLineAsync() is { } line)
        {
            if (line.Length < 99)
            {
                continue;
            }


            var station = new NoaaStation
            {
                Usaf = line.Substring(0, 6).Trim(),
                Wban = line.Substring(7, 5).Trim(),
                StationName = line.Substring(13, 29).Trim(),
                Ctry = line.Substring(43, 4).Trim(),
                St = line.Substring(48, 2).Trim(),
                Icao = line.Substring(51, 4).Trim(),
                Lat = ParseFloat(line.Substring(57, 7).Trim()),
                Lon = ParseFloat(line.Substring(65, 8).Trim()),
                Elev = ParseFloat(line.Substring(74, 7).Trim()),
                Begin = DateTime.ParseExact(line.Substring(82, 8).Trim(), "yyyyMMdd", CultureInfo.InvariantCulture),
                End = DateTime.ParseExact(line.Substring(91, 8).Trim(), "yyyyMMdd", CultureInfo.InvariantCulture)
            };

            stations.Add(station);
        }

        return stations;
    }
}

public class NoaaWeatherDataSource : IDataSource
{
    public NoaaWeatherDataSource(string stationName)
    {

    }

    public Task<List<string>> Trends()
    {
        throw new NotImplementedException();
    }

    public List<double> GetData(string trend)
    {
        throw new NotImplementedException();
    }

    public string Header { get; }
    public string ShortName { get; }
    public DataSourceType DataSourceType { get; }
    public TimestampData GetTimestampData(string trend)
    {
        throw new NotImplementedException();
    }

    public List<TimestampData> GetTimestampData(List<string> trends)
    {
        throw new NotImplementedException();
    }

    public List<TimestampData> GetTimestampData(List<string> trends, DateTime startDateInc, DateTime endDateExc)
    {
        throw new NotImplementedException();
    }
}
