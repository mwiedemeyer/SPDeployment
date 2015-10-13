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
            Console.WriteLine("============");
            Console.WriteLine();

            try
            {
                var deployer = new Deployer("SPDeployment.json");

                if (args.Length > 0)
                {
                    var arg = args[0];

                    if (arg == "/?" || arg == "?" || arg == "-?" || arg == "-help" || arg == "--help")
                    {
                        Console.WriteLine("Usage");
                        Console.WriteLine("---------------------");
                        Console.WriteLine("{0}.exe\t\t\tDeploys everything from SPDeployment.json", System.Reflection.Assembly.GetExecutingAssembly().GetName().Name);
                        Console.WriteLine("{0}.exe name:SITENAME\t\tDeploys site with SITENAME", System.Reflection.Assembly.GetExecutingAssembly().GetName().Name);
                        Console.WriteLine("{0}.exe env:ENVNAME\t\tDeploys all sites with environment ENVNAME", System.Reflection.Assembly.GetExecutingAssembly().GetName().Name);
                        Environment.ExitCode = 400;
                        return;
                    }

                    var byName = arg.StartsWith("name:");
                    var byEnvironment = arg.StartsWith("env:");

                    var name = arg.Substring(arg.IndexOf(':') + 1);

                    if (byName)
                        deployer.DeployByName(name);
                    if (byEnvironment)
                        deployer.DeployByEnvironment(name);
                }
                else
                {
                    deployer.DeployAll();
                }

                Environment.ExitCode = 0;
            }
            catch
            {
                Environment.ExitCode = 500;
            }
            finally
            {
                Console.ResetColor();
            }
        }
    }
}
