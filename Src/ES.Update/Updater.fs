namespace ES.Update

open System
open System.Collections.Generic
open System.Net
open System.IO
open System.IO.Compression
open System.Text
open ES.Fslog

type Updater(serverUri: Uri, projectName: String, currentVersion: Version, destinationDirectory: String, publicKey: Byte array, logProvider: ILogProvider) =
    let _downloadingFileEvent = new Event<String>()
    let _downloadedFileEvent = new Event<String>()
    let mutable _additionalData: Dictionary<String, String> option = None

    let _logger =
        log "Updater"
        |> critical "CatalogIntegrityFail" "The integrity check of the catalog failed"
        |> critical "DownloadError" "Download error: {0}"
        |> info "DownloadDone" "Updates downloaded to file: {0}"
        |> buildAndAdd logProvider

    let getContent(url: String) =
        try
            // configure request
            let webRequest = WebRequest.Create(new Uri(serverUri, url)) :?> HttpWebRequest
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
            use memoryStream = new MemoryStream()
            responseStream.CopyTo(memoryStream)
            memoryStream.ToArray()
        with e ->
            _logger?DownloadError(e.Message)
            Array.empty<Byte>

    let getString(url: String) =
        Encoding.UTF8.GetString(getContent(url))

    let parseCatalog(catalog: String) =
        catalog.Split("\r\n")
        |> Array.filter(fun line -> String.IsNullOrWhiteSpace(line) |> not)
        |> Array.map(fun line -> 
            // hash, file path
            let items = line.Split(",")   
            (items.[0], items.[1])
        )

    let tryGetCatalog() =
        let catalog = getString("updates")
        let items = catalog.Split("\r\n")
        if items.Length >= 2 then
            let signature = items.[0].Trim()
            let catalog = String.Join("\r\n", items.[1..])
            if CryptoUtility.verifyString(catalog, signature, publicKey) then
                (String.Empty, Some(parseCatalog(catalog)))
            else
                ("Wrong catalog signature", None)
        else
            ("Wrong catalog format", None)

    let isFileAlreadyDownloaded(hash: String, filePath: String) = 
        if File.Exists(filePath) then
            let receivedHash = CryptoUtility.sha256(File.ReadAllBytes(filePath))
            receivedHash.Equals(hash, StringComparison.OrdinalIgnoreCase)
        else
            false

    let downloadFile(hash: String, filePath: String) =
        // check if the file was already downloaded
        let storagePath = Path.Combine(destinationDirectory, filePath)
        if isFileAlreadyDownloaded(hash, storagePath) then
            new Result(true)
        else
            _downloadingFileEvent.Trigger(hash)
            let fileContent = getContent(String.Format("file/{0}", hash))
            if fileContent |> Array.isEmpty then
                new Result(false, Error = String.Format("Error downloading file: {0}", filePath))
            else
                let receivedHash = CryptoUtility.sha256(fileContent)
                if receivedHash.Equals(hash, StringComparison.OrdinalIgnoreCase) then
                    Directory.CreateDirectory(Path.GetDirectoryName(storagePath)) |> ignore
                    File.WriteAllBytes(storagePath, fileContent)
                    _downloadedFileEvent.Trigger(hash)
                    new Result(true)
                else
                    new Result(false, Error = String.Format("Downloaded file '{0}' has a wrong hash", filePath))

    let verifyCatalogContentIntegrity(catalog: Byte array, installerCatalog: (Byte array) option, signature: Byte array, installerSignature: (Byte array) option) =
        let catalogOk = CryptoUtility.verifyData(catalog, signature, publicKey)
        let installerCatalogOk = 
            match (installerCatalog, installerSignature) with
            | (Some installerCatalog, Some installerSignature) ->
                CryptoUtility.verifyData(installerCatalog, installerSignature, publicKey)
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

    let downloadFiles(files: (String * String) array) =
        files
        |> Array.map(downloadFile)
        |> Array.tryFind(fun res -> not res.Success)

    new (serverUri: Uri, projectName: String, currentVersion: Version, destinationDirectory: String, publicKey: Byte array) = new Updater(serverUri, projectName, currentVersion, destinationDirectory, publicKey, LogProvider.GetDefault())

    member val PatternsSkipOnExist = new List<String>() with get, set
    member val SkipIntegrityCheck = false with get, set
    member val RemoveTempFile = true with get, set

    [<CLIEvent>]
    member val DownloadingFile = _downloadingFileEvent.Publish

    [<CLIEvent>]
    member val DownloadedFile = _downloadedFileEvent.Publish

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
        
    member this.Update() =
        // prepare update file
        let resultDirectory = Path.Combine(Path.GetTempPath(), projectName)
        Directory.CreateDirectory(resultDirectory) |> ignore
        
        // download catalog
        match tryGetCatalog() with
        | (_, Some files) -> 
            match downloadFiles(files) with
            | Some error -> error
            | None -> new Result(true)
        | (error, _) ->
            new Result(false, Error = error)