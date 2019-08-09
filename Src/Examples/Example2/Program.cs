using System;
using System.Threading;
using System.Threading.Tasks;

namespace Example2
{
    public static class Program
    {
        private static Server _server = null;

        private static void RunServer(String workspaceDirectory)
        {
            var wait = new ManualResetEventSlim();
            Task.Factory.StartNew(() =>
            {
                _server = new Server(workspaceDirectory);
                wait.Set();
                _server.Start();
            });
            wait.Wait();
            Thread.Sleep(2000);
        }

        private static void RunClient(String destinationDirectory)
        {
            var client = new Client();
            client.Run(_server, destinationDirectory);
        }
        
        static void Main(string[] args)
        {
            var (workspaceDirectory, destinationDirectory) = Helpers.CreateEnvironment();
            RunServer(workspaceDirectory);            
            RunClient(destinationDirectory);
            _server.Stop();
        }
    }
}
