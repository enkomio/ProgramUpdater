namespace ES.Update

open System
open System.Net
open System.Security.Cryptography
open System.IO

type Updater(serverUri: Uri, projectName: String, currentVersion: Version, serverPublicKey: String) =
    let generateEncryptionKey() =
        use client =
            new ECDiffieHellmanCng(
                KeyDerivationFunction = ECDiffieHellmanKeyDerivationFunction.Hash,
                HashAlgorithm = CngAlgorithm.Sha256
            )            
        
        // generate keys
        let sharedKey = client.DeriveKeyMaterial(CngKey.Import(Convert.FromBase64String(serverPublicKey), CngKeyBlobFormat.EccPublicBlob))
        use aes = new AesManaged(Key = sharedKey)
        let clientPublicKey = Convert.ToBase64String(client.PublicKey.ToByteArray())

        (sharedKey, aes.IV, clientPublicKey)

    let downloadUpdates(clientPublicKey: String, iv: Byte array) =
        try
            // configure request
            let webRequest = WebRequest.Create(new Uri(serverUri, "updates")) :?> HttpWebRequest
            webRequest.Method <- "POST"
            webRequest.Timeout <- 5 * 60 * 1000
            webRequest.ContentType <- "application/x-www-form-urlencoded"

            // write data
            use streamWriter = new StreamWriter(webRequest.GetRequestStream())
            let data = String.Format("version=1.2&key={0}&iv={1}&project=TaipanPro", clientPublicKey, Convert.ToBase64String(iv))
            streamWriter.Write(data)
            streamWriter.Close()

            // send the request and save the response to file
            use webResponse = webRequest.GetResponse() :?> HttpWebResponse
            use responseStream = webResponse.GetResponseStream()
            let resultFile = Path.Combine(Path.GetTempPath(), Path.GetTempFileName())
            use fileHandle = File.OpenWrite(resultFile)
            responseStream.CopyTo(fileHandle)
            Some resultFile
        with _ -> 
            None

    member this.InstallUpdates(updateFile: String, sharedKey: Byte array, iv: Byte array) =
        // TODO
        ()

    member this.GetLatestVersion() =
        let path = String.Format("/latest?project={0}", projectName)

        // contact server
        use webClient = new WebClient()
        webClient.DownloadString(new Uri(serverUri, path)) |> Version.Parse

    member this.CheckForUpdates() =
        this.GetLatestVersion() > currentVersion

    member this.GetUpdates() =
        let (sharedKey, iv, clientPublicKey) = generateEncryptionKey()
        match downloadUpdates(clientPublicKey, iv) with
        | Some updateFile -> this.InstallUpdates(updateFile, sharedKey, iv)
        | None -> ()