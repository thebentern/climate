#include <PubSubClient.h>
#include <Ticker.h>
#include <ESP8266WebServer.h>
#include <Arduino.h>

extern Ticker wifiReconnectTimer;

void connectToMqtt();
void setupMqtt();
void reportConditions(float voltage, float temperatureF, float humidity, float barometricPressure); 