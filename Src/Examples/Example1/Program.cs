using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Example1
{
    public static class Program
    {
        private static Server _server = null;

        private static void RunServer(String workspaceDirectory)
        {
            _server = new Server(workspaceDirectory);
            
            Task.Factory.StartNew(() =>
            {
                _server.Start();
            });

            var req = (HttpWebRequest)WebRequest.Create(_server.BindingUri);
            HttpWebResponse resp = null;

            do
            {
                try
                {
                    resp = (HttpWebResponse)req.GetResponse();
                }
                catch { }
            } while (resp != null && resp.StatusCode != HttpStatusCode.OK);
            
        }

        private static void RunClient(String destinationDirectory)
        {
            var client = new Client();
            client.Run(_server, destinationDirectory);
        }
        
        static void Main(string[] args)
        {
            var (workspaceDirectory, destinationDirectory) = Helpers.CreateEnvironment(4);
            RunServer(workspaceDirectory);            
            RunClient(destinationDirectory);
            _server.Stop();
        }
    }
}
