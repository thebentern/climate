using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AdysTech.InfluxDB.Client.Net;
using MQTTnet;
using MQTTnet.Client;
using Newtonsoft.Json;
using Nito.AsyncEx;

namespace Climate.Service
{
  class Program
  {
    public static string INFLUX_DB = DotNetEnv.Env.GetString("INFLUX_DB");
    public static string PWS_ID = DotNetEnv.Env.GetString("PWS_ID");
    public static string PWS_KEY = DotNetEnv.Env.GetString("PWS_KEY");

    private static readonly HttpClient client = new HttpClient();
    private static readonly AutoResetEvent closing = new AutoResetEvent(false);

    static async Task Main(string[] args)
    {
      DotNetEnv.Env.Load();
      IMqttClient mqttClient = CreateMqttClient();
      IMqttClientOptions options = CreateMqttClientOptions();

      SetupConnection(mqttClient);
      SetupDisconnection(mqttClient, options);
      mqttClient.ApplicationMessageReceived += OnMessageReceived;
      await mqttClient.ConnectAsync(options);
      closing.WaitOne();
    }

    private static void ReportConditionsWeatherUnderground(string jsonData)
    {
      var report = JsonConvert.DeserializeObject<WeatherStationReport>(jsonData);
      var weatherStationUrl = BuildWeatherStationUrl(report);
      UploadReportWeatherUnderground(weatherStationUrl);
      RecordInfluxDbMetric(report);
    }

    private static void RecordInfluxDbMetric(WeatherStationReport report)
    {
      Console.WriteLine("Recording metric to influx db");
      var client = new InfluxDBClient("http://influxdb:8086", "admin", "admin");
      var metric = new InfluxDatapoint<InfluxValueField>()
      {
        UtcTimestamp = DateTime.UtcNow,
        Precision = TimePrecision.Seconds,
        MeasurementName = "weather-station"
      };

      foreach (var property in report.GetType().GetProperties()) 
      {
        double fieldValue;
        var value = property.GetValue(report);
        if (value is string)
          fieldValue = Double.Parse(value.ToString());
        else
          fieldValue = (double)value;

        metric.Fields.Add(property.Name, new InfluxValueField(fieldValue));
      }
    
      bool success = client.PostPointAsync(INFLUX_DB, metric).Result;
    }

    private static void UploadReportWeatherUnderground(string url)
    {
      Console.WriteLine("Reporting conditions to Weather Underground...");
      Console.WriteLine($"GET: {url}");

      client.DefaultRequestHeaders.Accept.Clear();
      client.DefaultRequestHeaders.Add("User-Agent", ".NET Climate Service Reporter");

      var response = client.GetStringAsync(url).Result;

      Console.Write(response);
    }

    private static string BuildWeatherStationUrl(WeatherStationReport report)
    {
      var builder = new StringBuilder("https://weatherstation.wunderground.com/weatherstation/updateweatherstation.php?");
      builder.Append($"ID={PWS_ID}");
      builder.Append($"&PASSWORD={PWS_KEY}");
      builder.Append("&action=updateraw");
      builder.Append("&dateutc=now");

      if (report.temperature != null)
        builder.Append($"&tempf={report.temperature}");
        
      if (report.humidity != null)
        builder.Append($"&humidity={report.humidity}");

      if (report.barometricPressure != null)
        builder.Append($"&baromin={report.barometricPressureInHg}");

      if (report.temperature != null && report.humidity != null)
        builder.Append($"&dewptf={report.dewpoint}");

      return builder.ToString();
    }

    private static void OnMessageReceived(object sender, MqttApplicationMessageReceivedEventArgs e)
    {
        string payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
        Console.WriteLine($"Received message on topic: {e.ApplicationMessage.Topic}{Environment.NewLine}");
        Console.WriteLine($"Payload = {payload}");
        Console.WriteLine();

        ReportConditionsWeatherUnderground(payload);
    }

    private static void SetupDisconnection(IMqttClient mqttClient, IMqttClientOptions options)
    {
      mqttClient.Disconnected += async (s, e) =>
      {
        Console.WriteLine("Disconnected to server");
        await Task.Delay(TimeSpan.FromSeconds(5));

        try
        {
          await mqttClient.ConnectAsync(options);
        }
        catch
        {
          Console.WriteLine("Reconnecting failed");
        }
      };
    }

    private static void SetupConnection(IMqttClient mqttClient)
    {
      mqttClient.Connected += async (s, e) =>
      {
        var topic = DotNetEnv.Env.GetString("MQTT_TOPIC");
        Console.WriteLine("Connected to server");

        await mqttClient.SubscribeAsync(new TopicFilterBuilder().WithTopic(topic).Build());

        Console.WriteLine($"Subscribed to topic: {topic}");
      };
    }

    private static IMqttClientOptions CreateMqttClientOptions()
    {
      return new MqttClientOptionsBuilder()
          .WithTcpServer(DotNetEnv.Env.GetString("MQTT_HOST"), 1883)
          .Build();    
    }

    private static IMqttClient CreateMqttClient()
    {
      var factory = new MqttFactory();
      return factory.CreateMqttClient();
    }
  }
}
