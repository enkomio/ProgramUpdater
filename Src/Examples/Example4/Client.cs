using ES.Update;
using Example2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Example4
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
                    Console.WriteLine("Everything is fine the installer program is now running. After completation you should see in this directory a file named 'file8.txt' in directory 'folder'");
                }
                else
                {
                    Console.WriteLine("Error during installing updates: {0}", updateResult.Error);
                }
            }
        }
    }
}
