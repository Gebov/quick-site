using CommandLine;
using Microsoft.Web.Administration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Permissions;
using System.Text;

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
                    var files = Directory.GetFiles(options.Directory);
                    var webConfigExists = files.Select(x => new FileInfo(x)).Any(x => string.Equals(x.Name, "web.config", StringComparison.OrdinalIgnoreCase));
                    if (webConfigExists)
                    {
                        AddSite(options.Directory, dirName);
                    }
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
                AddSite(dirPath, siteName);
        }

        private static void RemoveSite(string dirPath, string siteName)
        {
            var server = new ServerManager();

            var site = server.Sites.FirstOrDefault(x => x.Name == siteName);
            if (site != null)
            {
                server.Sites.Remove(site);

                var appPool = server.ApplicationPools.FirstOrDefault(x => x.Name == siteName);
                if (appPool != null)
                    server.ApplicationPools.Remove(appPool);

                server.CommitChanges();
            }
            
        }

        private static void AddSite(string dirPath, string siteName)
        {
            var server = new ServerManager();

            var site = server.Sites.FirstOrDefault(x => x.Name == siteName);
            if (site == null)
            {
                var appPool = server.ApplicationPools.FirstOrDefault(x => x.Name == siteName);
                if (appPool == null)
                    appPool = server.ApplicationPools.Add(siteName);

                site = server.Sites.Add(siteName, dirPath, 8080);

                foreach (var app in site.Applications)
                    app.ApplicationPoolName = appPool.Name;

                server.CommitChanges();
            }
            else
                Console.WriteLine("Site already exists.");
        }
    }
}
