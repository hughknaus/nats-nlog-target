using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace NatsStreamingServerInstaller
{
    public class Program
    {
        private static int verbosity = 0;

        public static void Main(string[] args)
        {
            var exit = false;
            var showHelp = true;
            var auto = false;
            var delete = false;
            var cluster = false;
            var faulttolerant = false;
            var nodes = new List<string>();
            var validClusterNodes = new List<string>() { "node-A", "node-B", "node-C" };
            var validFaultTolerantNodes = new List<string>() { "node-A", "node-B" };

            var p = new Options() {
                { "a|auto", $"auto install nats-streaming-server cluster/fault tolerant.", v => auto = v != null },
                { "d|delete=", $"uninstall one or more of the nats-streaming-server nodes.\n\t\t\t\t[[[{validClusterNodes[0]}],{validClusterNodes[1]}],{validClusterNodes[2]}]]", v => delete = v != null },
                { "v|verbose", $"increase debug message verbosity", v => { if (v != null) ++verbosity; } },
                { "h|help", $"show this message", v => showHelp = v != null },
                { "x|exit", $"exit", v => exit = v != null },
            };

            while (exit == false) 
            {
                IEnumerable<string> extra;

                try
                {
                    extra = p.Parse(args);
                }
                catch (Exception e)
                {
                    Console.Write("natsinstaller: ");
                    Console.WriteLine(e.Message);
                    Console.WriteLine("Try `natsinstaller --help' for more information.");
                    return;
                }

                if (showHelp)
                    ShowHelp(p);

                if (auto && exit == false) 
                {
                    //ToDo: install auto

                    if (cluster) 
                    {
                        Console.WriteLine($"Auto node installation: {String.Join(",", validClusterNodes)}.");
                        SetupClusterFoldersAndInstall(validClusterNodes);
                    }
                    else if (faulttolerant) 
                    {
                        Console.WriteLine($"Auto node installation: {String.Join(",", validFaultTolerantNodes)}.");
                        SetupFaultTolerantFoldersAndInstall(validFaultTolerantNodes);
                    }

                    Console.ReadKey();

                    return;
                }

                if (delete && exit == false)
                {
                    Console.WriteLine($"Uninstalling node(s)...");
                    UninstallNodes(validClusterNodes); //We'll just use the Cluster Nodes here because it can get them all (regardless of FT or Cluster)
                    Console.WriteLine($"Uninstall complete.");
                }

                Console.WriteLine();
                Console.WriteLine("Option: ");

                switch (Console.ReadKey().Key)
                {
                    case ConsoleKey.A:
                        auto = true;
                        showHelp = false;

                        Console.WriteLine();
                        Console.WriteLine("Cluster (C) or Fault Tolerant (F):");
                        var key = Console.ReadKey().Key;
                        cluster = (key == ConsoleKey.C) ? true : false;
                        faulttolerant = (key == ConsoleKey.F) ? true : false;
                        break;
                    case ConsoleKey.D:
                        delete = true;
                        showHelp = false;
                        break;
                    default:
                        exit = true;
                        showHelp = false;
                        break;
                }
                
                Console.WriteLine();
            }
        }

        private static void ShowHelp(Options p)
        {
            Console.WriteLine("Usage: natsinstaller [OPTIONS]+");
            Console.WriteLine("Install nats cluster or fault tolerant instances.");
            Console.WriteLine();
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
        }

        private static void Debug(string format, params object[] args)
        {
            if (verbosity > 0)
            {
                Console.Write("# ");
                Console.WriteLine(format, args);
            }
        }

        private static void MakeDir(string path)
        {
            if (Directory.Exists(path) == false)
                Directory.CreateDirectory(path);
        }

        private static void DeleteDir(string path)
        {
            if (Directory.Exists(path) == true) 
            {
                Console.WriteLine($"DELETE {path}, Are you sure? y/n");
                var key = Console.ReadKey().Key;

                if (key == ConsoleKey.Y)
                    try
                    {
                        Directory.Delete(path, true);
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine($"Unable to delete. {ex.Message}");
                    }

                Console.WriteLine();
            }
        }

        private static void Copy(string fromPath, string toPath)
        {
            foreach(var file in Directory.GetFiles(fromPath))
            {
                var f = Path.GetFileName(file);

                if (File.Exists(Path.Combine(toPath, f)) == false)
                    File.Copy(Path.Combine(fromPath, f), Path.Combine(toPath, f));
            }
        }

        private static void UninstallNodes(List<string> nodes)
        {
            nodes.ForEach(node => {
                var sourcePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), $"nats-{node}");
                var installPath = $"C:\\nats-{node}";

                InstallService($"stop nats-{node}");
                InstallService($"delete nats-{node}");
                DeleteDir(installPath);
            });
        }

        private static void SetupClusterFoldersAndInstall(List<string> nodes)
        {
            /* NEED TO RUN FOR EACH NODE, CLUSTER IS >3 NODES:
             * sc stop nats-streaming-server-A
             * sc delete nats-streaming-server-A
             * sc.exe create nats-streaming-server-A binPath= "%CD%\bin\nats-Node-A\nats-streaming-server.exe -config ./node-a.conf -l c:\logs\nats-NODE-A.log -D" start= auto
             * sc.exe start nats-streaming-server-A
             */

            nodes.ForEach(node => {
                var sourcePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "ClusterConfigs", $"nats-{node}");
                var installPath = $"C:\\nats-{node}";
                var tuple = ParseConfigFile(Path.Combine(sourcePath, $"{node}.conf"));
                var listenPort = tuple.Item1.Where(x => x.Key == "port").Select(x => x.Value).FirstOrDefault().Trim();
                var cluster = tuple.Item1.Where(x => x.Key == "listen").Select(x => x.Value).FirstOrDefault().Trim();
                var routes = tuple.Item1.Where(x => x.Key == "routes").Select(x => x.Value).FirstOrDefault()?.Replace("[", "").Replace("]", "").Trim();
                var clusterId = tuple.Item2.Where(x => x.Key == "cluster_id").Select(x => x.Value).FirstOrDefault().Trim();

                var clusterPeers = (nodes.Count > 1) ? $" -cluster_raft_logging -cluster_peers {String.Join(",", nodes.Where(n => n != node))}" : "";

                //var createCmd = $"create nats-{node} binPath= \"{Path.Combine(installPath, "nats-streaming-server.exe")} -p {listenPort} -config {Path.Combine(installPath, $"{node}.conf")} -cid test-cluster -clustered -cluster_node_id {node}{clusterPeers} -l C:\\logs\\nats-{node}.log -DV\" start= auto";
                var createCmd = $"create nats-{node} binPath= \"{Path.Combine(installPath, "nats-streaming-server.exe")} -config {Path.Combine(installPath, $"{node}.conf")} -l C:\\logs\\nats-{node}.log -DV\" start= auto";

                Console.WriteLine($"Installing node \"{node}\"...");

                MakeDir(installPath);
                Copy(sourcePath, installPath);

                InstallService($"stop nats-{node}");
                InstallService($"delete nats-{node}");
                InstallService(createCmd);
                InstallService($"start nats-{node}");

                System.Threading.Thread.Sleep(10000);
            });
        }

        private static void SetupFaultTolerantFoldersAndInstall(List<string> nodes)
        {
            /* NEED TO RUN FOR EACH NODE, FAULT TOLERANCE IS ONLY 2 NODES:
             * sc stop nats-streaming-server-A
             * sc delete nats-streaming-server-A
             * sc stop nats-streaming-server-B
             * sc delete nats-streaming-server-B
             * sc.exe create nats-streaming-server-A binPath= "%CD%\bin\nats-Node-A\nats-streaming-server -p 4222 -store file -dir datastore -ft_group \"ft\" -cluster nats://localhost:6222 -routes nats://localhost:6223 -l c:\logs\nats-NODE-A.log -D" start= auto
             * sc.exe create nats-streaming-server-B binPath= "%CD%\bin\nats-Node-B\nats-streaming-server -p 4223 -store file -dir datastore -ft_group \"ft\" -cluster nats://localhost:6223 -routes nats://localhost:6222 -l c:\logs\nats-NODE-A.log -D" start= auto
             * sc.exe start nats-streaming-server-A
             * sc.exe start nats-streaming-server-B
             */

            nodes.ForEach(node => {
                var sourcePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "FaultTolerantConfigs", $"nats-{node}");
                var installPath = $"C:\\nats-{node}";
                var tuple = ParseConfigFile(Path.Combine(sourcePath, $"{node}.conf"));
                var listenPort = tuple.Item1.Where(x => x.Key == "port").Select(x => x.Value).FirstOrDefault().Trim();
                var cluster = tuple.Item1.Where(x => x.Key == "listen").Select(x => x.Value).FirstOrDefault().Trim();
                var routes = tuple.Item1.Where(x => x.Key == "routes").Select(x => x.Value).FirstOrDefault()?.Replace("[", "").Replace("]", "").Trim();
                var clusterId = tuple.Item2.Where(x => x.Key == "cluster_id").Select(x => x.Value).FirstOrDefault().Trim();

                cluster = $"nats://{cluster.Split(':')[0]}:{cluster.Split(':')[1]}"; //For command line it needs to be formatted as the URL version

                //var createCmd = $"create nats-{node} binPath= \"{Path.Combine(installPath, "nats-streaming-server.exe")} -p {listenPort} -store file -dir /nats/shareddatastore -ft_group \"test-group\" -cluster_node_id {clusterId} -cluster {cluster} -routes {routes} -l C:\\logs\\nats-{node}.log -DV\" start= auto";
                var createCmd = $"create nats-{node} binPath= \"{Path.Combine(installPath, "nats-streaming-server.exe")} -config {Path.Combine(installPath, $"{node}.conf")} -l C:\\logs\\nats-{node}.log -DV\" start= auto";

                Console.WriteLine($"Installing node \"{node}\"...");

                MakeDir(installPath);
                Copy(sourcePath, installPath);

                InstallService($"stop nats-{node}");
                InstallService($"delete nats-{node}");
                InstallService(createCmd);
                InstallService($"start nats-{node}");

                System.Threading.Thread.Sleep(10000);
            });
        }

        private static bool InstallService(string processArgs)
        {
            bool success = true;
            bool errorsWritten = false;

            using (var proc = new Process())
            {
                try
                {
                    proc.StartInfo.FileName = "sc.exe";
                    proc.StartInfo.Arguments = processArgs;
                    proc.StartInfo.CreateNoWindow = true;
                    proc.StartInfo.UseShellExecute = false; //Needs to be "false" to redirect output
                    proc.EnableRaisingEvents = true;
                    proc.StartInfo.RedirectStandardError = true;
                    proc.ErrorDataReceived += (sender, args) =>
                    {
                        if (!String.IsNullOrEmpty(args.Data))
                        {
                            errorsWritten = true;
                            Console.Write($"InstallService.Proc.ErrorDataReceived() {args.Data}");
                        }
                    };
                    proc.Exited += (sender, args) =>
                    {
                        Console.WriteLine("InstallService.Proc.Exited() successfully.");
                    };

                    proc.Start();
                    proc.BeginErrorReadLine(); //Need to call before proc.WaitForExit()
                    proc.WaitForExit();

                    if (errorsWritten)
                        success = false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"InstallService EXCEPTION: {ex}");

                    success = false;
                    proc.Kill();
                }
            }

            return success;
        }

        private static Tuple<List<KeyValuePair<string, string>>, List<KeyValuePair<string, string>>> ParseConfigFile(string filePath)
        {
            var configFile = File.ReadAllText(filePath);

            var natsConfig = configFile.Split(new string[] { "\n\n" }, StringSplitOptions.None)[0];
            var re = new Regex(@"(?<key>\w+):(?<value>.+[^\s])", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            MatchCollection mcNats = re.Matches(natsConfig);

            var natsConfigCollection = new List<KeyValuePair<string, string>>();
            foreach (Match m in mcNats)
            {
                natsConfigCollection.Add(new KeyValuePair<string, string>(m.Groups["key"].Value, m.Groups["value"].Value.Replace("\"", "")));
            }

            var streamingConfig = configFile.Split(new string[] { "\n\n" }, StringSplitOptions.None)[1];
            MatchCollection mcStreaming = re.Matches(streamingConfig);
            var streamingConfigCollection = new List<KeyValuePair<string, string>>();
            foreach (Match m in mcStreaming)
            {
                streamingConfigCollection.Add(new KeyValuePair<string, string>(m.Groups["key"].Value, m.Groups["value"].Value.Replace("\"", "")));
            }

            return Tuple.Create(natsConfigCollection, streamingConfigCollection);
        }
    }
}