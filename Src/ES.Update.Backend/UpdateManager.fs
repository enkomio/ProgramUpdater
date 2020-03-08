namespace ES.Update.Backend

open System
open System.Timers
open System.IO
open System.Text
open ES.Update.Backend.Entities

type UpdateManager(workingDirectory: String) =
    let _lock = new Object() 
    let _timer = new Timer()
    let mutable _applications : Application array = Array.empty
    
    let populateKnowledgeBase() =
        _timer.Stop()
        lock _lock (fun () ->
            let versionDir = Path.Combine(workingDirectory, "Versions")
            if Directory.Exists(versionDir) then
                _applications <-            
                    Directory.GetFiles(versionDir)
                    |> Array.map(fun fileName ->
                        {
                            Version = Version.Parse(Path.GetFileNameWithoutExtension(fileName))
                            Files =
                                File.ReadAllLines(fileName)
                                |> Array.map(fun line -> line.Trim().Split(','))
                                |> Array.map(fun items -> (items.[0], String.Join(",", items.[1..])))
                                |> Array.map(fun (hashValue, path) -> {ContentHash = hashValue; Path = path})
                        }
                    )
        )
        _timer.Start()

    do
        if Directory.Exists(workingDirectory) then
            populateKnowledgeBase()

            // add directory watcher
            _timer.Interval <- TimeSpan.FromMinutes(1.).TotalMilliseconds |> float
            _timer.Elapsed.Add(fun _ -> populateKnowledgeBase())

    let getApplicationHashes(application: Application) =
        application.Files |> Array.map(fun file -> file.ContentHash)

    let getVersionHashes(version: Version) =
        match _applications |> Seq.tryFind(fun app -> app.Version = version) with
        | Some application -> getApplicationHashes(application)
        | None -> Array.empty

    let computeUpdate(oldVersion: Version, latestApplication: Application) =
        let oldHashes = getVersionHashes(oldVersion) |> Set.ofArray
        let newHashes = getApplicationHashes(latestApplication) |> Set.ofArray
        Set.difference newHashes oldHashes

    let mapHashToFile(hashes: String seq) = [
        let allFiles = _applications |> Seq.collect(fun app -> app.Files)
        for hash in hashes do
            yield (allFiles |> Seq.find(fun file -> file.ContentHash = hash))
    ]

    let getFiles(hashes: String seq) =
        let fileBucketDirectory = Path.Combine(workingDirectory, "FileBucket")        
        let allFiles = Directory.GetFiles(fileBucketDirectory, "*", SearchOption.AllDirectories)
        let updateFiles = mapHashToFile(hashes)
        
        // calculate the files to add
        updateFiles 
        |> List.map(fun file ->
            let version =
                allFiles 
                |> Array.find(fun f -> Path.GetFileNameWithoutExtension(f).Equals(file.ContentHash, StringComparison.OrdinalIgnoreCase))
                |> Path.GetDirectoryName
                |> Path.GetFileName
            let fileName = Path.Combine(fileBucketDirectory, version, file.ContentHash)
            (file, File.ReadAllBytes(fileName))
        )

    member this.GetAvailableVersions() =
        _applications 
        |> Seq.toArray
        |> Array.map(fun application -> application.Version)

    abstract GetApplication: Version -> Application option
    default this.GetApplication(version: Version) =
        _applications |> Seq.tryFind(fun app -> app.Version = version)
    
    abstract GetLatestVersion: unit -> Application option
    default this.GetLatestVersion() =
        _applications
        |> Seq.sortByDescending(fun application -> application.Version)
        |> Seq.tryHead

    abstract GetUpdates: Version -> (Application * (File * Byte array) list) option
    default this.GetUpdates(version: Version) =
        match this.GetLatestVersion() with
        | Some application when application.Version > version ->
            // compute the new hashes to be added
            let updateHashes = computeUpdate(version, application)
            Some (application, getFiles(updateHashes))
        | _ -> None

    abstract ComputeIntegrityInfo: File array -> String
    default this.ComputeIntegrityInfo(files: File array) =
        let fileContent = new StringBuilder()        
        files 
        |> Array.iter(fun file ->
            fileContent.AppendFormat("{0},{1}", file.ContentHash, file.Path).AppendLine() |> ignore
        )
        fileContent.ToString()