﻿using Microsoft.SharePoint.Client;
using OfficeDevPnP.Core.Extensions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace SPDeployment
{
    internal class Deployer
    {
        private DeploymentConfiguration _deploymentConfiguration;

        public Deployer(string deploymentConfiguration)
        {
            try
            {
                var deploymentConfigContent = System.IO.File.ReadAllText(deploymentConfiguration);
                _deploymentConfiguration = JsonConvert.DeserializeObject<DeploymentConfiguration>(deploymentConfigContent);
            }
            catch (IOException ex)
            {
                Log("Stop Error: {0}", ConsoleColor.Red, ex.Message);
                Console.ResetColor();
                throw new ApplicationException("Error initializing deployment system");
            }
        }

        public void DeployAll()
        {
            if (string.IsNullOrEmpty(_deploymentConfiguration.DefaultEnvironment) || _deploymentConfiguration.DefaultEnvironment.ToUpper() == "ALL")
                Deploy();
            else
                Deploy(null, _deploymentConfiguration.DefaultEnvironment);
        }

        public void DeployByName(string name = null)
        {
            Deploy(name, null);
        }

        public void DeployByEnvironment(string name = null)
        {
            Deploy(null, name);
        }

        private void Deploy(string name = null, string environment = null)
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

                            var destFolder = context.Web.EnsureFolderPath(fileConfig.Destination);

                            var requiresPublishing = false;
                            try
                            {
                                var destinationList = context.Web.GetListByUrl(fileConfig.Destination);
                                context.Load(destinationList, p => p.EnableMinorVersions);
                                context.ExecuteQuery();
                                requiresPublishing = destinationList.EnableMinorVersions;
                            }
                            catch { }

                            string[] excludeSplit = null;
                            if (!string.IsNullOrEmpty(fileConfig.Exclude))
                                excludeSplit = fileConfig.Exclude.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

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

                                if (fileConfig.Destination != "/")
                                    context.Web.CheckOutFile(remoteFile);

                                remoteFolder.UploadFile(filename, localFile, true);

                                if (fileConfig.Destination != "/")
                                    context.Web.CheckInFile(remoteFile, CheckinType.MajorCheckIn, "SPDeployment");

                                if (requiresPublishing)
                                    context.Web.PublishFile(remoteFile, "SPDeployment");

                                Log("...... {0} deployed successfully", ConsoleColor.DarkGreen, remoteFile);
                            }
                        }
                    }
                }

                Log("Completed successfully", ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                Log("Stop Error: {0}", ConsoleColor.Red, ex.ToString());
                Console.ResetColor();
            }
        }

        private ClientContext GetClientContext(DeploymentSite site)
        {
            var context = new ClientContext(site.Url);
            if (string.IsNullOrEmpty(site.Username))
            {
                Console.ResetColor();
                Console.WriteLine("Please enter username for {0}", site.Url);
                site.Username = Console.ReadLine();
            }
            if (string.IsNullOrEmpty(site.Password))
            {
                Console.ResetColor();
                Console.WriteLine("Please enter password for user {0} and site {1}", site.Username, site.Url);
                ConsoleKeyInfo key;
                string password = "";
                do
                {
                    key = Console.ReadKey(true);
                    if (key.Key != ConsoleKey.Enter)
                        password += key.KeyChar;
                    Console.Write("*");
                }
                while (key.Key != ConsoleKey.Enter);
                Console.WriteLine();
                site.Password = password;
            }
            context.Credentials = new System.Net.NetworkCredential(site.Username, site.Password);
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
