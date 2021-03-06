﻿using Example2;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Example3
{
    class Program
    {
        private static AuthenticatedWebServer _server = null;

        private static void RunServer(String workspaceDirectory)
        {
            var wait = new ManualResetEventSlim();
            Task.Factory.StartNew(() =>
            {
                _server = new AuthenticatedWebServer(workspaceDirectory);
                wait.Set();
                _server.Start();
            });
            wait.Wait();
            Thread.Sleep(2000);
        }

        private static void RunClient(String destinationDirectory)
        {
            var client = new Client();
            client.Run(destinationDirectory);
        }

        static void Main(string[] args)
        {
            var (workspaceDirectory, destinationDirectory) = Helpers.CreateEnvironment(5);
            RunServer(workspaceDirectory);
            RunClient(destinationDirectory);
            _server.Stop();
        }
    }
}
