using Microsoft.Extensions.Configuration;
using STAN.Client;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NatsNlogConsumer
{
    class Program
    {
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

            using (var conn = Connect(clusterId, clientid, opts))
            {
                var cts = new CancellationTokenSource();

                Task.Run(() =>
                {
                    conn.Subscribe(topic, (obj, args) => {
                        Console.WriteLine($"Received a message: {System.Text.Encoding.UTF8.GetString(args.Message.Data)}");
                    });
                }, cts.Token);

                Console.WriteLine("Hit any key to exit");
                Console.ReadKey();
                cts.Cancel();
            }
        }

        private static IStanConnection Connect(string clusterId, string clientId, StanOptions opts = null)
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
                Console.WriteLine($"NATS connection exception. {ex}");
            }
            catch (STAN.Client.StanConnectRequestTimeoutException ex)
            {
                Console.WriteLine($"NATS connection request timeout exception. {ex}");
            }

            return stanConnection;
        }
    }
}
