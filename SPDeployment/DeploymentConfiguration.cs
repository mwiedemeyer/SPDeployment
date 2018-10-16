using System.Collections.Generic;

namespace SPDeployment
{
    /// <summary>
    /// SPDeployment.json representation
    /// </summary>
    public class DeploymentConfiguration
    {
        public string DefaultEnvironment { get; set; }
        public List<DeploymentSite> Sites { get; set; }
    }

    public class DeploymentSite
    {
        public bool FastMode { get; set; }
        public string Environment { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public List<DeploymentFile> Files { get; set; }
    }

    public class DeploymentFile
    {
        public string Source { get; set; }
        public string Destination { get; set; }
        public string Exclude { get; set; }
        public string Include { get; set; }
        public bool Clean { get; set; }
    }

    /// <summary>
    /// spdeployment.credentials.json representation
    /// </summary>
    public class CredentialConfiguration
    {
        public bool FromChromeCookies { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }

}