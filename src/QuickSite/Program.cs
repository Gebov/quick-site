using CommandLine;
using Microsoft.Web.Administration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Permissions;
using System.Security.Principal;
using System.Text;

namespace QuickSite
{

    class Options
    {
        [Option('a', "add", Required = false, HelpText = "Adds the site to iis.")]
        public bool Add { get; set; }

        [Option('r', "remove", Required = false, HelpText = "Removes the site from iis.")]
        public bool Remove { get; set; }

        [Option('d', "dir", Required = true, HelpText = "The directory to make a site.")]
        public string Directory { get; set; }
    }

    class Program
    {
       // [PrincipalPermission(SecurityAction.Demand, Role = @"BUILTIN\Administrators")]
        static void Main(string[] args)
        {
            var options = new Options();
            if (CommandLine.Parser.Default.ParseArguments(args, options))
            {
                var dirName = Path.GetFileName(options.Directory);

                if (options.Remove)
                {
                    RemoveSite(options.Directory, dirName);
                }
                else if (options.Add)
                {
                    AddAndStartSite(options.Directory, dirName);
                }
                else
                {
                    ToggleSite(options.Directory, dirName);
                }
            }
        }

        private static void ToggleSite(string dirPath, string siteName)
        {
            var server = new ServerManager();
            var site = server.Sites.FirstOrDefault(x => x.Name == siteName);
            if (site != null)
                RemoveSite(dirPath, siteName);
            else
                AddAndStartSite(dirPath, siteName);
        }

        private static void RemoveSite(string dirPath, string siteName)
        {
            var server = new ServerManager();

            var site = server.Sites.FirstOrDefault(x => x.Name == siteName);
            if (site != null)
            {
                server.Sites.Remove(site);

                foreach (var app in site.Applications)
                {
                    var appPoolName = app.ApplicationPoolName;

                    bool isLastApp = true;
                    foreach (var siteEntry in server.Sites)
                    {
                        foreach (var appEntry in siteEntry.Applications)
                        {
                            if (string.Equals(appEntry.ApplicationPoolName, appPoolName))
                            {
                                isLastApp = false;
                                break;
                            }
                        }
                    }

                    if (isLastApp)
                    {
                        var appPool = server.ApplicationPools.FirstOrDefault(x => x.Name == appPoolName);
                        if (appPool != null)
                            server.ApplicationPools.Remove(appPool);
                    }    
                }
                
                server.CommitChanges();
                Trace.TraceInformation("Successfully removed site");
            }
            
        }

        private static void AddAndStartSite(string dirPath, string siteName)
        {
            var siteUrl = AddSite(dirPath, siteName);

            var startInfo = new ProcessStartInfo("explorer.exe", siteUrl);
            Process.Start(startInfo);
        }
        
        private static string AddSite(string dirPath, string siteName)
        {
            var files = Directory.GetFiles(dirPath);
            var webConfigExists = files.Select(x => new FileInfo(x)).Any(x => string.Equals(x.Name, "web.config", StringComparison.OrdinalIgnoreCase));
            if (!webConfigExists)
                return null;

            var server = new ServerManager();

            int port = 0;
            var site = server.Sites.FirstOrDefault(x => x.Name == siteName);
            if (site == null && TryGetFreePort(server, out port))
            {
                GrantSecurity(dirPath);

                var appPool = server.ApplicationPools.FirstOrDefault(x => x.Name == siteName);
                if (appPool == null)
                {
                    var poolName = string.Concat(siteName, "_pool");
                    appPool = server.ApplicationPools.Add(poolName);
                    appPool.ProcessModel.IdentityType = ProcessModelIdentityType.NetworkService;
                    appPool.ProcessModel.IdleTimeout = TimeSpan.FromMinutes(43200);
                    appPool.Recycling.PeriodicRestart.Time = TimeSpan.FromMinutes(432000);
                    appPool.Failure.OrphanWorkerProcess = true;
                    appPool.AutoStart = true;
                }

                site = server.Sites.Add(siteName, dirPath, port);

                foreach (var app in site.Applications)
                    app.ApplicationPoolName = appPool.Name;

                server.CommitChanges();
                Trace.TraceInformation("Successfully added site at port {0}", port);
                return string.Format("http://localhost:{0}", port);
            }
            else
                Trace.TraceInformation("Site already exists.");

            return null;
        }

        private static void GrantSecurity(string dirPath)
        {
            var sec = Directory.GetAccessControl(dirPath);
            // Using this instead of the "Everyone" string means we work on non-English systems.
            var everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            sec.AddAccessRule(new FileSystemAccessRule(everyone, FileSystemRights.Modify | FileSystemRights.Synchronize, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            Directory.SetAccessControl(dirPath, sec);
        }

        private static bool TryGetFreePort(ServerManager server, out int port)
        {
            port = default(int);

            var ports = new HashSet<int>();
            foreach (var site in server.Sites)
            {
                foreach (var binding in site.Bindings)
                {
                    ports.Add(binding.EndPoint.Port);
                }
            }

            for (int i = 8000; i < ushort.MaxValue + 1; i++)
            {
                if (!ports.Contains(i))
                {
                    port = i;
                    return true;
                }
            }

            Trace.TraceError("No ports available to set up site");
            return false;
        }

    }
}
