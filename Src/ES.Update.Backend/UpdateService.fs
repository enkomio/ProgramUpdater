namespace ES.Update.Backend

open System
open System.Timers
open System.IO
open System.Collections.Concurrent
open Suave.Logging
open ES.Fslog

type UpdateService(workspaceDirectory: String, privateKey: Byte array, logProvider: ILogProvider) =
    let _lock = new Object() 
    let _timer = new Timer()
    let _updateManagers = new ConcurrentDictionary<String, UpdateManager>()

    let _logger =
        log "UpdateService"
        |> info "NoInstaller" "No installer path defined, use a standard copy"
        |> info "Installer" "Use installer from path: {0}"
        |> info "CreateZipFile" "Created zip file: {0}"
        |> info "ZipFileAlreadyPresent" "Zip file already present, use file from: {0}"
        |> buildAndAdd logProvider

    let getUpdateManager(projectName: String) =
        _updateManagers.[projectName]
        
    let getUpdateFileName(inputVersion: Version, updateManager: UpdateManager) =
        let correctInputVersion =
            match updateManager.GetApplication(inputVersion) with
            | Some application-> application.Version.ToString()
            | None -> Entities.DefaultVersion

        let latestVersion = updateManager.GetLatestVersion().Value.Version.ToString()
        String.Format("{0}-{1}.zip", correctInputVersion, latestVersion)

    let doUpdate() =
        _timer.Stop()
        lock _lock (fun _ ->
            Directory.GetDirectories(workspaceDirectory)
            |> Array.map(fun directory -> Path.GetFileName(directory))
            |> Array.iter(fun projectName -> 
                let projectDirectory = Path.Combine(workspaceDirectory, projectName)
                Directory.CreateDirectory(projectDirectory) |> ignore
                _updateManagers.[projectName] <- new UpdateManager(projectDirectory)
            )
        )        
        _timer.Start()

    do        
        _timer.Interval <- TimeSpan.FromMinutes(1.).TotalMilliseconds |> float
        _timer.Elapsed.Add(fun _ -> doUpdate())
        doUpdate()
        
    member this.GetAvailableVersions() =
        lock _lock (fun _ ->
            _updateManagers 
            |> Seq.toArray
            |> Array.collect(fun kv ->
                kv.Value.GetAvailableVersions() 
                |> Array.map(fun version -> (kv.Key, version))
            )
        )

    member this.GetLatestVersion(projectName: String) =
        lock _lock (fun _ ->
            if _updateManagers.ContainsKey(projectName) then
                match getUpdateManager(projectName).GetLatestVersion() with
                | Some application -> Some <| application.Version.ToString()
                | None -> None
            else
                None
        )

    member this.IsValidProject(projectName: String) =
        _updateManagers.ContainsKey(projectName)

    member this.GetUpdates(version: Version, projectName: String, installerPath: String) =
        let updateManager = getUpdateManager(projectName)
            
        // compute zip filename
        let storageDirectory = Path.Combine(workspaceDirectory, projectName, "Binaries")
        let zipFile = Path.Combine(storageDirectory, getUpdateFileName(version, updateManager))

        // check if we already compute this update, if not create it
        lock _lock (fun _ ->
            if not(File.Exists(zipFile)) then
                // compute updates
                let updateFiles = updateManager.GetUpdates(version)
                let integrityInfo = updateManager.ComputeIntegrityInfo(updateFiles |> List.map(fst))
                
                // create the zip file and store it in the appropriate directory            
                Directory.CreateDirectory(storageDirectory) |> ignore            
                createZipFile(zipFile, updateFiles, integrityInfo)
                _logger?CreateZipFile(zipFile)

                // add signature to zip file
                addSignature(zipFile, privateKey)

                // add installer if necessary
                if Directory.Exists(installerPath) then 
                    _logger?Installer(installerPath)
                    addInstaller(zipFile, installerPath, privateKey)
                else
                    _logger?NoInstaller()
            else
                _logger?ZipFileAlreadyPresent(zipFile)
        )            

        zipFile
