using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Climate.Service
{
  class Program
  {
    static async Task Main(string[] args)
    {
        var hostBuilder = new HostBuilder()
          .ConfigureServices((hostContext, services) =>
          {
            services
              .AddLogging(opt =>
              {
                  opt.AddConsole();
              });
          });
        await hostBuilder.RunConsoleAsync();
    }
  }
}
