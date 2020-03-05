using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;

namespace NatsNlogExample
{
    internal static class Program
    {
        private static void Main()
        {
            var logger = LogManager.GetCurrentClassLogger();

            try
            {
                var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                    .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                    .Build();


                var servicesProvider = BuildDi(config);

                using (servicesProvider as IDisposable)
                {
                    var runner = servicesProvider.GetRequiredService<Runner>();
                   
                    for (var i = 1; i < 100; i++) 
                    {
                        runner.DoAction($"Action-{i}");

                        Random rnd = new Random();
                        var x = rnd.Next(1, 10);

                        if (x == 5)
                            System.Threading.Thread.Sleep(5000);
                    }

                    Console.WriteLine("Done...");
                    Console.WriteLine("Shutting down...");
                    runner.ShutDown();
                }
            }
            catch (Exception ex)
            {
                // NLog: catch any exception and log it.
                logger.Error(ex, "Stopped program because of exception");
                throw;
            }
            finally
            {
                // Ensure to flush and stop internal timers/threads before application-exit (Avoid segmentation fault on Linux)
                LogManager.Shutdown();
            }
        }

        private static IServiceProvider BuildDi(IConfiguration config)
        {
            return new ServiceCollection()
                .AddTransient<Runner>() // Runner is the custom class
                .AddLogging(loggingBuilder =>
                {
                    // configure Logging with NLog
                    loggingBuilder.ClearProviders();
                    loggingBuilder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                    loggingBuilder.AddNLog(config);
                })
                .BuildServiceProvider();
        }
    }

    public class Runner
    {
        private readonly ILogger<Runner> logger;

        public Runner(ILogger<Runner> logger)
        {
            this.logger = logger;
        }

        public void DoAction(string name)
        {
            logger.LogDebug(20, $"Doing hard work! {name}");
            logger.LogInformation(21, $"Doing hard work! {name}");
            logger.LogWarning(22, $"Doing hard work! {name}");
            logger.LogError(23, $"Doing hard work! {name}");
            logger.LogCritical(24, $"Doing hard work! {name}");
        }

        public void ShutDown()
        {
            logger.LogInformation(24, "Shutting down.");
        }
    }
}