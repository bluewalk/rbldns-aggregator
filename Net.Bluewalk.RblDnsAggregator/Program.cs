using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Net.Bluewalk.RblDnsAggregator
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var version = FileVersionInfo.GetVersionInfo(typeof(Program).Assembly.Location).ProductVersion;
            Console.WriteLine($"RBL DNS Aggregator {version}");
            Console.WriteLine("https://github.com/bluewalk/rbldns-aggregator\n");


            var builder = new HostBuilder()
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.AddEnvironmentVariables();

                    if (File.Exists("config.json"))
                        config.AddJsonFile("config.json", false, true);

                    if (args != null)
                    {
                        config.AddCommandLine(args);
                    }
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddOptions();

                    //services.Replace(ServiceDescriptor.Singleton(typeof(ILogger<>), typeof(DateTimeLogger<>)));

                    services.AddSingleton<IHostedService, Logic>();
                })
                .ConfigureLogging((hostingContext, logging) => {
                    logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                    logging.AddConsole();
                });

            await builder.RunConsoleAsync();
        }
    }
}
