using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Example2;

namespace Example4
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

                // set the installer path
                var installerPath = Path.GetDirectoryName(typeof(Installer.Program).Assembly.Location);
                _server.WebServer.InstallerPath = installerPath;

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
            var (workspaceDirectory, _) = Helpers.CreateEnvironment(6);
            RunServer(workspaceDirectory);
            RunClient(Directory.GetCurrentDirectory());
            _server.Stop();
        }
    }
}
