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

    abstract GetAvailableVersions: unit -> Version array
    default this.GetAvailableVersions() =
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

    abstract ComputeCatalog: File array -> String
    default this.ComputeCatalog(files: File array) =
        let fileContent = new StringBuilder()        
        files 
        |> Array.iter(fun file ->
            fileContent.AppendFormat("{0},{1}\r\n", file.ContentHash, file.Path) |> ignore
        )
        fileContent.ToString()