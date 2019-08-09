using ES.Update.Releaser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Example2
{
    public static class Program
    {
        private static Server _server = null;

        private static void RunServer(String workspaceDirectory, ManualResetEventSlim wait)
        {
            Task.Factory.StartNew(() =>
            {
                using (_server = new Server(workspaceDirectory))
                {
                    wait.Set();
                    _server.Start();
                }
            });
        }

        private static void RunClient(String destinationDirectory)
        {
            var client = new Client();
            client.Run(_server, destinationDirectory);
        }
        
        static void Main(string[] args)
        {
            var (workspaceDirectory, destinationDirectory) = Helpers.CreateEnvironment();            
            var wait = new ManualResetEventSlim();
            RunServer(workspaceDirectory, wait);
            wait.Wait();
            RunClient(destinationDirectory);

        }
    }
}
