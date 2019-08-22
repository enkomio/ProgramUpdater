namespace UnitTests

open System
open System.Reflection
open System.Text
open ES.Update

module BackendTests =
    let private encryptExportedKey(password: String, keyData: String) =
        let utility = typeof<UpdateServer.Settings>.Assembly.ManifestModule.GetType("UpdateServer.Utility")
        let m = utility.GetMethod("encryptExportedKey", BindingFlags.Static ||| BindingFlags.NonPublic)
        m.Invoke(null, [|password; keyData|]) :?> String

    let private decryptImportedKey(password: String, keyData: String) =
        let utility = typeof<UpdateServer.Settings>.Assembly.ManifestModule.GetType("UpdateServer.Utility")
        let m = utility.GetMethod("decryptImportedKey", BindingFlags.Static ||| BindingFlags.NonPublic)
        m.Invoke(null, [|password; keyData|]) :?> String
        
    let private ``Encrypt and Decrypt exported key``() =
        let password = "testPassword"
        let privateKey = 
            "This private key value will be used to test the export and import feature"
            |> Encoding.UTF8.GetBytes 
            |> Convert.ToBase64String
        
        // export key
        let exportedKey = encryptExportedKey(password, privateKey)

        // import key
        let importedKey = decryptImportedKey(password, exportedKey)
        
        // verify
        let testResult = importedKey.Equals(privateKey, StringComparison.Ordinal)
        assert testResult
        
    let runAll() =
        ``Encrypt and Decrypt exported key``()
