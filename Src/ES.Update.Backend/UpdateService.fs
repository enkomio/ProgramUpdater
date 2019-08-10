namespace ES.Update.Backend

open System
open System.IO
open System.Collections.Generic

type UpdateService(workspaceDirectory: String, privateKey: Byte array) =
    let _lock = new Object()    
    let _updateManagers = new Dictionary<String, UpdateManager>()

    let getUpdateManager(projectName: String) =
        _updateManagers.[projectName]
        
    let getUpdateFileName(inputVersion: Version, updateManager: UpdateManager) =
        let correctInputVersion =
            match updateManager.GetApplication(inputVersion) with
            | Some application-> application.Version.ToString()
            | None -> Entities.DefaultVersion

        let latestVersion = updateManager.GetLatestVersion().Value.Version.ToString()
        String.Format("{0}-{1}.zip", correctInputVersion, latestVersion)

    do
        Directory.GetDirectories(workspaceDirectory)
        |> Array.map(fun directory -> Path.GetFileName(directory))
        |> Array.iter(fun projectName -> 
            let projectDirectory = Path.Combine(workspaceDirectory, projectName)
            Directory.CreateDirectory(projectDirectory) |> ignore
            _updateManagers.[projectName] <- new UpdateManager(projectDirectory)
        )

    /// This timeout is used to clean the temporary update files that are generated
    /// during the update process.
    member val CacheCleanupSecondsTimeout = 24 * 60 * 60 with get, set

    member this.GetAvailableVersions() =
        _updateManagers 
        |> Seq.toArray
        |> Array.collect(fun kv ->
            kv.Value.GetAvailableVersions() 
            |> Array.map(fun version -> (kv.Key, version))
        )

    member this.GetLatestVersion(projectName: String) =
        if _updateManagers.ContainsKey(projectName) then
            match getUpdateManager(projectName).GetLatestVersion() with
            | Some application -> Some <| application.Version.ToString()
            | None -> None
        else
            None

    member this.IsValidProject(projectName: String) =
        _updateManagers.ContainsKey(projectName)

    member this.GetUpdates(version: Version, projectName: String) =
        let updateManager = getUpdateManager(projectName)
            
        // compute zip filename
        let storageDirectory = Path.Combine(workspaceDirectory, "Binaries")
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
        )            

        // add signature to zip file
        removeOldBinaryFiles(this.CacheCleanupSecondsTimeout)
        addSignature(zipFile, privateKey)
