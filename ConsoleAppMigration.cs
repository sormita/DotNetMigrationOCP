using System.Collections.Generic;
using Octokit;
using System.Threading.Tasks;
using System.Xml;
using System.Net.Http;
using System.IO;
using LibGit2Sharp;
using System.Linq;
using System;
using System.Configuration;

namespace DotNetMigrationTool
{
    public static class ConsoleAppMigration
    {
        public static async Task MigrateConsoleApp(string repo, string owner,string file_path)
        {
            var extractPath= GitMethods.CloneRepository(repo, owner, file_path).Result;
            ModifyFilesToFitMigration(extractPath.ToString(), owner).Wait();
        }
        

        public static async Task ModifyFilesToFitMigration(string filePath,string owner)
        {
            ModifyDotNetVersion(filePath);

            UpdatePackageReference(filePath).Wait();

            UpdateDockerFile(filePath);

            //Check in the file to a new repository
            GitMethods.CommitFileToRepository(owner, filePath,"MigratedConsoleApp");

        }

        public static void ModifyDotNetVersion(string filePath)
        {
            try
            {
                Console.WriteLine("Modifying the dotnet version");
                //Modify csproj file to update the version to core 5.0
                string[] files = Directory.GetFiles(filePath, "*.csproj", SearchOption.AllDirectories);
                string newNetVersion = "<TargetFramework>net50</TargetFramework>";

                for (int i = 0; i < files.Length; i++)
                {
                    string text = File.ReadAllText(files[i]);
                    string subText = text.Substring(text.IndexOf("<TargetFramework>"), 40);

                    if ((subText.Length > 0)&&(!(subText.Contains("netstandard2.0"))))
                    {
                        string oldNetVersion = subText;

                        text = text.Replace(oldNetVersion, newNetVersion);
                    }
                    else
                    {
                        subText = text.Substring(text.IndexOf("<TargetFramework>"), 49);
                        text = text.Replace(subText, newNetVersion);
                    }


                    File.WriteAllText(files[i], text);
                    Console.WriteLine("Completed modifying the dotnet version");
                }
            }
            catch(Exception ex)
            {
                throw ex;
            }
            
        }

        public static async Task UpdatePackageReference(string filePath)
        {
            try
            {
                Console.WriteLine("Updating the package reference.");
                string[] files = Directory.GetFiles(filePath, "*.csproj", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    string text = File.ReadAllText(files[i]);
                    XmlDocument xdoc = new XmlDocument();
                    xdoc.LoadXml(text);
                    if (xdoc.GetElementsByTagName("PackageReference").Count > 0)
                    {
                        for (int j = 0; j < xdoc.GetElementsByTagName("PackageReference").Count; j++)
                        {                            
                            string[] packageNameArray = xdoc.GetElementsByTagName("PackageReference")[j].OuterXml.Split('=');
                            string strPackageName = packageNameArray[1].Replace(" Version", "").Replace('"',' ').Trim();
                            string strOldVersion = packageNameArray[2];
                            string latestVersion = null;
                            var nugetUrl = ConfigurationManager.AppSettings["packageManager"];
                            var url = nugetUrl + strPackageName + "/index.json";

                            using (HttpClient _httpClient = new HttpClient())
                            {
                                using (HttpResponseMessage response = _httpClient.GetAsync(url).Result)
                                {
                                    response.EnsureSuccessStatusCode();
                                    string responseBody = await response.Content.ReadAsStringAsync();
                                    responseBody = responseBody.Substring(responseBody.IndexOf('['));
                                    responseBody = responseBody.Replace('}', ' ').Replace('[', ' ').Replace(']', ' ');
                                    string[] responses = responseBody.Split(',');
                                    latestVersion = responses[^1].Trim() + "/>"; //This will fetch the latest version of the package from npm
                                }
                            }

                            text = text.Replace(strOldVersion, latestVersion);
                            File.WriteAllText(files[i], text);
                        }
                    }
                }
                Console.WriteLine("Completed updating the package reference.");
            }
            catch(Exception ex)
            {
                throw ex;
            }

        }

        public static void UpdateDockerFile(string filePath)
        {
            try
            {
                Console.WriteLine("Updating the docker file.");
                string[] files = Directory.GetFiles(filePath, "Dockerfile", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    string text = File.ReadAllText(files[i]);
                    text = text.Replace("mcr.microsoft.com/dotnet/framework/sdk:4.8", "mcr.microsoft.com/dotnet/sdk:5.0");
                    text = text.Replace("mcr.microsoft.com/dotnet/framework/runtime:4.8", "mcr.microsoft.com/dotnet/runtime:5.0");
                    File.WriteAllText(files[i], text);
                }
                Console.WriteLine("Completed updating the docker file.");
            }
            catch(Exception ex)
            {
                throw ex;
            }            
        }

        
    }
}
