using CommandLine;
using Microsoft.Web.Administration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using System.Threading;

namespace QuickSite
{

    class Options
    {
        [Option('a', "add", Required = false, HelpText = "Adds the site to iis.")]
        public bool Add { get; set; }

        [Option('r', "remove", Required = false, HelpText = "removes the site from iis.")]
        public bool Remove { get; set; }

        [Option('d', "dir", Required = true, HelpText = "The directory to make a site.")]
        public string Directory { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var options = new Options();
            if (CommandLine.Parser.Default.ParseArguments(args, options))
            {
                var dirName = Path.GetFileName(options.Directory);

                var files = Directory.GetFiles(options.Directory);
                var webConfigExists = files.Select(x => new FileInfo(x)).Any(x => string.Equals(x.Name, "web.config", StringComparison.OrdinalIgnoreCase));
                if (!webConfigExists)
                    return;

                var server = new ServerManager();

                if (options.Remove)
                {
                    RemoveSite(server, options.Directory, dirName);
                }
                else if (options.Add)
                {
                    AddSite(server, options.Directory, dirName);
                }
                else
                {
                    ToggleSite(server, options.Directory, dirName);
                }
            }
        }

        private static void ToggleSite(ServerManager server, string dirPath, string siteName)
        {
            var site = server.Sites.FirstOrDefault(x => x.Name == siteName);
            if (site != null)
                RemoveSite(server, dirPath, siteName);
            else
                AddSite(server, dirPath, siteName);
        }

        private static void RemoveSite(ServerManager server, string dirPath, string siteName)
        {
            var sitesToRemove = new List<Site>();

            foreach (var site in server.Sites)
            {
                foreach (var app in site.Applications)
                {
                    if (app.ApplicationPoolName == siteName)
                    {
                        sitesToRemove.Add(site);
                        break;
                    }
                }
            }

            foreach (var site in sitesToRemove)
            {
                server.Sites.Remove(site);
            }

            var appPool = server.ApplicationPools.FirstOrDefault(x => x.Name == siteName);
            if (appPool != null && appPool.State == ObjectState.Started)
                server.ApplicationPools.Remove(appPool);

            server.CommitChanges();
        }

        private static void AddSite(ServerManager server, string dirPath, string siteName)
        {
            var site = server.Sites.FirstOrDefault(x => x.Name == siteName);
            if (site == null)
            {
                var appPool = server.ApplicationPools.FirstOrDefault(x => x.Name == siteName);
                if (appPool == null)
                    appPool = server.ApplicationPools.Add(siteName);

                var portNumber = GetAvailablePortNumber(server);
                site = server.Sites.Add(siteName, dirPath, portNumber);

                foreach (var app in site.Applications)
                    app.ApplicationPoolName = appPool.Name;

                server.CommitChanges();

                var start = DateTime.UtcNow;
                while (true)
                {
                    try
                    {
                        if (appPool.State != ObjectState.Starting && appPool.State != ObjectState.Started)
                            appPool.Start();

                        if (site.State != ObjectState.Starting && site.State != ObjectState.Started)
                            site.Start();

                        break;
                    }
                    catch
                    {
                        Thread.Sleep(10);
                    }

                    // timeout
                    var now = DateTime.UtcNow;
                    if (now - start > TimeSpan.FromSeconds(5))
                        break; 
                }

                var siteUrl = string.Concat("http://localhost:", portNumber);
                Process.Start(siteUrl);
            }
        }


        private static int GetAvailablePortNumber(ServerManager server)
        {
            int port = 8000;
            foreach (var site in server.Sites)
            {
                foreach (var binding in site.Bindings)
                {
                    if (binding.EndPoint.Port > port)
                        port = binding.EndPoint.Port;
                }
            }

            return ++port;
        }
    }
}
