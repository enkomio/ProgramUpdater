using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ES.Update;
using ES.Update.Backend;

namespace Example3
{
    public class AuthenticatedWebServer : WebServer
    {
        public static Byte[] PrivateKey = null;
        public static Byte[] PublicKey = null;
        public static Uri BindingUri = null;

        static AuthenticatedWebServer()
        {
            var (publicKey, privateKey) = CryptoUtility.GenerateKeys();
            PrivateKey = privateKey;
            PublicKey = publicKey;

            var rnd = new Random();
            BindingUri = new Uri(String.Format("http://127.0.0.1:{0}", rnd.Next(1025, 65534)));
        }

        public AuthenticatedWebServer(String workspaceDirectory) : 
            base(BindingUri, workspaceDirectory, PrivateKey)
        {

        }
    }
}
