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
        public void Run(Server server)
        {
            var myVersion = new Version(3, 0);
            var updater = new Updater(server.BindingUri, "MyApplication", myVersion, server.PublicKey);
            var latestVersion = updater.GetLatestVersion();
            Console.WriteLine("My version: {0}. Latest version: {1}", myVersion, latestVersion);
            if (latestVersion > myVersion)
            {
                // start update
                var updateResult = updater.Update(myVersion);
                if (updateResult.Success)
                {                    
                    var currentDir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                    var fileContent = File.ReadAllText(Path.Combine(currentDir, "file9.txt"));
                    Console.WriteLine("Update installed correctly! Content: {0}", fileContent);
                }
                else
                {
                    Console.WriteLine("Error during installing updates: {0}", updateResult.Error);
                }
            }
        }
    }
}
