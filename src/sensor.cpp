#include <SPI.h>
#include <BME280I2C.h>
#include <Wire.h>
#include <mqtt.h>
#include <sensor.h>

BME280I2C bme; 

void initSensor() 
{
  while (!bme.begin())
  {
    Serial.println("Attempting to connect to BME280 sensor...");
    delay(100);
  }
}

void sendClimateConditions() 
{
  float temperature(NAN);
  float humidity(NAN);
  float pressure(NAN);
  float volt(NAN); 

  BME280::TempUnit tempUnit(BME280::TempUnit::TempUnit_Fahrenheit);
  BME280::PresUnit presUnit(BME280::PresUnit_psi);

  unsigned int raw = analogRead(A0);
  volt = (raw / 1023.0) * 4.2;
  bme.read(pressure, temperature, humidity, tempUnit, presUnit);

  reportConditions(volt, temperature, humidity, pressure);
}