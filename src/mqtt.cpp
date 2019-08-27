#include <mqtt.h>
#include <ArduinoJson.h>
#include <secrets.h>
#include <sensor.h>

WiFiClient espClient;
PubSubClient mqttClient(espClient);

void setupMqtt() 
{
  Serial.println("Setting up MQTT");
  mqttClient.setServer(MQTT_HOST, 1883);
}

void connectToMqtt() {
  Serial.println("Connecting to MQTT");
  String clientId = "ESP8266Client-";
  clientId += String(random(0xffff), HEX);
  mqttClient.connect(clientId.c_str());
  delay(1);
  sendClimateConditions();
}

void shutdownRadio(bool publishSucceeded) {
  Serial.println("Publish " + publishSucceeded ? "succeeded" : "failed");
  Serial.println("Shutting down...");
  delay(1);
  WiFi.forceSleepBegin();
}

void reportConditions(float voltage, float temperatureF, float humidity, float barometricPressure) 
{
  DynamicJsonDocument doc(1024);

  JsonObject root = doc.to<JsonObject>();
  if (!isnan(voltage))
    root["voltage"] = voltage;
  if (!isnan(temperatureF))
    root["temperature"] = temperatureF;
  if (!isnan(humidity))
    root["humidity"] = humidity;
  if (!isnan(barometricPressure))
    root["barometricPressure"] = barometricPressure;

  char jsonPayload[1024];
  serializeJson(doc, jsonPayload);
  bool successful = mqttClient.publish(MQTT_TOPIC_CONDITIONS, jsonPayload);
  Serial.println(jsonPayload);

  if (!DEBUG)
    shutdownRadio(successful);
}