using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ES.Update;

namespace Example2
{
    public class Client
    {
        public void Run(Server server, String destinationDirectory)
        {
            var myVersion = new Version(3, 0);            
            var updater = new Updater(server.BindingUri, "MyApplication", myVersion, destinationDirectory, server.PublicKey);
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
                }
                else
                {
                    Console.WriteLine("Error during installing updates: {0}", updateResult.Error);
                }
            }
        }
    }
}
