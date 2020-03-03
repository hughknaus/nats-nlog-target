using STAN.Client;
using Newtonsoft.Json;
using NLog;
using NLog.Common;
using NLog.Config;
using NLog.Layouts;
using NLog.Targets;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;

namespace NatsNlogTargets
{
    [Target("NatsTarget")]
    public class NatsTarget : TargetWithLayout
    {
        private class IpObj
        {
            public string Ip { get; set; }
            public DateTimeOffset Expiration { get; set; }
        }

        #region "Member Variables"

        private int count;
        private ConcurrentDictionary<string, IpObj> cache;
        private const string IP_CACHE_KEY = "memory:ipaddress";
        private StanOptions opts = StanOptions.GetDefaultOptions();
        private IStanConnection stanConnection;
        private bool verbose = true;

        #endregion

        #region "Properties"

        [RequiredParameter]
        public Layout Topic { get; set; }

        [RequiredParameter]
        public Layout ThreadId { get; set; }

        [RequiredParameter]
        public Layout MachineName { get; set; }

        [RequiredParameter]
        public Layout TenantName { get; set; }

        [RequiredParameter]
        public Layout NatsUrl { get; set; }

        [RequiredParameter]
        public Layout NatsClusterId { get; set; }

        [RequiredParameter]
        public Layout NatsClientId { get; set; }

        [RequiredParameter]
        public Layout NatsConnectionTimeout { get; set; }

        [RequiredParameter]
        public Layout NatsPubAckWait { get; set; }

        #endregion

        #region "Ctor"
        public NatsTarget()
        {
            cache = new ConcurrentDictionary<string, IpObj>();
        }
        #endregion

        /// <summary>
        /// Close NLog Target
        /// </summary>
        protected override void CloseTarget()
        {
            base.CloseTarget();

            if (stanConnection != null)
            {
                stanConnection.Close();
                stanConnection.Dispose();
            }
        }

        /// <summary>
        /// Write LogEventInfo to STAN
        /// </summary>
        /// <param name="logEvent"></param>
        protected override void Write(LogEventInfo logEvent)
        {
            var instanceIp = GetCurrentIpFromCache();

            string topic = base.RenderLogEvent(this.Topic, logEvent);
            string threadId = base.RenderLogEvent(this.ThreadId, logEvent);
            string machineName = base.RenderLogEvent(this.MachineName, logEvent);
            string tenantName = base.RenderLogEvent(this.TenantName, logEvent);
            string msg = base.RenderLogEvent(this.Layout, logEvent);
            string natsurl = base.RenderLogEvent(this.NatsUrl, logEvent); //2 for FT, 3 for Cluster
            string natsclusterid = base.RenderLogEvent(this.NatsClusterId, logEvent);
            string natsclientid = base.RenderLogEvent(this.NatsClientId, logEvent);
            string natsconnectiontimeout = base.RenderLogEvent(this.NatsConnectionTimeout, logEvent);
            string natspubackwait = base.RenderLogEvent(this.NatsPubAckWait, logEvent);

            var json = JsonConvert.SerializeObject(new
            {
                dateTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                level = logEvent.Level.Name.ToUpper(),
                instanceip = instanceIp,
                threadid = threadId,
                machinename = machineName,
                tenantname = tenantName, 
                @class = logEvent.LoggerName,
                message = msg
            });

            try
            {
                AutoResetEvent ev = new AutoResetEvent(false);

                long acksProcessed = 0;

                if (!String.IsNullOrEmpty(natsconnectiontimeout.Trim()))
                    opts.ConnectTimeout = Int32.Parse(natsconnectiontimeout);

                if (!String.IsNullOrEmpty(natspubackwait.Trim()))
                    opts.PubAckWait = Int32.Parse(natspubackwait);

                opts.NatsURL = natsurl;
                opts.ConnectionLostEventHandler = (obj, args) =>
                {
                    InternalLogger.Error($"NATS connection lost. {args.ConnectionException}");
                };

                byte[] msgData = System.Text.Encoding.UTF8.GetBytes(json);
                var pubTopic = System.Text.RegularExpressions.Regex.Replace(topic, @"\s", "");

                using (var conn = Connect(natsclusterid, natsclientid, opts))
                {
                    // when the server responds with an acknowledgment, this handler will be invoked.
                    EventHandler<StanAckHandlerArgs> ackHandler = (obj, args) =>
                    {
                        if (verbose)
                            InternalLogger.Info($"Ack Received ack for message {args.GUID}");

                        if (!string.IsNullOrEmpty(args.Error))
                            InternalLogger.Error($"Error processing Pub {args.GUID}; {args.Error}");

                        if (Interlocked.Increment(ref acksProcessed) == count)
                            ev.Set();
                    };

                    var guid = conn.Publish(pubTopic, msgData, ackHandler);

                    if (verbose)
                        InternalLogger.Info($"Published message with guid: {guid}");
                }
                
            }
            catch (Exception ex)
            {
                InternalLogger.Error(ex, $"NATS published error.");
            }

            base.Write(logEvent);
        }

        private IStanConnection Connect(string clusterId, string clientId, StanOptions opts = null)
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
                InternalLogger.Error(ex, $"NATS connection exception.");
            }
            catch (STAN.Client.StanConnectRequestTimeoutException ex)
            {
                InternalLogger.Error(ex, $"NATS connection request timeout exception.");
            }

            return stanConnection;
        }

        private string GetCurrentIp()
        {
            var instanceIp = "127.0.0.1";

            try
            {
                IPHostEntry ipHost = Dns.GetHostEntry(Dns.GetHostName());

                foreach (var ipAddr in Dns.GetHostAddresses(Dns.GetHostName()))
                {
                    if (ipAddr.AddressFamily.ToString() == "InterNetwork")
                    {
                        instanceIp = ipAddr.ToString();
                        break;
                    }
                }
            }
            catch
            {
            }

            return instanceIp;
        }

        private string GetCurrentIpFromCache()
        {
            if (cache.TryGetValue(IP_CACHE_KEY, out var obj))
            {
                return DateTimeOffset.UtcNow.Subtract(obj.Expiration) < TimeSpan.Zero
                    ? obj.Ip
                    : BuildCacheAndReturnIp();
            }
            else
            {
                return BuildCacheAndReturnIp();
            }
        }

        private string BuildCacheAndReturnIp()
        {
            var newObj = new IpObj
            {
                Ip = GetCurrentIp(),
                Expiration = DateTimeOffset.UtcNow.AddMinutes(5),
            };

            cache.AddOrUpdate(IP_CACHE_KEY, newObj, (x, y) => newObj);

            return newObj.Ip;
        }
    }
}
