#include <SPI.h>
#include <BME280I2C.h>
#include <Wire.h>
#include <mqtt.h>
#include <sensor.h>
#include <secrets.h>
#include <OneWire.h>
#include <DallasTemperature.h>

BME280I2C bme;

BME280::TempUnit tempUnit(BME280::TempUnit::TempUnit_Fahrenheit);
BME280::PresUnit presUnit(BME280::PresUnit_psi);

const int gpioPin = D4;
OneWire oneWire(gpioPin);
DallasTemperature DS18B20(&oneWire);

void initSensor()
{
  if (SENSOR_TYPE == "BME280") 
  {
    while (!bme.begin())
    {
      Serial.println("Attempting to connect to BME280 sensor...");
      delay(100);
    }
  } 
  else if (SENSOR_TYPE == "DS18B20") 
  {
    Serial.println("Attempting to connect to DS18B20 sensor...");
    DS18B20.begin();
  }
}

void sendClimateConditions() 
{
  float temperature(NAN);
  float humidity(NAN);
  float pressure(NAN);
  float volt(NAN);

  unsigned int raw = analogRead(A0);
  volt = (raw / 1023.0) * 4.2;

  if (SENSOR_TYPE == "BME280") 
  {
    bme.read(pressure, temperature, humidity, tempUnit, presUnit);
  }
  else if (SENSOR_TYPE == "DS18B20") 
  {
    DS18B20.requestTemperatures(); 
    temperature = DS18B20.getTempFByIndex(0);
  }
  reportConditions(volt, temperature, humidity, pressure);
}