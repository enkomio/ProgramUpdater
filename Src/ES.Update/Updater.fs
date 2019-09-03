namespace ES.Update

open System
open System.Collections.Generic
open System.Net
open System.IO
open System.IO.Compression
open System.Text
open ES.Fslog
open ES.Fslog.Loggers

type Updater(serverUri: Uri, projectName: String, currentVersion: Version, destinationDirectory: String, publicKey: Byte array, logProvider: ILogProvider) =
    let mutable _additionalData: Dictionary<String, String> option = None
    
    let _logger =
        log "Updater"
        |> critical "CatalogIntegrityFail" "The integrity check of the catalog failed"
        |> critical "DownloadError" "Download error: {0}"
        |> info "DownloadDone" "Updates downloaded to file: {0}"
        |> buildAndAdd logProvider

    let downloadUpdates(resultFile: String) =
        try
            // configure request
            let webRequest = WebRequest.Create(new Uri(serverUri, "updates")) :?> HttpWebRequest
            webRequest.Method <- "POST"
            webRequest.Timeout <- 5 * 60 * 1000
            webRequest.ContentType <- "application/x-www-form-urlencoded"

            // compose data
            let data = new StringBuilder()
            data.AppendFormat("version={0}&project={1}", currentVersion, projectName) |> ignore

            _additionalData
            |> Option.iter(fun additionalData ->
                additionalData
                |> Seq.iter(fun kv ->
                    data.AppendFormat("&{0}={1}", kv.Key, kv.Value) |> ignore
                )
            )

            // write data
            use streamWriter = new StreamWriter(webRequest.GetRequestStream())
            streamWriter.Write(data.ToString().Trim('&'))
            streamWriter.Close()

            // send the request and save the response to file
            use webResponse = webRequest.GetResponse() :?> HttpWebResponse            
            use responseStream = webResponse.GetResponseStream()            
            use fileHandle = File.OpenWrite(resultFile)
            responseStream.CopyTo(fileHandle)
            true
        with e ->
            _logger?DownloadError(e.Message)
            false

    let verifyCatalogIntegrity(zipArchive: ZipArchive) =
        let catalog = Utility.readEntry(zipArchive, "catalog")
        let catalogOk = CryptoUtility.verifySignature(catalog, Utility.readEntry(zipArchive, "signature"), publicKey)

        let installerCatalogOk = 
            match Utility.tryReadEntry(zipArchive, "installer-catalog") with
            | Some installerCatalog -> 
                CryptoUtility.verifySignature(installerCatalog, Utility.readEntry(zipArchive, "installer-signature"), publicKey)
            | None -> 
                true

        catalogOk && installerCatalogOk

    new (serverUri: Uri, projectName: String, currentVersion: Version, destinationDirectory: String, publicKey: Byte array) = new Updater(serverUri, projectName, currentVersion, destinationDirectory, publicKey, LogProvider.GetDefault())

    member this.AddParameter(name: String, value: String) =
        let dataStorage =
            match _additionalData with
            | None -> 
                _additionalData <- new Dictionary<String, String>() |> Some
                _additionalData.Value
            | Some d -> d
        dataStorage.[name] <- value
            
    member this.InstallUpdates(updateFile: String) =
        use zipStream = File.OpenRead(updateFile)
        use zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Read)
        if verifyCatalogIntegrity(zipArchive) then
            let installer = new Installer(destinationDirectory, logProvider)
            let fileList = Utility.readEntry(zipArchive, "catalog")
            installer.InstallUpdate(zipArchive, Encoding.UTF8.GetString(fileList))
        else
            _logger?CatalogIntegrityFail()
            Error "Integrity check failed"

    member this.GetLatestVersion() =
        use webClient = new WebClient()        
        let latestVersionUri = new Uri(serverUri, String.Format("latest?project={0}", projectName))
        webClient.DownloadString(latestVersionUri) |> Version.Parse
        
    member this.Update(version: Version) =
        // prepare update file
        let resultDirectory = Path.Combine(Path.GetTempPath(), projectName)
        Directory.CreateDirectory(resultDirectory) |> ignore
        let resultFile = Path.Combine(resultDirectory, String.Format("update-{0}.zip", version))
        if File.Exists(resultFile) then File.Delete(resultFile)

        // generate keys, download updates and install them
        if downloadUpdates(resultFile) then
            _logger?DownloadDone(resultFile)
            let result = 
                match this.InstallUpdates(resultFile) with
                | Ok _ -> new Result(true)
                | Error msg -> new Result(false, Error = msg)
            File.Delete(resultFile)
            result
        else
            new Result(false, Error = "Unable to download updates")