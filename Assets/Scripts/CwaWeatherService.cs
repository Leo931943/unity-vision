using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Loads the current observation nearest to Ming Chuan University's Taoyuan
/// campus from the Central Weather Administration (CWA) Open Data API.
/// </summary>
public class CwaWeatherService : MonoBehaviour
{
    private const string CampusName = "銘傳大學桃園校區";
    private const float CampusLatitude = 25.0316f;
    private const float CampusLongitude = 121.3453f;
    private const string ApiUrl = "https://opendata.cwa.gov.tw/api/v1/rest/datastore/O-A0001-001";
    private const string ConfigFileName = "weather-config.json";

    [Header("CWA Update Settings")]
    [Tooltip("How often to refresh the current observation from CWA, in seconds.")]
    [Min(60f)] public float refreshIntervalSeconds = 600f;

    public static CwaWeatherService Instance { get; private set; }
    public string CampusDisplayName => CampusName;
    public string TaiwanTimeText => GetTaiwanTime().ToString("yyyy/MM/dd HH:mm:ss");
    public string TemperatureText { get; private set; } = "-- °C";
    public string WeatherText { get; private set; } = "等待氣象資料";
    public string ObservationStation { get; private set; } = "";
    public string ObservationTime { get; private set; } = "";
    public bool HasWeatherData { get; private set; }

    private string apiKey;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateService()
    {
        if (FindFirstObjectByType<CwaWeatherService>() != null)
            return;

        GameObject serviceObject = new GameObject("CwaWeatherService");
        serviceObject.AddComponent<CwaWeatherService>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private IEnumerator Start()
    {
        apiKey = LoadApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            WeatherText = "CWA API 金鑰未設定";
            yield break;
        }

        while (true)
        {
            yield return RefreshWeather();
            yield return new WaitForSecondsRealtime(refreshIntervalSeconds);
        }
    }

    private IEnumerator RefreshWeather()
    {
        string url = $"{ApiUrl}?Authorization={UnityWebRequest.EscapeURL(apiKey)}&format=JSON";
        using UnityWebRequest request = UnityWebRequest.Get(url);
        request.timeout = 20;
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            WeatherText = "CWA 資料暫時無法取得";
            HasWeatherData = false;
            yield break;
        }

        CwaResponse response;
        try
        {
            response = JsonUtility.FromJson<CwaResponse>(request.downloadHandler.text);
        }
        catch (ArgumentException)
        {
            WeatherText = "CWA 資料格式無法辨識";
            HasWeatherData = false;
            yield break;
        }

        CwaStation nearestStation = FindNearestStation(response?.records?.Station);
        if (nearestStation == null || nearestStation.WeatherElement == null || nearestStation.WeatherElement.AirTemperature <= -90f)
        {
            WeatherText = "找不到校區附近的有效測站";
            HasWeatherData = false;
            yield break;
        }

        TemperatureText = $"{nearestStation.WeatherElement.AirTemperature:F1} °C";
        WeatherText = string.IsNullOrWhiteSpace(nearestStation.WeatherElement.Weather)
            ? "即時觀測"
            : nearestStation.WeatherElement.Weather;
        ObservationStation = nearestStation.StationName;
        ObservationTime = nearestStation.ObsTime?.DateTime ?? "";
        HasWeatherData = true;
    }

    private static CwaStation FindNearestStation(CwaStation[] stations)
    {
        if (stations == null)
            return null;

        CwaStation nearest = null;
        float shortestDistance = float.MaxValue;
        foreach (CwaStation station in stations)
        {
            if (station?.GeoInfo?.Coordinates == null || station.WeatherElement == null || station.WeatherElement.AirTemperature <= -90f)
                continue;

            CwaCoordinate coordinate = Array.Find(station.GeoInfo.Coordinates,
                item => item != null && item.CoordinateName == "WGS84");
            if (coordinate == null)
                continue;

            float distance = HaversineDistanceKm(CampusLatitude, CampusLongitude,
                coordinate.StationLatitude, coordinate.StationLongitude);
            if (distance < shortestDistance)
            {
                shortestDistance = distance;
                nearest = station;
            }
        }

        return nearest;
    }

    private static float HaversineDistanceKm(float latitudeA, float longitudeA, float latitudeB, float longitudeB)
    {
        const float earthRadiusKm = 6371f;
        float latitudeDelta = Mathf.Deg2Rad * (latitudeB - latitudeA);
        float longitudeDelta = Mathf.Deg2Rad * (longitudeB - longitudeA);
        float a = Mathf.Sin(latitudeDelta * 0.5f) * Mathf.Sin(latitudeDelta * 0.5f) +
                  Mathf.Cos(latitudeA * Mathf.Deg2Rad) * Mathf.Cos(latitudeB * Mathf.Deg2Rad) *
                  Mathf.Sin(longitudeDelta * 0.5f) * Mathf.Sin(longitudeDelta * 0.5f);
        return earthRadiusKm * 2f * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1f - a));
    }

    private static string LoadApiKey()
    {
        string environmentKey = Environment.GetEnvironmentVariable("CWA_API_KEY");
        if (!string.IsNullOrWhiteSpace(environmentKey))
            return environmentKey.Trim();

        string configPath = Path.Combine(Application.streamingAssetsPath, ConfigFileName);
        if (!File.Exists(configPath))
            return string.Empty;

        try
        {
            WeatherConfig config = JsonUtility.FromJson<WeatherConfig>(File.ReadAllText(configPath));
            return config?.cwaApiKey?.Trim() ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static DateTime GetTaiwanTime()
    {
        try
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time"));
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei"));
        }
    }

    // JsonUtility populates these fields during deserialization.
#pragma warning disable CS0649
    [Serializable]
    private class WeatherConfig
    {
        public string cwaApiKey;
    }

    [Serializable]
    private class CwaResponse
    {
        public CwaResult records;
    }

    [Serializable]
    private class CwaResult
    {
        public CwaStation[] Station;
    }

    [Serializable]
    private class CwaStation
    {
        public string StationName;
        public CwaObsTime ObsTime;
        public CwaGeoInfo GeoInfo;
        public CwaWeatherElement WeatherElement;
    }

    [Serializable]
    private class CwaObsTime
    {
        public string DateTime;
    }

    [Serializable]
    private class CwaGeoInfo
    {
        public CwaCoordinate[] Coordinates;
    }

    [Serializable]
    private class CwaCoordinate
    {
        public string CoordinateName;
        public float StationLatitude;
        public float StationLongitude;
    }

    [Serializable]
    private class CwaWeatherElement
    {
        public string Weather;
        public float AirTemperature;
    }
#pragma warning restore CS0649
}
