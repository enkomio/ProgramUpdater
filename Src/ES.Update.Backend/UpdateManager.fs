namespace ES.Update.Backend

open System
open System.IO
open System.Text
open ES.Update.Backend.Entities

type UpdateManager(workingDirectory: String, privateKey: String) =
    let mutable _applications : Application array = Array.empty

    let populateKnowledgeBase() =
        _applications <-
            Directory.GetFiles(Path.Combine(workingDirectory, "Versions"))
            |> Array.map(fun fileName ->
                {
                    Version = Version.Parse(Path.GetFileNameWithoutExtension(fileName))
                    Files =
                        File.ReadAllLines(fileName)
                        |> Array.map(fun line -> line.Trim().Split(','))
                        |> Array.map(fun items -> (items.[0], String.Join(",", items.[1..])))
                        |> Array.map(fun (sha1, path) -> {Sha1 = sha1; Path = path})
                }
            )

    do
        populateKnowledgeBase()

        // add directory watcher
        let watcher = new FileSystemWatcher(workingDirectory)
        watcher.Changed.Add(fun arg -> if arg.ChangeType = WatcherChangeTypes.Changed then populateKnowledgeBase())
        watcher.EnableRaisingEvents <- true

    let getApplicationHashes(application: Application) =
        application.Files |> Array.map(fun file -> file.Sha1)

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
            yield (allFiles |> Seq.find(fun file -> file.Sha1 = hash))
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
                |> Array.find(fun f -> Path.GetFileNameWithoutExtension(f).Equals(file.Sha1, StringComparison.OrdinalIgnoreCase))
                |> Path.GetDirectoryName
            let fileName = Path.Combine(fileBucketDirectory, version, file.Sha1)
            (file, File.ReadAllBytes(fileName))
        )

    member this.GetApplication(version: Version) =
        _applications |> Seq.tryFind(fun app -> app.Version = version)
    
    member this.GetLatestVersion() =
        _applications
        |> Seq.sortByDescending(fun application -> application.Version)
        |> Seq.tryHead

    member this.GetUpdates(version: Version) =
        match this.GetLatestVersion() with
        | Some application when application.Version > version ->
            // compute the new hashes to be added
            let updateHashes = computeUpdate(version, application)
            getFiles(updateHashes)
        | _ -> List.empty

    member this.ComputeIntegrityInfo(files: File list) =
        let fileContent = new StringBuilder()        
        files |> List.iter(fun file ->
            fileContent.AppendFormat("{0},{1}", file.Sha1, file.Path).AppendLine() |> ignore
        )
        fileContent.ToString()