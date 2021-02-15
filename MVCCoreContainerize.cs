using Microsoft.CSharp;
using Newtonsoft.Json;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DotNetMigrationTool
{
    public static class MVCCoreContainerize
    {
        public static async Task ContainerApplication(string repo, string owner, string file_path)
        {
            var extractPath = GitMethods.CloneRepository(repo, owner, file_path).Result;
            BuildClonedRepo(file_path);
            GetSolutionVersion(extractPath.ToString(), owner).Wait();           
        }

        public static void BuildClonedRepo(string file_path)
        {
            try
            {                
                string path = @"C:\Users\SormitaChakraborty\source\repos\IBMDotNetMigration\dotnetmigration\WorkingDirectory\dotnetcorecontainerize\sormita-dotnetcorecontainerize-28f5e16" + "\\InventoryManagement.sln";
                //   var result= new Microsoft.Build.Execution.ProjectInstance(path).Build();
                string strCmdText="/C dotnet build "+ path;
                System.Diagnostics.Process.Start("CMD.exe", strCmdText);
            }
            catch(Exception ex)
            {
                throw;
            }
        }

        public static async Task GetSolutionVersion(string filePath, string owner)
        {
            Console.WriteLine("Modifying the dotnet version");

            string[] files = Directory.GetFiles(filePath, "*.csproj", SearchOption.AllDirectories);

            for (int i = 0; i < files.Length; i++)
            {
                string text = File.ReadAllText(files[i]);
                string subText = text.Substring(text.IndexOf("<TargetFramework>"), 40);
                subText=subText.Substring(subText.IndexOf('n'), "netcoreapp3.1".Length);
                string dockerText = null;
                string deploymentFile = GitMethods.GetDeploymentFileAsync(owner).Result;
                string serviceFile = GitMethods.GetServiceFileAsync(owner).Result;
                int port = 0;

                switch (subText)
                {
                    case "netcoreapp3.1":
                        {
                            Console.WriteLine("Download docker file for netcoreapp3.1");
                            dockerText=GitMethods.GetDockerFileAsync(owner, "Dockerfile_31").Result;
                            port=await CreateDockerFile(dockerText, filePath);                            
                            CreateDeploymentFile(deploymentFile,filePath).Wait();
                            CreateServiceFile(serviceFile, port, filePath).Wait();
                            GitMethods.CommitFileToRepository(owner, filePath, "ContainerizedDotNetCoreApp").Wait();
                            break;
                        }                        
                    case "netcoreapp5.0":
                        Console.WriteLine("Download docker file for netcoreapp5.0");
                        GitMethods.GetDockerFileAsync(owner, "Dockerfile_50").Wait();
                        break;
                    default:
                        Console.WriteLine("Default case");
                        break;
                }
            }

        }

        public static async Task<int> CreateDockerFile(string dockerText,string filePath)
        {
            Console.WriteLine("Creating the docker file");
            int port= GetPortNumber(filePath);
            string dockerNewText = dockerText.Replace("{port_number}", port.ToString());
            string solName=GetSolutionName(filePath);
            dockerNewText = dockerNewText.Replace("{projectname}", solName);
            //save file here
            await File.WriteAllTextAsync(filePath + "//Dockerfile", dockerNewText);

            return port;
        }

        public static async Task CreateDeploymentFile(string deploymentText,string filePath)
        {
            Console.WriteLine("Creating the deployment file");
            List<string> _connectionStrings =GetConnectionString(filePath);
            string deploymentNewText = deploymentText.Replace("{connectionstringname}",_connectionStrings[0].Trim().Replace(" ",string.Empty));
            deploymentNewText= deploymentNewText.Replace("{connectionstring}", _connectionStrings[1].Trim());
            //save file here
            await File.WriteAllTextAsync(filePath + "//deployment.yaml", deploymentNewText);
        }

        public static async Task CreateServiceFile(string serviceText, int port, string filePath)
        {
            Console.WriteLine("Creating the Service file");
            string serviceNewText = serviceText.Replace("{portnumber}", port.ToString());
            //save file here
            await File.WriteAllTextAsync(filePath + "//service.yaml", serviceNewText);
        }

        public static List<string> GetConnectionString(string filePath)
        {
            string[] files = Directory.GetFiles(filePath, "appsettings.json", SearchOption.AllDirectories);

            string text = File.ReadAllText(files[0]);
            text = text.Substring(text.IndexOf("ConnectionStrings"));
            string[] texts = text.Split(':');
            string connectString= texts[texts.Length - 1].Replace("}", " ").Replace('"',' ');
            string connectStringName = texts[texts.Length - 2];
            List<string> connectDetails = new List<string>();
            connectDetails.Add(connectStringName);
            connectDetails.Add(connectString);
            return connectDetails;
        }

        public static int GetPortNumber(string filePath)
        {            
            string[] files = Directory.GetFiles(filePath, "launchSettings.json", SearchOption.AllDirectories);

            string text = File.ReadAllText(files[0]);
            string[] texts = text.Substring(text.IndexOf("applicationUrl"), text.IndexOf(',')).Split(',');
            int port = Int32.Parse(texts[0].Substring(texts[0].LastIndexOf(':') + 1).Replace('"', ' ').Trim());
            return port;
        }

        public static string GetSolutionName(string filePath)
        {
            string[] files = Directory.GetFiles(filePath, "*.sln", SearchOption.AllDirectories);
            string[] texts = files[0].Split(@"\");
            string strSolName = texts[texts.Length - 1].Replace(".sln", " ");
            return strSolName;
        }
    }
}
