using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AdysTech.InfluxDB.Client.Net;
using MQTTnet;
using MQTTnet.Client;
using Newtonsoft.Json;
using Nito.AsyncEx;
using Polly;
using Polly.Retry;
using System.Net.Http;
using Climate.Service;
using System.Text;

public class WeatherStationService : IHostedService, IDisposable
{
  private readonly ILogger _logger;

  public static string INFLUX_DB = DotNetEnv.Env.GetString("INFLUX_DB");

  private readonly HttpClient WebClient = new HttpClient();
  private static IAsyncPolicy TimeoutPolicy = Policy
    .TimeoutAsync(5);

  private static AsyncRetryPolicy RetryPolicy = Policy
    .Handle<Exception>()
    .WaitAndRetryAsync(new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4),  TimeSpan.FromSeconds(8) });

  public WeatherStationService(ILogger<WeatherStationService> logger) => 
    _logger = logger;

  public async Task StartAsync(CancellationToken cancellationToken)
  {
    _logger.LogInformation($"{nameof(WeatherStationService)} is starting.");

    DotNetEnv.Env.Load();
    IMqttClient mqttClient = CreateMqttClient();
    IMqttClientOptions options = CreateMqttClientOptions();

    SetupConnection(mqttClient);
    SetupDisconnection(mqttClient, options);
    mqttClient.ApplicationMessageReceived += OnMessageReceived;
    await mqttClient.ConnectAsync(options);
  }
  public Task StopAsync(CancellationToken cancellationToken)
  {
    _logger.LogInformation($"{nameof(WeatherStationService)} is stopping.");

    return Task.CompletedTask;
  }

  public void Dispose() => WebClient?.Dispose();

  private async Task UploadReportWeatherUnderground(string url)
  {
    _logger.LogInformation("Reporting conditions to Weather Underground...");
    _logger.LogInformation($"GET: {url}");

    WebClient.DefaultRequestHeaders.Accept.Clear();
    WebClient.DefaultRequestHeaders.Add("User-Agent", ".NET Climate Service Reporter");
    WebClient.Timeout = TimeSpan.FromSeconds(10);
    
    var response = await WebClient.GetStringAsync(url).ConfigureAwait(false);

    _logger.LogInformation(response);
  }

  private void OnMessageReceived(object sender, MqttApplicationMessageReceivedEventArgs e)
  {
    string payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
    _logger.LogInformation($"Received message on topic: {e.ApplicationMessage.Topic}{Environment.NewLine}");
    _logger.LogInformation($"Payload = {payload}");
    _logger.LogInformation(Environment.NewLine);

    AsyncContext.Run(async () => await ReportConditionsWeatherUnderground(payload));
  }

  private  void SetupDisconnection(IMqttClient mqttClient, IMqttClientOptions options)
  {
    mqttClient.Disconnected += async (s, e) =>
    {
      _logger.LogInformation("Disconnected to server");
      await Task.Delay(TimeSpan.FromSeconds(5));

      try
      {
        await mqttClient.ConnectAsync(options);
      }
      catch
      {
        _logger.LogInformation("Reconnecting failed");
      }
    };
  }

  private void SetupConnection(IMqttClient mqttClient)
  {
    mqttClient.Connected += async (s, e) =>
    {
      var topic = DotNetEnv.Env.GetString("MQTT_TOPIC");
      _logger.LogInformation("Connected to server");

      await mqttClient.SubscribeAsync(new TopicFilterBuilder().WithTopic(topic).Build());

      _logger.LogInformation($"Subscribed to topic: {topic}");
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
  
  private async Task ReportConditionsWeatherUnderground(string jsonData)
  {
    var report = JsonConvert.DeserializeObject<WeatherStationReport>(jsonData);
    await TimeoutPolicy.ExecuteAsync(async () =>
    {
      await UploadReportWeatherUnderground(report.BuildWeatherStationUrl());
    });
    await TimeoutPolicy.ExecuteAsync(async () =>
    {
      await RecordInfluxDbMetric(report);
    });
  }

  private async Task RecordInfluxDbMetric(WeatherStationReport report)
  {
    _logger.LogInformation("Recording metric to influx db");
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
  
    bool success = await client.PostPointAsync(INFLUX_DB, metric);
  }
}
