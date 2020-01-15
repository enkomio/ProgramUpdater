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

    let verifyCatalogContentIntegrity(catalog: Byte array, installerCatalog: (Byte array) option, signature: Byte array, installerSignature: (Byte array) option) =
        let catalogOk = CryptoUtility.verifySignature(catalog, signature, publicKey)
        let installerCatalogOk = 
            match (installerCatalog, installerSignature) with
            | (Some installerCatalog, Some installerSignature) ->
                CryptoUtility.verifySignature(installerCatalog, installerSignature, publicKey)
            | (Some _, None) -> false
            | (None, Some _) -> false
            | _ -> true
        catalogOk && installerCatalogOk  

    let verifyCatalogIntegrity(zipArchive: ZipArchive) =
        let catalog = Utility.readEntry(zipArchive, "catalog")
        let signature = Utility.readEntry(zipArchive, "signature")
        let installerCatalog = Utility.tryReadEntry(zipArchive, "installer-catalog")
        let installerSignature = Utility.tryReadEntry(zipArchive, "installer-signature")
        verifyCatalogContentIntegrity(catalog, installerCatalog, signature, installerSignature)  

    new (serverUri: Uri, projectName: String, currentVersion: Version, destinationDirectory: String, publicKey: Byte array) = new Updater(serverUri, projectName, currentVersion, destinationDirectory, publicKey, LogProvider.GetDefault())

    member val PatternsSkipOnExist = new List<String>() with get, set
    member val SkipIntegrityCheck = false with get, set
    member val RemoveTempFile = true with get, set

    member this.AddParameter(name: String, value: String) =
        let dataStorage =
            match _additionalData with
            | None -> 
                _additionalData <- new Dictionary<String, String>() |> Some
                _additionalData.Value
            | Some d -> d
        dataStorage.[name] <- value

    member internal this.InstallUpdatesFromFile(file: String, installer: Installer) =
        use zipStream = File.OpenRead(file)
        use zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Read)
        if this.SkipIntegrityCheck || verifyCatalogIntegrity(zipArchive) then
            let fileList = Utility.readEntry(zipArchive, "catalog")
            installer.InstallUpdate(zipArchive, Encoding.UTF8.GetString(fileList))
        else
            _logger?CatalogIntegrityFail()
            Error "Integrity check failed"

    member internal this.InstallUpdatesFromDirectory(directory: String, installer: Installer) =
        let fileList = Path.Combine(directory, "catalog") |> File.ReadAllText
        let mutable integrityCheckOk = true

        // check integrity
        if not this.SkipIntegrityCheck then            
            let catalog = Path.Combine(directory, "catalog") |> File.ReadAllBytes
            let signature = Path.Combine(directory, "signature") |> File.ReadAllBytes
            let installerCatalogFile =  Path.Combine(directory, "installer-catalog")
            let installerSignatureFile = Path.Combine(directory, "installer-signature")

            if File.Exists(installerCatalogFile) && File.Exists(installerSignatureFile) then
                let installerCatalog = File.ReadAllBytes(installerCatalogFile)
                let installerSignature = File.ReadAllBytes(installerSignatureFile)
                integrityCheckOk <- verifyCatalogContentIntegrity(catalog, Some installerCatalog, signature, Some installerSignature)
            elif not(File.Exists(installerCatalogFile)) && not(File.Exists(installerSignatureFile)) then
                integrityCheckOk <- verifyCatalogContentIntegrity(catalog, None, signature, None)
            else
                // do I have an installer and not associated signature or the opposite
                integrityCheckOk <- false

        if integrityCheckOk then
            installer.InstallUpdate(directory, fileList)
        else
            _logger?CatalogIntegrityFail()
            Error "Integrity check failed"
            
    member this.InstallUpdates(fileOrDirectory: String) =
        let installer = 
            new Installer(
                destinationDirectory, 
                logProvider, 
                PatternsSkipOnExist = this.PatternsSkipOnExist,
                SkipIntegrityCheck = this.SkipIntegrityCheck
            )

        if Directory.Exists(fileOrDirectory) then
            this.InstallUpdatesFromDirectory(fileOrDirectory, installer)
        elif File.Exists(fileOrDirectory) then
            this.InstallUpdatesFromFile(fileOrDirectory, installer)
        else
            Error(String.Format("{0} is not a valid update path", fileOrDirectory))

    member this.GetLatestVersion() =
        use webClient = new WebClient()        
        let latestVersionUri = new Uri(serverUri, String.Format("latest?project={0}", projectName))
        webClient.DownloadString(latestVersionUri) |> Version.Parse
        
    member this.Update(version: Version) =
        // prepare update file
        let resultDirectory = Path.Combine(Path.GetTempPath(), projectName)
        Directory.CreateDirectory(resultDirectory) |> ignore
        let resultFile = Path.Combine(resultDirectory, String.Format("update-{0}.zip", version))
        if File.Exists(resultFile) && this.RemoveTempFile then File.Delete(resultFile)

        // generate keys, download updates and install them
        if downloadUpdates(resultFile) then
            _logger?DownloadDone(resultFile)
            let result = 
                match this.InstallUpdates(resultFile) with
                | Ok _ -> new Result(true)
                | Error msg -> new Result(false, Error = msg)
            if this.RemoveTempFile then File.Delete(resultFile)
            result
        else
            new Result(false, Error = "Unable to download updates")