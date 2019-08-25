using ES.Update;
using Example2;
using System;
using System.IO;

namespace Example3
{
    public class Client
    {
        public void Run(String destinationDirectory)
        {
            var myVersion = Helpers.GetCurrentVersion();
            var serverUri = new Uri(AuthenticatedWebServer.BindingUri, "myupdate/");
            var updater = new Updater(serverUri, "MyApplication", myVersion, destinationDirectory, AuthenticatedWebServer.PublicKey);

            // add username and password to the update request
            updater.AddParameter("username", AuthenticatedWebServer.Username);
            updater.AddParameter("password", AuthenticatedWebServer.Password);

            var latestVersion = updater.GetLatestVersion();
            Console.WriteLine("My version: {0}. Latest version: {1}", myVersion, latestVersion);

            if (latestVersion > myVersion)
            {
                // start update
                var updateResult = updater.Update(myVersion);
                if (updateResult.Success)
                {
                    var fileContent = File.ReadAllText(Path.Combine(destinationDirectory, "folder", "file8.txt"));
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
