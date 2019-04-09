using System;

namespace Climate.Service
{
  public class WeatherStationReport
  {
    private const double PSI_TO_INHG_COEFFICIENT = 2.03602;

    public string voltage { get; set; }
    public string temperature { get; set; }
    public string humidity { get; set; }
    public string barometricPressure { get; set; }

    public double barometricPressureInHg => Double.Parse(barometricPressure) * PSI_TO_INHG_COEFFICIENT;
    public double dewpoint => CaculateDewpoint(Double.Parse(temperature), Double.Parse(humidity));
    
    private static double CaculateDewpoint(double temperature, double humidity)
    {
      double temp = (temperature - 32) / 1.8;
    
      double result =  (temp - (14.55 + 0.114 * temp) * (1 - (0.01 * humidity)) - Math.Pow(((2.5 + 0.007 * temp) * (1 - (0.01 * humidity))),3) - (15.9 + 0.117 * temp) * Math.Pow((1 - (0.01 * humidity)), 14));

      double value = result * (9.0/5.0);
      
      return value + 32.0;
    }
  }
}