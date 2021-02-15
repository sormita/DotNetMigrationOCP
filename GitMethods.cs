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
    public static class GitMethods
    {
        public static async Task<string> GetDockerFileAsync(string owner, string netcoreversion)
        {
            string ghtoken = "6bde8c2406d95d319199582c488fccff686cf7c2";
            var ghClient = new GitHubClient(new Octokit.ProductHeaderValue("IntermediateMigrationFiles"));
            ghClient.Credentials = new Octokit.Credentials(ghtoken);

            //Look for files with .csproj extension  
            var existingFile = ghClient.Repository.Content.GetAllContentsByRef(owner, "IntermediateMigrationFiles", "master").Result;
            string responseBody = null;

            for (int i = 0; i < existingFile.Count; i++)
            {
                if (existingFile[i].Name.Contains(netcoreversion))
                {
                    string appFolder = existingFile[i].Name;

                    using (HttpClient _httpClient = new HttpClient())
                    {
                        using (HttpResponseMessage response = _httpClient.GetAsync(existingFile[i].DownloadUrl).Result)
                        {
                            response.EnsureSuccessStatusCode();
                            responseBody = await response.Content.ReadAsStringAsync();
                            break;     
                        }
                    }                    
                }                

            }
            return responseBody;
        }

        public static async Task<string> GetDeploymentFileAsync(string owner)
        {
            string ghtoken = "6bde8c2406d95d319199582c488fccff686cf7c2";
            var ghClient = new GitHubClient(new Octokit.ProductHeaderValue("IntermediateMigrationFiles"));
            ghClient.Credentials = new Octokit.Credentials(ghtoken);

            //Look for files with .csproj extension  
            var existingFile = ghClient.Repository.Content.GetAllContentsByRef(owner, "IntermediateMigrationFiles", "master").Result;
            string responseBody = null;

            for (int i = 0; i < existingFile.Count; i++)
            {
                if (existingFile[i].Name.Contains("deployment.yaml"))
                {
                    string appFolder = existingFile[i].Name;

                    using (HttpClient _httpClient = new HttpClient())
                    {
                        using (HttpResponseMessage response = _httpClient.GetAsync(existingFile[i].DownloadUrl).Result)
                        {
                            response.EnsureSuccessStatusCode();
                            responseBody = await response.Content.ReadAsStringAsync();
                            break;
                        }
                    }
                }

            }
            return responseBody;
        }

        public static async Task<string> GetServiceFileAsync(string owner)
        {
            string ghtoken = "6bde8c2406d95d319199582c488fccff686cf7c2";
            var ghClient = new GitHubClient(new Octokit.ProductHeaderValue("IntermediateMigrationFiles"));
            ghClient.Credentials = new Octokit.Credentials(ghtoken);

            //Look for files with .csproj extension  
            var existingFile = ghClient.Repository.Content.GetAllContentsByRef(owner, "IntermediateMigrationFiles", "master").Result;
            string responseBody = null;

            for (int i = 0; i < existingFile.Count; i++)
            {
                if (existingFile[i].Name.Contains("service.yaml"))
                {
                    string appFolder = existingFile[i].Name;

                    using (HttpClient _httpClient = new HttpClient())
                    {
                        using (HttpResponseMessage response = _httpClient.GetAsync(existingFile[i].DownloadUrl).Result)
                        {
                            response.EnsureSuccessStatusCode();
                            responseBody = await response.Content.ReadAsStringAsync();
                            break;
                        }
                    }
                }

            }
            return responseBody;
        }





        public static async Task<string> CloneRepository(string repo, string owner, string file_path)
    {
        try
        {
            Console.WriteLine("Cloning the repository to a local volume");
            string ghtoken = "7f94b2db23f31cd2bbf10bf57fe0771a2ca48cf5";
            var ghClient = new GitHubClient(new Octokit.ProductHeaderValue("dotnetconsoleapp-migrate"));
            ghClient.Credentials = new Octokit.Credentials(ghtoken);

            var archiveBytes = ghClient.Repository.Content.GetArchive(owner, repo, ArchiveFormat.Zipball).Result;

            string path = file_path + repo + ".zip";
            string extractPath = file_path + repo;
            File.WriteAllBytes(path, archiveBytes);

            Console.WriteLine("Extracting the files to a local volume");
            //Extract the files
            System.IO.Compression.ZipFile.ExtractToDirectory(path, extractPath);
            return extractPath;
        }
        catch (Exception ex)
        {
            throw ex;
        }

    }

    public static async Task CommitFileToRepository(string owner, string filePath,string repoName)
    {
        try
        {
            Console.WriteLine("Creating the new repository in GitHub.");
            var newRepo = new NewRepository(repoName)
            {
                AutoInit = true,
                HasIssues = false,
                HasWiki = true,
                Private = false
            };

            string ghtoken = "7f94b2db23f31cd2bbf10bf57fe0771a2ca48cf5";
            var ghClient = new GitHubClient(new Octokit.ProductHeaderValue(repoName));
            ghClient.Credentials = new Octokit.Credentials(ghtoken);

            var repositoryResponse = ghClient.Repository.Create(newRepo).Result;
            string cloneUrl = repositoryResponse.CloneUrl.ToString();


            CommitAllChanges("Commiting the migrated project", filePath, cloneUrl).Wait();
        }
        catch (Exception ex)
        {
            throw;
        }

    }

    public static async Task CommitAllChanges(string message, string filePath, string cloneUrl)
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
                        new UsernamePasswordCredentials { Username = "sormita@gmail.com", Password = "e5310e7e7b37b57bd66fb5ed6d033c9aa2f0c255" }
                };

                var fetchOptions = new FetchOptions
                {
                    CredentialsProvider = (_url, _user, _cred) =>
                        new UsernamePasswordCredentials { Username = "sormita@gmail.com", Password = "e5310e7e7b37b57bd66fb5ed6d033c9aa2f0c255" }
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
