﻿using STAN.Client;
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

        public string NatsUrl { get; set; }

        public string NatsClusterId { get; set; }

        public string NatsClientId { get; set; }

        public int NatsConnectionTimeout { get; set; }

        public int NatsPubAckWait { get; set; }

        #endregion

        #region "Ctor"
        public NatsTarget() : this(null, null, null)
        {

        }

        public NatsTarget(string natsUrl, string natsClusterId, string natsClientId, int natsConnectionTimeout = 10000, int natsPubAckWait = 5000)
        {
            cache = new ConcurrentDictionary<string, IpObj>();
            this.NatsUrl = natsUrl;
            this.NatsClusterId = natsClusterId;
            this.NatsClientId = natsClientId;
            this.NatsConnectionTimeout = natsConnectionTimeout;
            this.NatsPubAckWait = natsPubAckWait;

            opts.NatsURL = this.NatsUrl;
            opts.ConnectionLostEventHandler = (obj, args) =>
            {
                InternalLogger.Error($"NATS connection lost. {args.ConnectionException}");
            };

            stanConnection = Connect(this.NatsClusterId, this.NatsClientId, opts);
        }
        #endregion

        /// <summary>
        /// Close NLog Target
        /// </summary>
        protected override void CloseTarget()
        {
            base.CloseTarget();
            CloseAndDisposeStan();
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

                byte[] msgData = System.Text.Encoding.UTF8.GetBytes(json);
                var pubTopic = System.Text.RegularExpressions.Regex.Replace(topic, @"\s", "");
                
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

                var conn = Connect(this.NatsClusterId, this.NatsClientId, opts); //We wont to reuse the connection as much as possible so manually handle close/dispose
                var guid = conn.Publish(pubTopic, msgData, ackHandler);

                if (verbose)
                    InternalLogger.Info($"Published message with guid: {guid}");
            }
            catch (Exception ex)
            {
                InternalLogger.Error(ex, $"NATS published error.");
                CloseAndDisposeStan();
            }

            base.Write(logEvent);
        }

        private IStanConnection Connect(string clusterId, string clientId, StanOptions opts = null)
        {
            try
            {
                if (String.IsNullOrEmpty(clusterId) || String.IsNullOrEmpty(clientId))
                    return null;

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

        private void CloseAndDisposeStan()
        {
            if (stanConnection != null)
            {
                stanConnection.Close();
                stanConnection.Dispose();
            }
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
