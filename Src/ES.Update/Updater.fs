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

    let downloadUpdates(clientPublicKey: String, iv: Byte array, resultFile: String) =
        try
            // configure request
            let webRequest = WebRequest.Create(new Uri(serverUri, "updates")) :?> HttpWebRequest
            webRequest.Method <- "POST"
            webRequest.Timeout <- 5 * 60 * 1000
            webRequest.ContentType <- "application/x-www-form-urlencoded"

            // write data
            use streamWriter = new StreamWriter(webRequest.GetRequestStream())
            let data = 
                String.Format(
                    "version={0}&key={1}&iv={2}&project={3}", 
                    currentVersion,
                    clientPublicKey, 
                    Convert.ToBase64String(iv),
                    projectName
                )
            streamWriter.Write(data)
            streamWriter.Close()

            // send the request and save the response to file
            use webResponse = webRequest.GetResponse() :?> HttpWebResponse            
            use responseStream = webResponse.GetResponseStream()            
            use fileHandle = File.OpenWrite(resultFile)
            responseStream.CopyTo(fileHandle)
            true
        with _ ->
            false

    member this.InstallUpdates(updateFile: String, sharedKey: Byte array, iv: Byte array) =
        // TODO
        // 
        ()

    member this.GetLatestVersion() =
        let path = String.Format("/latest?project={0}", projectName)

        // contact server
        use webClient = new WebClient()
        webClient.DownloadString(new Uri(serverUri, path)) |> Version.Parse
        
    member this.GetUpdates(version: Version) =
        // prepare update file
        let resultDirectory = Path.Combine(Path.GetTempPath(), projectName)
        Directory.CreateDirectory(resultDirectory) |> ignore
        let resultFile = Path.Combine(resultDirectory, version.ToString() + ".zip")

        // generate keys, download updates and install them
        let (sharedKey, iv, clientPublicKey) = generateEncryptionKey()
        if File.Exists(resultFile) || downloadUpdates(clientPublicKey, iv, resultFile) then
            this.InstallUpdates(resultFile, sharedKey, iv)
            File.Delete(resultFile)