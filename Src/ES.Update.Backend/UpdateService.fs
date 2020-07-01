namespace ES.Update.Backend

open System
open System.Timers
open System.IO
open System.Collections.Concurrent
open ES.Update
open System.Text

type UpdateService(workspaceDirectory: String, privateKey: Byte array) =
    let _lock = new Object() 
    let _timer = new Timer()
    let _updateManagers = new ConcurrentDictionary<String, UpdateManager>()
    let _syncRoot = new Object()
    let mutable _allFiles = Array.empty<String>

    let getUpdateManager(projectName: String) =
        _updateManagers.[projectName]

    let doUpdate() =
        _timer.Stop()
        lock _lock (fun _ ->
            // read all version
            Directory.GetDirectories(workspaceDirectory)
            |> Array.map(fun directory -> Path.GetFileName(directory))
            |> Array.iter(fun projectName -> 
                let projectDirectory = Path.Combine(workspaceDirectory, projectName)
                Directory.CreateDirectory(projectDirectory) |> ignore
                _updateManagers.[projectName] <- new UpdateManager(projectDirectory)
            )

            // read all available files
            let fileBucketDirectory = Path.Combine(workspaceDirectory, "FileBucket")
            let allFiles = Directory.GetFiles(fileBucketDirectory, "*", SearchOption.AllDirectories)
            lock _syncRoot (fun () -> _allFiles <- allFiles)
        )        
        _timer.Start()

    do        
        _timer.Interval <- TimeSpan.FromMinutes(1.).TotalMilliseconds |> float
        _timer.Elapsed.Add(fun _ -> doUpdate())
        doUpdate()

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

    member this.GetCatalog(version: Version, projectName: String) =
        let updateManager = getUpdateManager(projectName)
        match updateManager.GetLatestVersion() with
        | Some application when application.Version > version ->
            let catalog = updateManager.ComputeCatalog(application.Files)
            let signature = CryptoUtility.hexSign(Encoding.UTF8.GetBytes(catalog), privateKey)
            let result = String.Format("{0}\r\n{1}", signature, catalog)
            result
        | _ -> String.Empty       

    member this.GetAvailableVersions() =
        lock _lock (fun _ ->
            _updateManagers 
            |> Seq.toArray
            |> Array.collect(fun kv ->
                kv.Value.GetAvailableVersions() 
                |> Array.map(fun version -> (kv.Key, version))
            )
        )

    member this.GetFilePath(hash: String) =
        lock _syncRoot (fun () ->
            _allFiles
            |> Array.tryFind(fun file -> file.EndsWith(hash, StringComparison.OrdinalIgnoreCase))
        )