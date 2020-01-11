namespace ES.Update.Releaser

open System
open System.Collections.Generic
open System.IO
open System.Text
open System.Text.RegularExpressions
open System.IO.Compression
open ES.Fslog
open ES.Update

type MetadataBuilder(workingDirectory: String, patternsToExclude: List<String>, logProvider: ILogProvider) =
    let _logger =
        log "MetadataBuilder"
        |> info "AnalyzeReleaseFile" "Analyze release file: {0}"
        |> info "SaveMetadata" "Saving release metadata"
        |> info "SavingFiles" "Saving artifacts to update"
        |> info "SavingFile" "Adding new file '{0}' as {1}"
        |> info "Completed" "Process completed"
        |> info "SkipZipEntry" "Skipped entry '{0}' due to forbidden pattern"
        |> buildAndAdd logProvider

    let extractProjectName(releaseFile: String) =        
        let m = Regex.Match(releaseFile |> Path.GetFileName, "(.+?)[0-9]+(\.[0-9]+)+")
        m.Groups.[1].Value.Trim('v').Trim('.')

    let extractVersion(releaseFile: String) =
        let m = Regex.Match(releaseFile |> Path.GetFileName, "[0-9]+(\.[0-9]+)+")
        m.Value |> Version.Parse

    let readZipEntryContent(name: String, entries: ZipArchiveEntry seq) =
        let zipEntry = 
            entries
            |> Seq.find(fun entry -> entry.FullName.Equals(name, StringComparison.OrdinalIgnoreCase))
        
        use zipStream = zipEntry.Open()
        use memStream = new MemoryStream()
        zipStream.CopyTo(memStream)
        memStream.ToArray()

    let getVersionFilesSummary(releaseFile: String) =
        use fileHandle = File.OpenRead(releaseFile)
        
        // inspect zip
        (new System.IO.Compression.ZipArchive(fileHandle, ZipArchiveMode.Read)).Entries
        |> Seq.toArray
        |> Array.filter(fun zipEntry ->
            let skipZipEntry =
                patternsToExclude
                |> Seq.exists(fun pattern -> Regex.IsMatch(zipEntry.FullName, pattern))                

            if skipZipEntry then _logger?SkipZipEntry(zipEntry.FullName)
            not skipZipEntry
        )
        |> Array.map(fun zipEntry ->            
            use zipStream = zipEntry.Open()
            use memStream = new MemoryStream()
            zipStream.CopyTo(memStream)
            (zipEntry.FullName, CryptoUtility.sha256(memStream.ToArray()))
        )

    let saveApplicationMetadata(workingDirectory: String, releaseFile: String, files: (String * String) seq) =
        let fileContent = new StringBuilder()        
        files |> Seq.iter(fun (name, hashValue) ->
            fileContent.AppendFormat("{0},{1}", hashValue, name).AppendLine() |> ignore
        )

        // save the content
        let versionsDirectory = Path.Combine(workingDirectory, "Versions")
        Directory.CreateDirectory(versionsDirectory) |> ignore
        let filename = Path.Combine(versionsDirectory, String.Format("{0}.txt", extractVersion(releaseFile)))
        File.WriteAllText(filename, fileContent.ToString())

    let getAllHashPerVersion(workingDirectory: String) =
        Directory.GetFiles(Path.Combine(workingDirectory, "Versions"))
        |> Array.map(fun filename ->
            (
                Path.GetFileNameWithoutExtension(filename),            
                (File.ReadAllLines(filename) |> Array.map(fun line -> line.Split([|','|]).[0]))
            )
        )

    let saveFilesContent(workingDirectory: String, releaseFile: String, files: (String * String) seq) =
        let releaseVersion = extractVersion(releaseFile).ToString()
        let fileBucketDir = Path.Combine(workingDirectory, "FileBucket", releaseVersion)
        Directory.CreateDirectory(fileBucketDir) |> ignore

        // compute the new files that must be copied
        let allHashFiles = 
            getAllHashPerVersion(workingDirectory) 
            |> Array.filter(fun (version, _) -> version.Equals(releaseVersion, StringComparison.OrdinalIgnoreCase) |> not)
            |> Array.collect(snd) 
            |> Set.ofArray

        let allVersionHash = files |> Seq.map(fun (_, h) -> h) |> Set.ofSeq
        let newHashFiles = Set.difference allVersionHash allHashFiles

        // open the zip file again
        use fileHandle = File.OpenRead(releaseFile)
        let entries = (new ZipArchive(fileHandle, ZipArchiveMode.Read)).Entries

        // save the new files
        files 
        |> Seq.filter(fun (_, hashValue) -> newHashFiles.Contains(hashValue) )
        |> Seq.iter(fun (name, hashValue) ->
            let filename = Path.Combine(fileBucketDir, hashValue)
            if not(File.Exists(filename)) then
                _logger?SavingFile(name, hashValue)
                File.WriteAllBytes(filename, readZipEntryContent(name, entries))
        )

    new(workingDirectory: String) = new MetadataBuilder(workingDirectory, new List<String>())
    new(workingDirectory: String, patternsToExclude: List<String>) = new MetadataBuilder(workingDirectory, patternsToExclude, new LogProvider())

    member this.CreateReleaseMetadata(releaseFile: String) =
        _logger?AnalyzeReleaseFile(Path.GetFileName(releaseFile))
        let files = getVersionFilesSummary(releaseFile)
        let projectWorkspace = Path.Combine(workingDirectory, extractProjectName(releaseFile))

        _logger?SaveMetadata()
        saveApplicationMetadata(projectWorkspace, releaseFile, files)
        
        _logger?SavingFiles()        
        saveFilesContent(projectWorkspace, releaseFile, files)
        
        _logger?Completed()
        ()
