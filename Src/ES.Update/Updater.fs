namespace ES.Update

open System
open System.Net
open System.Security.Cryptography
open System.IO
open System.IO.Compression
open System.Text
open System.Linq

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

    let readEntry(zipArchive: ZipArchive, name: String) =
        let entry =
            zipArchive.Entries
            |> Seq.find(fun entry -> entry.FullName.Equals(name, StringComparison.OrdinalIgnoreCase))
        
        use zipStream = entry.Open()
        use memStream = new MemoryStream()
        zipStream.CopyTo(memStream)
        memStream.ToArray()

    let checkIntegrity(zipArchive: ZipArchive, sharedKey: Byte array, iv: Byte array) =
        let signature = CryptoUtility.decrypt(readEntry(zipArchive, "signature"), sharedKey, iv)
        let computedSignature = sha256Raw(readEntry(zipArchive, "catalog"))
        Enumerable.SequenceEqual(signature, computedSignature)
            
    member this.InstallUpdates(updateFile: String, sharedKey: Byte array, iv: Byte array) =
        use zipStream = File.OpenRead(updateFile)
        use zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Read)
        let fileList = Encoding.UTF8.GetString(readEntry(zipArchive, "catalog"))
        if checkIntegrity(zipArchive, sharedKey, iv) then
            // check signature 
            // start copy files and check integrity after copy. If fails delete all copied file
            Ok ()
        else
            Error "Integrity check failed"

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
        if downloadUpdates(clientPublicKey, iv, resultFile) then
            let result = this.InstallUpdates(resultFile, sharedKey, iv)
            File.Delete(resultFile)
            result
        else
            Error "Unable to download updates"