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
        //public static async Task GetProjectFilesAsync(string repo, string owner, string branch, string applicationFolder)
        //{
        //    string ghtoken = "4bc4b5e44039c91d404439f0dc384fb000ab4e8c";
        //    var ghClient = new GitHubClient(new Octokit.ProductHeaderValue("dotnetconsoleapp-migrate"));
        //    ghClient.Credentials = new Octokit.Credentials(ghtoken);

        //    //Look for files with .csproj extension  
        //    var existingFile = ghClient.Repository.Content.GetAllContentsByRef(owner, repo, applicationFolder, branch).Result;

        //    for (int i = 0; i < existingFile.Count; i++)
        //    {
        //        if (existingFile[i].Name.Contains(".csproj"))
        //        {
        //            string appFolder = applicationFolder + "/" + existingFile[i].Name;
                    
        //            using(HttpClient _httpClient=new HttpClient())
        //            {
        //                using (HttpResponseMessage response = _httpClient.GetAsync(existingFile[i].DownloadUrl).Result)
        //                {
        //                    response.EnsureSuccessStatusCode();
        //                    string responseBody = await response.Content.ReadAsStringAsync();
        //                    string oldNetVersion = "<TargetFramework>net48</TargetFramework>";
        //                    string newNetVersion= "<TargetFramework>net50</TargetFramework>";
        //                    responseBody = responseBody.Replace(oldNetVersion, newNetVersion);

        //                    var res= ghClient.Repository.Content.GetAllContents(owner, repo, appFolder).Result;

        //                    // update the file
        //                    var updateChangeSet = ghClient.Repository.Content.UpdateFile(owner, repo, existingFile[i].Name,
        //                        new UpdateFileRequest("API File update", responseBody, existingFile[i].Sha, branch)).Result;
        //                }
        //            }
                    
        //        }
        //    }

        //}

        public static async Task CloneRepository(string repo, string owner)
        {
            try
            {
                Console.WriteLine("Cloning the repository to a local volume");
                string ghtoken = "4bc4b5e44039c91d404439f0dc384fb000ab4e8c";
                var ghClient = new GitHubClient(new Octokit.ProductHeaderValue("dotnetconsoleapp-migrate"));
                ghClient.Credentials = new Octokit.Credentials(ghtoken);

                var archiveBytes = ghClient.Repository.Content.GetArchive(owner, repo, ArchiveFormat.Zipball).Result;
                string path = @"C:\Users\SormitaChakraborty\source\repos\DotNetMigration\WorkingDirectory\" + repo + ".zip";
                string extractPath = @"C:\Users\SormitaChakraborty\source\repos\DotNetMigration\WorkingDirectory\" + repo;
                File.WriteAllBytes(path, archiveBytes);

                Console.WriteLine("Extracting the files to a local volume");
                //Extract the files
                System.IO.Compression.ZipFile.ExtractToDirectory(path, extractPath);
                ModifyFilesToFitMigration(extractPath, owner).Wait();
            }
            catch(Exception ex)
            {
                throw ex;
            }
            
        }

        public static async Task ModifyFilesToFitMigration(string filePath,string owner)
        {
            ModifyDotNetVersion(filePath);

            UpdatePackageReference(filePath).Wait();

            UpdateDockerFile(filePath);

            //Check in the file to a new repository
            CommitFileToRepository(owner, filePath);

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

        public static async Task CommitFileToRepository(string owner,string filePath)
        {
            try
            {
                Console.WriteLine("Creating the new repository in GitHub.");
                var newRepo = new NewRepository("MigratedConsoleApp")
                {
                    AutoInit = true,
                    HasIssues = false,
                    HasWiki = true,
                    Private = false
                };

                string ghtoken = "4bc4b5e44039c91d404439f0dc384fb000ab4e8c";
                var ghClient = new GitHubClient(new Octokit.ProductHeaderValue("MigratedConsoleApp"));
                ghClient.Credentials = new Octokit.Credentials(ghtoken);

                var repositoryResponse = ghClient.Repository.Create(newRepo).Result;
                string cloneUrl = repositoryResponse.CloneUrl.ToString();


                CommitAllChanges("Commiting the migrated project", filePath, cloneUrl).Wait();
            }
            catch(Exception ex)
            {
                throw;
            }
            
        }

        public static async Task CommitAllChanges(string message,string filePath, string cloneUrl)
        {
            try
            {
                Console.WriteLine("Commit all changes to GitHub.");
                var _folder = new DirectoryInfo(filePath);
                string path = LibGit2Sharp.Repository.Init(_folder.FullName);
                using (var repo = new LibGit2Sharp.Repository(path))
                {                    
                    var files = _folder.GetFiles("*", SearchOption.AllDirectories).Select(f => f.FullName);
                    Commands.Stage(repo, "*");

                    repo.Commit(message, new LibGit2Sharp.Signature("sormita", "sormita@gmail.com", DateTimeOffset.Now),
                         new LibGit2Sharp.Signature("sormita", "sormita@gmail.com", DateTimeOffset.Now));

                    //push files                
                    string name = "origin";
                    repo.Network.Remotes.Add(name, cloneUrl);
                    var remote = repo.Network.Remotes.FirstOrDefault(r => r.Name == name);

                    var options = new PushOptions
                    {
                        CredentialsProvider = (_url, _user, _cred) =>
                            new UsernamePasswordCredentials { Username = "sormita@gmail.com", Password = "0b60084fead07fe92c1f583ff5a574882400c242" }
                    };

                    var fetchOptions = new FetchOptions {
                        CredentialsProvider = (_url, _user, _cred) =>
                            new UsernamePasswordCredentials { Username = "sormita@gmail.com", Password = "0b60084fead07fe92c1f583ff5a574882400c242" }
                    };

                    string pushRefSpec = @"+refs/heads/master";

                    List<string> fetchString = new List<string>();
                    fetchString.Add(pushRefSpec);

                    repo.Network.Fetch("origin", fetchString, fetchOptions);
                                        
                    repo.Network.Push(remote, pushRefSpec, options);
                }
            }
            catch (Exception ex)
            {
                throw;
            }
            
            
        }
    }
}
