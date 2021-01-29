using System.Collections.Generic;
using Octokit;
using System.Threading.Tasks;
using System.Xml;
using System.Net.Http;
using System.IO;
using LibGit2Sharp;
using System.Linq;
using System;

namespace DotNetMigrationTool
{
    public static class ConsoleAppMigration
    {
        //public static async Task GetProjectFilesAsync(string repo, string owner, string branch, string applicationFolder)
        //{
        //    string ghtoken = "2c0d060c49fc52d582739a58dc990f56e171dad5";
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
            string ghtoken = "2c0d060c49fc52d582739a58dc990f56e171dad5";
            var ghClient = new GitHubClient(new Octokit.ProductHeaderValue("dotnetconsoleapp-migrate"));
            ghClient.Credentials = new Octokit.Credentials(ghtoken);

            var archiveBytes = ghClient.Repository.Content.GetArchive(owner, repo,ArchiveFormat.Zipball).Result;
            string path = @"C:\Users\SormitaChakraborty\source\repos\DotNetMigration\WorkingDirectory\"+ repo + ".zip";
            string extractPath = @"C:\Users\SormitaChakraborty\source\repos\DotNetMigration\WorkingDirectory\" + repo;
            File.WriteAllBytes(path, archiveBytes);

            //Extract the files
            System.IO.Compression.ZipFile.ExtractToDirectory(path, extractPath);
            ModifyFilesToFitMigration(extractPath,owner).Wait();
        }

        public static async Task ModifyFilesToFitMigration(string filePath,string owner)
        {
            ModifyDotNetVersion(filePath);

            //Check in the file to a new repository
            CommitFileToRepository(owner, filePath);

        }

        public static void ModifyDotNetVersion(string filePath)
        {
            //Modify csproj file to update the version to core 5.0
            string[] files = Directory.GetFiles(filePath, "*.csproj", SearchOption.AllDirectories);
            string newNetVersion = "<TargetFramework>net50</TargetFramework>";

            for (int i = 0; i < files.Length; i++)
            {
                string text = File.ReadAllText(files[i]);
                string subText = text.Substring(text.IndexOf("<TargetFramework>"), 40);

                if (subText.Length > 0)
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
            }
        }

        //public static void UpdatePackageReference(string filePath)
        //{
        //    string package
        //}

        public static void UpdateDockerFile(string filePath)
        {

        }

        public static async Task CommitFileToRepository(string owner,string filePath)
        {
            var newRepo = new NewRepository("MigratedConsoleApp")
            {
                AutoInit = true,
                Description = "This is the repository for the migrated console application",
                HasIssues = false,
                HasWiki = true,
                Private = false                
            };

            string ghtoken = "2c0d060c49fc52d582739a58dc990f56e171dad5";
            var ghClient = new GitHubClient(new Octokit.ProductHeaderValue("MigratedConsoleApp"));
            ghClient.Credentials = new Octokit.Credentials(ghtoken);

            var repositoryResponse = ghClient.Repository.Create(newRepo).Result;
            string cloneUrl = repositoryResponse.CloneUrl.ToString();
            

            CommitAllChanges("Commiting the migrated project",filePath, cloneUrl).Wait();
        }

        public static async Task CommitAllChanges(string message,string filePath, string cloneUrl)
        {
            try
            {
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
                            new UsernamePasswordCredentials { Username = "sormita@gmail.com", Password = "2c0d060c49fc52d582739a58dc990f56e171dad5" }
                    };

                    string pushRefSpec = @"refs/heads/master";

                    repo.Network.Push(remote, pushRefSpec, options);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            
            
        }
    }
}
