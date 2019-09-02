using ES.Fslog;
using ES.Fslog.Loggers;
using ES.Fslog.TextFormatters;
using ES.Update;
using ES.Update.Backend;
using System;
using System.IO;
using System.Reflection;

namespace Example2
{
    public class Server
    {
        public Uri BindingUri { get; set; }
        public Byte[] PublicKey { get; set; }
        public String WorkspaceDirectory { get; set; }
        
        public Server(String workspaceDirectory)
        {
            var logProvider = new LogProvider();
            logProvider.AddLogger(new ConsoleLogger(LogLevel.Verbose, new ConsoleLogFormatter()));

            this.WorkspaceDirectory = workspaceDirectory;
            var (publicKey, privateKey) = CryptoUtility.GenerateKeys();
            this.PublicKey = publicKey;

            var rnd = new Random();
            this.BindingUri = new Uri(String.Format("http://127.0.0.1:{0}", rnd.Next(1025, 65534)));
            this.WebServer = new WebServer(this.BindingUri, this.WorkspaceDirectory, privateKey, logProvider);
        }

        public WebServer WebServer { get; set; }

        public void Start()
        {
            this.WebServer.Start();
        }

        public void Stop()
        {
            this.WebServer.Stop();
        }
    }
}
