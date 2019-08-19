using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ES.Update;
using ES.Update.Backend;
using static Suave.Http;

namespace Example3
{
    public class AuthenticatedWebServer : WebServer
    {
        public static Byte[] PrivateKey = null;
        public static Byte[] PublicKey = null;
        public static Uri BindingUri = null;
        public static String Username = "admin";
        public static String Password = "password";

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
            // specify a prefix to use for the uri composition
            this.PathPrefix = "/myupdate";
        }

        public override Boolean Authenticate(HttpContext ctx)
        {
            if (ctx.request.method.IsPOST)
            {
                var formParameters = Encoding.UTF8.GetString(ctx.request.rawForm).Split('&');
                var username = String.Empty;
                var password = String.Empty;

                foreach(var parameter in formParameters)
                {
                    var nameValue = parameter.Split('=');
                    if (nameValue[0].Equals("Username", StringComparison.OrdinalIgnoreCase))
                    {
                        username = nameValue[1];
                    }
                    else if (nameValue[0].Equals("Password", StringComparison.OrdinalIgnoreCase))
                    {
                        password = nameValue[1];
                    }
                }

                return 
                    username.Equals(AuthenticatedWebServer.Username, StringComparison.Ordinal) 
                    && password.Equals(AuthenticatedWebServer.Password, StringComparison.Ordinal);
            }
            else
            {
                return true;
            }
        }
    }
}
