#include <Arduino.h>
#include <ESP8266WebServer.h>
#include <Wire.h>
#include <secrets.h>
#include <sensor.h>
#include <mqtt.h>

// flag to turn on/off debugging
#define DEBUG false 

#define Serial if(DEBUG)Serial 

// 1 minute
// Deep sleep times are specified in microseconds
#define DUTY_INTERVAL 60000

void setupWifi()
{
  WiFi.mode( WIFI_OFF );
  WiFi.forceSleepBegin();
  delay(1);
}

void connectAndPublish() 
{
  IPAddress ip(192, 168, 2, 90);
  IPAddress gateway(192, 168, 0, 254);
  IPAddress subnet(255, 255, 255, 0);

  WiFi.persistent(false);
  WiFi.forceSleepWake();
  delay(1);
  WiFi.mode(WIFI_STA);
  WiFi.begin(SSID, WIFI_PASSWORD);
  Serial.print("Connecting to wifi: ");
  Serial.print(SSID);
  Serial.println();
  while (WiFi.status() != WL_CONNECTED)
  {
    delay(200);
    Serial.print(".");
  }
  Serial.println();
  Serial.print("Connected. IP address: ");
  Serial.println(WiFi.localIP());
  Serial.println();
  connectToMqtt();
}

void setup() 
{
  Serial.begin(9600);
  Wire.begin();
  pinMode(A0, INPUT);
  setupMqtt();
  setupWifi();
  initSensor();
}

// Thanks to OppoverBakke for power consumption optimization research
// https://www.bakke.online/index.php/2017/05/22/reducing-wifi-power-consumption-on-esp8266-part-3/
void loop() 
{
  connectAndPublish();
  ESP.deepSleep(DUTY_INTERVAL * 1000);
}
