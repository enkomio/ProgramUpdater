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

        private static void RunClient()
        {
            var client = new Client();
            client.Run(_server);
        }
        
        static void Main(string[] args)
        {
            var workspaceDirectory = Helpers.CreateEnvironment();
            var wait = new ManualResetEventSlim();
            RunServer(workspaceDirectory, wait);
            wait.Wait();
            RunClient();
        }
    }
}
