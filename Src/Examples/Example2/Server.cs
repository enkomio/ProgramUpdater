using ES.Update;
using ES.Update.Backend;
using System;

namespace Example2
{
    public class Server
    {
        private WebServer _server = null;

        public Uri BindingUri { get; set; }
        public Byte[] PublicKey { get; set; }
        public String WorkspaceDirectory { get; set; }

        public Server(String workspaceDirectory)
        {
            this.WorkspaceDirectory = workspaceDirectory;
            var (publicKey, privateKey) = CryptoUtility.GenerateKeys();
            this.PublicKey = publicKey;

            var rnd = new Random();
            this.BindingUri = new Uri(String.Format("http://127.0.0.1:{0}", rnd.Next(1025, 65534)));
            _server = new WebServer(this.BindingUri, this.WorkspaceDirectory, privateKey);
        }

        public void Start()
        {
            _server.Start();
        }

        public void Stop()
        {
            _server.Stop();
        }
    }
}
