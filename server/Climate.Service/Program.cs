using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using Newtonsoft.Json;
using Nito.AsyncEx;

namespace Climate.Service
{
  class Program
  {
    private static readonly HttpClient client = new HttpClient();

    static async Task Main(string[] args)
    {
      DotNetEnv.Env.Load();
      IMqttClient mqttClient = CreateMqttClient();
      IMqttClientOptions options = CreateMqttClientOptions();


      SetupConnection(mqttClient);
      SetupDisconnection(mqttClient, options);
      mqttClient.ApplicationMessageReceived += OnMessageReceived;
      await mqttClient.ConnectAsync(options);
      Console.ReadKey();
    }

    private static void ReportConditionsWeatherUnderground(string jsonData)
    {
      var weatherStationUrl = BuildWeatherStationUrl(jsonData);
      UploadReportWeatherUnderground(weatherStationUrl);
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

    private static string BuildWeatherStationUrl(string jsonData)
    {
      var builder = new StringBuilder("https://weatherstation.wunderground.com/weatherstation/updateweatherstation.php?");
      builder.Append($"ID={DotNetEnv.Env.GetString("PWS_ID")}");
      builder.Append($"&PASSWORD={DotNetEnv.Env.GetString("PWS_KEY")}");
      builder.Append("&action=updateraw");
      builder.Append("&dateutc=now");

      var report = JsonConvert.DeserializeObject<WeatherStationReport>(jsonData);

      if (report.temperature != null)
        builder.Append($"&tempf={report.temperature}");
      if (report.humidity != null)
        builder.Append($"&humidity={report.humidity}");
      if (report.barometricPressure != null)
        builder.Append($"&baromin={report.barometricPressure}");

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
