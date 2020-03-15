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
        private static readonly bool IN_DOCKER = Environment.GetEnvironmentVariable("isdockercontainer") == "true";
        private static IConfigurationSection natsconfig;
        private static string natsUrl;
        private static string natsClusterId;
        private static string natsClientId;
        private static int natsConnectionTimeout;
        private static int natsPubAckWait;
        private static NLog.ILogger logger;

        private static void Main()
        {
            try
            {
                var config = new ConfigurationBuilder()
                    .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build();

                natsconfig = config.GetSection("NatsConfig");
                natsUrl = natsconfig["url"];
                natsClusterId = natsconfig["clusterid"];
                natsClientId = natsconfig["clientid"];
                Int32.TryParse(natsconfig["connectiontimeout"], out natsConnectionTimeout);
                Int32.TryParse(natsconfig["pubackwait"], out natsPubAckWait);

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
            var nlogInstance = NLog.Config.ConfigurationItemFactory.Default.CreateInstance;
            NLog.Config.ConfigurationItemFactory.Default.CreateInstance = type =>
            {
                if (type == typeof(NatsNlogTargets.NatsAsyncTarget))
                {
                    return new NatsNlogTargets.NatsAsyncTarget(natsUrl, natsClusterId, natsClientId, natsConnectionTimeout, natsPubAckWait);
                }

                return nlogInstance(type);
            };

            var servColl = new ServiceCollection()
                .AddScoped<NatsNlogTargets.NatsAsyncTarget>(s => { 
                    return new NatsNlogTargets.NatsAsyncTarget(natsUrl, natsClusterId, natsClientId, natsConnectionTimeout, natsPubAckWait);
                })
                .AddTransient<Runner>() // Runner is the custom class
                .AddLogging(loggingBuilder =>
                {
                    // configure Logging with NLog
                    loggingBuilder.ClearProviders();
                    loggingBuilder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                    loggingBuilder.AddNLog(config);
                })
                .BuildServiceProvider();

            return servColl;
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