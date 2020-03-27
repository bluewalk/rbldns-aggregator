using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Net.Bluewalk.DotNetEnvironmentExtensions;
using Serilog;
using Serilog.Events;

namespace Net.Bluewalk.RblDnsAggregator
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var version = FileVersionInfo.GetVersionInfo(typeof(Program).Assembly.Location).ProductVersion;
            Console.WriteLine($"RBL DNS Aggregator {version}");
            Console.WriteLine("https://github.com/bluewalk/rbldns-aggregator\n");

            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Debug()
                .WriteTo.ColoredConsole(
                    EnvironmentExtensions.GetEnvironmentVariable<LogEventLevel>("LOG_LEVEL", LogEventLevel.Information)
                ).CreateLogger();

            AppDomain.CurrentDomain.DomainUnload += (sender, eventArgs) => Log.CloseAndFlush();

            var builder = new HostBuilder()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddOptions();

                    services.AddSingleton<IHostedService, Logic>();
                })
                .UseSerilog();

            await builder.RunConsoleAsync();
        }
    }
}