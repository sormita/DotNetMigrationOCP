using System;
using System.Configuration;

namespace DotNetMigrationTool
{
    class Program
    {
        static void Main(string[] args)
        {            
            // github variables
            var repo = ConfigurationManager.AppSettings["repo"];
            var owner = ConfigurationManager.AppSettings["owner"];
            var branch = ConfigurationManager.AppSettings["branch"];
            var applicationFolder = ConfigurationManager.AppSettings["applicationFolder"];
            var file_path = ConfigurationManager.AppSettings["filepath"];
            //ConsoleAppMigration.MigrateConsoleApp(repo, owner, file_path);    
            MVCCoreContainerize.ContainerApplication(repo, owner, file_path);
        }
    }
}
