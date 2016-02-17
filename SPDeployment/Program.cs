using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPDeployment
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine();
            Console.WriteLine("SPDeployment v{0}", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3));
            Console.WriteLine("===================");
            Console.WriteLine();

            try
            {
                var deployer = new Deployer();

                var watch = false;
                if (args.Length > 0)
                {
                    var arg = args[0];

                    if (arg == "/?" || arg == "?" || arg == "-?" || arg == "-help" || arg == "--help")
                    {
                        ShowHelp();
                        Environment.ExitCode = 400;
                        return;
                    }

                    var byName = arg.StartsWith("name:");
                    var byEnvironment = arg.StartsWith("env:");

                    var name = string.Empty;
                    if (byName || byEnvironment)
                        name = arg.Substring(arg.IndexOf(':') + 1);

                    watch = arg.ToLower() == "watch";
                    if (args.Length == 2)
                        watch = args[1].ToLower() == "watch";

                    if (byName)
                    {
                        deployer.DeployByName(name, watch);
                    }
                    else if (byEnvironment)
                    {
                        deployer.DeployByEnvironment(name, watch);
                    }
                    else
                    {
                        if (watch)
                        {
                            deployer.DeployAll(watch);
                        }
                        else {
                            ShowHelp();
                            Environment.ExitCode = 400;
                            return;
                        }
                    }
                }
                else
                {
                    deployer.DeployAll();
                }

                if (watch)
                {
                    Console.ResetColor();
                    Console.WriteLine("Press CTRL+C to quit");
                    Console.ReadLine();
                }

                Environment.ExitCode = 0;
            }
            catch (ApplicationException)
            {
                Environment.ExitCode = 500;
            }
            catch
            {
                ShowHelp();
                Environment.ExitCode = 500;
            }
            finally
            {
                Console.ResetColor();
            }
        }

        private static void ShowHelp()
        {
            var exeName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
            Console.WriteLine("Usage");
            Console.WriteLine("---------------------");
            Console.WriteLine("{0}.exe\t\t\tDeploys default environment from SPDeployment.json", exeName);
            Console.WriteLine("{0}.exe watch\t\t\tDeploys default environment from SPDeployment.json and watch for changes", exeName);
            Console.WriteLine("{0}.exe name:SITENAME\t\tDeploys site with SITENAME", exeName);
            Console.WriteLine("{0}.exe name:SITENAME watch\tDeploys site with SITENAME and watch for changes", exeName);
            Console.WriteLine("{0}.exe env:ENVNAME\t\tDeploys all sites with environment ENVNAME", exeName);
            Console.WriteLine("{0}.exe env:ENVNAME watch\tDeploys all sites with environment ENVNAME and watch for changes", exeName);
        }
    }
}
