#include <ESP8266HTTPClient.h>
#include <secrets.h>
#include <wunderground.h>


void reportConditionsDirect(float temperatureF, float humidity, float barometricPressure) 
{
  Serial.println(getUrl(temperatureF, humidity, barometricPressure));
  // HTTPClient http;
  // http.begin(getUrl(temperatureF, humidity, barometricPressure));
  // http.addHeader("Content-Type", "application/json");
  // http.GET();
  // http.end();
}

String getUrl(float temperatureF, float humidity, float barometricPressure) 
{
  String url = String("https://weatherstation.wunderground.com/weatherstation/updateweatherstation.php?");
  url.concat("ID=");
  url.concat(PWS_STATION_ID);
  url.concat("&PASSWORDID=");
  url.concat(PWS_PASSWORD);
  url.concat("&action=updateraw");
  url.concat("&dateutc=now");

  if (temperatureF != NAN) {
    url.concat("&tempf=");
    url.concat(temperatureF);
  }
  if (humidity != NAN) {
    url.concat("&humidity=");
    url.concat(humidity);
  }
  if (barometricPressure != NAN) {
    url.concat("&baromin=");
    url.concat(barometricPressure);
  }

  return url;
}