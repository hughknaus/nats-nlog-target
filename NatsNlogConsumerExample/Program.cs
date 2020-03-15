using Microsoft.Extensions.Configuration;
using STAN.Client;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NatsNlogConsumerExample
{
    class Program
    {
        private static readonly bool IN_DOCKER = Environment.GetEnvironmentVariable("isdockercontainer") == "true";
        private static IStanConnection stanConnection;

        public static void Main(string[] args)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            IConfigurationRoot config = builder.Build();

            Console.WriteLine("Starting...");

            var natsconfig = config.GetSection("NatsConfig");
            var natsurl = natsconfig["url"];
            var clusterId = natsconfig["clusterid"];
            var clientid = natsconfig["clientid"];
            var topic = natsconfig["topic"];

            StanOptions opts = StanOptions.GetDefaultOptions();
            opts.NatsURL = natsurl;
            opts.ConnectionLostEventHandler = (obj, args) =>
            {
                Console.WriteLine($"NATS connection lost. {args.ConnectionException}");
            };

            try
            {
                using (var conn = Connect(clusterId, clientid, opts))
                {
                    var cts = new CancellationTokenSource();

                    Task.Run(() =>
                    {
                        conn.Subscribe(topic, (obj, args) =>
                        {
                            Console.WriteLine($"Received a message: {System.Text.Encoding.UTF8.GetString(args.Message.Data)}");
                        });
                    }, cts.Token);

                    if (IN_DOCKER)
                    {
                        Thread.Sleep(240000); // Only going to run this for 4min in the Docker container -- HEY! It's only an example!
                    }
                    else
                    {
                        Console.WriteLine("Hit any key to exit");
                        Console.ReadKey(); // If you don't run in a Docker container you could wait for the user to exit with any key
                    }

                    cts.Cancel();
                }
            }
            catch (Exception ex)
            {
                if (ex is StanConnectRequestTimeoutException || ex is StanConnectionException)
                {
                    Console.WriteLine($"Connection exception: {ex}");
                    return;
                }

                Console.WriteLine($"Exception: {ex}");
            }
        }

        private static IStanConnection Connect(string clusterId, string clientId, StanOptions opts = null)
        {
            int connectionRetryCounter = 0;
            int connectionRetryMax = 3;
            while (connectionRetryMax > 0 && stanConnection == null)
            {
                try
                {
                    if (stanConnection == null || stanConnection.NATSConnection == null)
                    {
                        if (opts == null)
                            stanConnection = new StanConnectionFactory().CreateConnection(clusterId, clientId);
                        else
                            stanConnection = new StanConnectionFactory().CreateConnection(clusterId, clientId, opts);
                    }
                }
                catch (STAN.Client.StanConnectionException ex)
                {
                    connectionRetryMax--;
                    Console.WriteLine($"NATS connection exception. {ex}");
                    Thread.Sleep(1000 * connectionRetryCounter);
                }
                catch (STAN.Client.StanConnectRequestTimeoutException ex)
                {
                    connectionRetryMax--;
                    Console.WriteLine($"NATS connection request timeout exception. {ex}");
                    Thread.Sleep(1000 * connectionRetryCounter);
                }
                catch (STAN.Client.StanConnectRequestException ex)
                {
                    connectionRetryMax--;
                    Console.WriteLine($"NATS connection request exception. {ex}");
                    Thread.Sleep(1000 * connectionRetryCounter);
                }

                connectionRetryCounter++;
            }

            return stanConnection;
        }
    }
}
