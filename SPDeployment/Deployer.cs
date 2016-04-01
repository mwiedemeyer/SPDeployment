using Microsoft.SharePoint.Client;
using OfficeDevPnP.Core.Extensions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Security;

namespace SPDeployment
{
    internal class Deployer
    {
        private const string DEPLOYMENT_CONFIG_JSON = "spdeployment.json";
        private const string DEPLOYMENT_CREDENTIAL_JSON = "spdeployment.credentials.json";

        private DeploymentConfiguration _deploymentConfiguration;
        private CredentialConfiguration _credentialConfiguration;

        private List<FileSystemWatcher> _watcherCache = new List<FileSystemWatcher>();
        private Dictionary<string, Tuple<DeploymentSite, DeploymentFile>> _registeredSources = new Dictionary<string, Tuple<DeploymentSite, DeploymentFile>>();


        public Deployer()
        {
            try
            {
                var deploymentConfigContent = System.IO.File.ReadAllText(DEPLOYMENT_CONFIG_JSON);
                _deploymentConfiguration = JsonConvert.DeserializeObject<DeploymentConfiguration>(deploymentConfigContent);
            }
            catch (IOException ex)
            {
                Log("Stop Error: {0}", ConsoleColor.Red, ex.Message);
                Console.ResetColor();
                throw new ApplicationException("Error initializing deployment system");
            }

            try
            {
                if (System.IO.File.Exists(DEPLOYMENT_CREDENTIAL_JSON))
                {
                    var deploymentCredentialContent = System.IO.File.ReadAllText(DEPLOYMENT_CREDENTIAL_JSON);
                    _credentialConfiguration = JsonConvert.DeserializeObject<CredentialConfiguration>(deploymentCredentialContent);
                }
            }
            catch {/* ignore errors for credentials config */}
        }

        public void DeployAll(bool watch = false)
        {
            if (string.IsNullOrEmpty(_deploymentConfiguration.DefaultEnvironment) || _deploymentConfiguration.DefaultEnvironment.ToUpper() == "ALL")
                Deploy(watch: watch);
            else
                Deploy(null, _deploymentConfiguration.DefaultEnvironment, watch);
        }

        public void DeployByName(string name = null, bool watch = false)
        {
            Deploy(name, null, watch);
        }

        public void DeployByEnvironment(string name = null, bool watch = false)
        {
            Deploy(null, name, watch);
        }

        private void Deploy(string name = null, string environment = null, bool watch = false)
        {
            try
            {
                IEnumerable<DeploymentSite> sitesToDeploy = null;

                if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(environment))
                    sitesToDeploy = _deploymentConfiguration.Sites;
                else if (!string.IsNullOrEmpty(name))
                    sitesToDeploy = _deploymentConfiguration.Sites.Where(p => p.Name == name);
                else if (!string.IsNullOrEmpty(environment))
                    sitesToDeploy = _deploymentConfiguration.Sites.Where(p => p.Environment == environment);

                if (sitesToDeploy == null || sitesToDeploy.Count() == 0)
                {
                    Log("Nothing to deploy!", ConsoleColor.Red);
                    return;
                }

                Log("Deployment started for {0}", ConsoleColor.White, !string.IsNullOrEmpty(name) ? name.ToUpper() : (!string.IsNullOrEmpty(environment) ? "environment " + environment.ToUpper() : "ALL sites"));

                foreach (var site in sitesToDeploy)
                {
                    Log("Deploying {0}...", ConsoleColor.Yellow, site.Name);

                    using (var context = GetClientContext(site))
                    {
                        foreach (var fileConfig in site.Files)
                        {
                            Log("... from {0} to {1}", ConsoleColor.DarkGray, fileConfig.Source, fileConfig.Destination);

                            var requiresPublishing = false;
                            if (!site.FastMode)
                            {
                                var destFolder = context.Web.EnsureFolderPath(fileConfig.Destination);

                                try
                                {
                                    var destinationList = context.Web.GetListByUrl(fileConfig.Destination);
                                    context.Load(destinationList, p => p.EnableMinorVersions);
                                    context.ExecuteQuery();
                                    requiresPublishing = destinationList.EnableMinorVersions;
                                }
                                catch { }
                            }

                            string[] excludeSplit = null;
                            if (!string.IsNullOrEmpty(fileConfig.Exclude))
                                excludeSplit = fileConfig.Exclude.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                            string[] includeSplit = null;
                            if (!string.IsNullOrEmpty(fileConfig.Include))
                                includeSplit = fileConfig.Include.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                            var folderCache = new Dictionary<string, Folder>();

                            foreach (var localFile in Directory.GetFiles(fileConfig.Source, "*.*", SearchOption.AllDirectories))
                            {
                                if (excludeSplit != null)
                                {
                                    var excludeFile = false;
                                    foreach (var exc in excludeSplit)
                                    {
                                        if (Regex.Match(localFile, exc, RegexOptions.IgnoreCase).Success)
                                        {
                                            excludeFile = true;
                                            break;
                                        }
                                    }
                                    if (excludeFile)
                                    {
                                        Log("...... {0} skipped by exclude pattern", ConsoleColor.DarkYellow, localFile);
                                        continue;
                                    }
                                }

                                var filename = Path.GetFileName(localFile);
                                var localDir = Path.GetDirectoryName(localFile);
                                localDir = localDir.Replace(fileConfig.Source, "").Replace("\\", "/");
                                var remoteFolderPath = fileConfig.Destination + localDir;

                                Folder remoteFolder = null;
                                if (!folderCache.ContainsKey(remoteFolderPath))
                                {
                                    remoteFolder = context.Web.EnsureFolderPath(remoteFolderPath);
                                    folderCache.Add(remoteFolderPath, remoteFolder);
                                }
                                remoteFolder = folderCache[remoteFolderPath];

                                var remoteFile = remoteFolder.ServerRelativeUrl + (remoteFolder.ServerRelativeUrl.EndsWith("/") ? string.Empty : "/") + filename;

                                if (!site.FastMode && fileConfig.Destination != "/")
                                    context.Web.CheckOutFile(remoteFile);

                                remoteFolder.UploadFile(filename, localFile, true);

                                if (!site.FastMode && fileConfig.Destination != "/")
                                    context.Web.CheckInFile(remoteFile, CheckinType.MajorCheckIn, "SPDeployment");

                                if (requiresPublishing)
                                    context.Web.PublishFile(remoteFile, "SPDeployment");

                                Log("...... {0} deployed successfully", ConsoleColor.DarkGreen, remoteFile);
                            }
                        }
                    }
                    if (watch)
                        RegisterWatchTask(site);
                }

                if (watch)
                    Log("Completed successfully. Watching for changes...", ConsoleColor.Green);
                else
                    Log("Completed successfully", ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                Log("Stop Error: {0}", ConsoleColor.Red, ex.ToString());
                Console.ResetColor();
                throw new ApplicationException();
            }
        }

        private void RegisterWatchTask(DeploymentSite site)
        {
            var fsWatcher = new List<FileSystemWatcher>();
            foreach (var fileConfig in site.Files)
            {
                var fs = new FileSystemWatcher(fileConfig.Source);
                fs.IncludeSubdirectories = true;
                fs.NotifyFilter = NotifyFilters.LastWrite;
                fs.Changed += fs_Changed;
                fs.EnableRaisingEvents = true;
                _watcherCache.Add(fs);

                _registeredSources.Add(fileConfig.Source.ToUpperInvariant(), new Tuple<DeploymentSite, DeploymentFile>(site, fileConfig));
            }
        }

        private string _watcherLastFullPath;
        private DateTime _watcherLastChange = DateTime.MinValue;
        private void fs_Changed(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType != WatcherChangeTypes.Changed)
                return;

            if (_watcherLastFullPath == e.FullPath && _watcherLastChange.AddSeconds(1) > DateTime.Now)
                return;

            _watcherLastFullPath = e.FullPath;
            _watcherLastChange = DateTime.Now;

            Task.Run(() =>
            {
                var dir = new DirectoryInfo(e.FullPath);
                Tuple<DeploymentSite, DeploymentFile> sourceFound = null;
                while (sourceFound == null && dir != null)
                {
                    if (_registeredSources.ContainsKey(dir.Name.ToUpperInvariant()))
                    {
                        sourceFound = _registeredSources[dir.Name.ToUpperInvariant()];
                        break;
                    }
                    dir = dir.Parent;
                }
                if (sourceFound == null)
                    return;

                var fileConfig = sourceFound.Item2;
                var site = sourceFound.Item1;

                var localFile = e.FullPath;

                string[] excludeSplit = null;
                if (!string.IsNullOrEmpty(fileConfig.Exclude))
                    excludeSplit = fileConfig.Exclude.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                string[] includeSplit = null;
                if (!string.IsNullOrEmpty(fileConfig.Include))
                    includeSplit = fileConfig.Include.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                if (excludeSplit != null)
                {
                    var excludeFile = false;
                    foreach (var exc in excludeSplit)
                    {
                        if (Regex.Match(localFile, exc, RegexOptions.IgnoreCase).Success)
                        {
                            excludeFile = true;
                            break;
                        }
                    }
                    if (excludeFile)
                        return;
                }
                if (includeSplit != null)
                {
                    var excludeFile = false;
                    foreach (var inc in includeSplit)
                    {
                        if (!Regex.Match(localFile, inc, RegexOptions.IgnoreCase).Success)
                        {
                            excludeFile = true;
                            break;
                        }
                    }
                    if (excludeFile)
                        return;
                }

                var filename = Path.GetFileName(localFile);

                Log("...... Deploying {0}...", ConsoleColor.DarkGray, filename);

                var localDir = Path.GetDirectoryName(localFile);
                localDir = localDir.Replace(fileConfig.Source, "").Replace("\\", "/");
                var remoteFolderPath = fileConfig.Destination + localDir;

                using (var context = GetClientContext(site))
                {
                    var remoteFolder = context.Web.EnsureFolderPath(remoteFolderPath);
                    var remoteFile = remoteFolder.ServerRelativeUrl + (remoteFolder.ServerRelativeUrl.EndsWith("/") ? string.Empty : "/") + filename;
                    remoteFolder.UploadFile(filename, localFile, true);
                    Log("...... {0} deployed successfully", ConsoleColor.DarkGreen, remoteFile);
                }
            });
        }

        private ClientContext GetClientContext(DeploymentSite site)
        {
            var context = new ClientContext(site.Url);

            var username = string.IsNullOrEmpty(_credentialConfiguration?.Username) ? site.Username : _credentialConfiguration?.Username;
            var password = string.IsNullOrEmpty(_credentialConfiguration?.Password) ? site.Password : _credentialConfiguration?.Password;

            if (string.IsNullOrEmpty(username))
            {
                Console.ResetColor();
                Console.WriteLine("Please enter username for {0}", site.Url);
                username = Console.ReadLine();
            }
            if (string.IsNullOrEmpty(password))
            {
                Console.ResetColor();
                Console.WriteLine("Please enter password for user {0} and site {1}", username, site.Url);
                ConsoleKeyInfo key;
                string pw = "";
                do
                {
                    key = Console.ReadKey(true);
                    if (key.Key != ConsoleKey.Enter)
                        pw += key.KeyChar;
                    Console.Write("*");
                }
                while (key.Key != ConsoleKey.Enter);
                Console.WriteLine();
                password = pw;
            }

            if (site.Url.ToUpper().Contains("SHAREPOINT.COM"))
            {
                var securePassword = new SecureString();
                foreach (char c in password) securePassword.AppendChar(c);
                context.Credentials = new SharePointOnlineCredentials(username, securePassword);
            }
            else
            {
                context.Credentials = new System.Net.NetworkCredential(username, password);
            }
            context.ExecutingWebRequest += (sender, e) => { e.WebRequestExecutor.WebRequest.PreAuthenticate = true; };
            return context;
        }

        private void Log(string message, ConsoleColor? color = null, params object[] args)
        {
            Console.ResetColor();
            if (color.HasValue)
                Console.ForegroundColor = color.Value;
            Console.WriteLine(message, args);
        }
    }
}
