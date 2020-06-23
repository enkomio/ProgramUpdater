using System;
using System.IO;
using ES.Update;

namespace Example1
{
    public class Client
    {
        public void Run(Server server, String destinationDirectory)
        {
            var myVersion = Helpers.GetCurrentVersion();
            var updater = new Updater(server.BindingUri, "MyApplication", myVersion, destinationDirectory, server.PublicKey);

            var latestVersion = updater.GetLatestVersion();
            Console.WriteLine("My version: {0}. Latest version: {1}", myVersion, latestVersion);

            if (latestVersion > myVersion)
            {
                // start update
                updater.DownloadingFile += (args, file) => { Console.WriteLine("[-] Downloading: {0}", file); };
                updater.DownloadedFile += (args, file) => { Console.WriteLine("[+] Downloaded: {0}", file); };

                var updateResult = updater.Update();
                if (updateResult.Success)
                {                    
                    var fileContent = File.ReadAllText(Path.Combine(destinationDirectory, "folder", "file7.txt"));
                    Console.WriteLine("Update installed correctly! {0}", fileContent);
                    Helpers.SaveVersion(latestVersion);
                }
                else
                {
                    Console.WriteLine("Error during installing updates: {0}", updateResult.Error);
                }
            }
        }
    }
}
