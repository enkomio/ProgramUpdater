namespace VersionReleaser

open System
open System.IO
open System.Text
open System.Text.RegularExpressions
open System.IO.Compression
open ES.Fslog
open ES.Update
open ES.Update.Backend

module MetadataBuilder =
    let private _logger =
        log "MetadataBuilder"
        |> info "AnalyzeReleaseFile" "Analyze release file: {0}"
        |> info "SaveMetadata" "Saving release metadata"
        |> info "SavingFiles" "Saving artifacts to update"
        |> info "SavingFile" "Adding new file '{0}' as {1}"
        |> info "Completed" "Process completed"
        |> build

    let private extractVersion(releaseFile: String) =
        let m = Regex.Match(releaseFile |> Path.GetFileName, "[0-9]+(\.[0-9]+)+")
        m.Value |> Version.Parse

    let private readZipEntryContent(name: String, entries: ZipArchiveEntry seq) =
        let zipEntry = 
            entries
            |> Seq.find(fun entry -> entry.FullName.Equals(name, StringComparison.OrdinalIgnoreCase))
        
        use zipStream = zipEntry.Open()
        use memStream = new MemoryStream()
        zipStream.CopyTo(memStream)
        memStream.ToArray()

    let private getVersionFilesSummary(releaseFile: String) =
        let patternsToExclude = VersionReleaser.Settings.Read().PatternToExclude
        use fileHandle = File.OpenRead(releaseFile)
        
        // inspect zip
        (new System.IO.Compression.ZipArchive(fileHandle, ZipArchiveMode.Read)).Entries
        |> Seq.toArray
        |> Array.filter(fun zipEntry ->
            patternsToExclude
            |> Seq.exists(fun pattern -> Regex.IsMatch(zipEntry.FullName, pattern))
            |> not
        )
        |> Array.map(fun zipEntry ->
            use zipStream = zipEntry.Open()
            use memStream = new MemoryStream()
            zipStream.CopyTo(memStream)
            (zipEntry.FullName, sha1(memStream.ToArray()))
        )

    let private saveApplicationMetadata(workingDirectory: String, releaseFile: String, files: (String * String) seq) =
        let fileContent = new StringBuilder()        
        files |> Seq.iter(fun (name, sha1) ->
            fileContent.AppendFormat("{0},{1}", sha1, name).AppendLine() |> ignore
        )

        // save the content
        let versionsDirectory = Path.Combine(workingDirectory, "Versions")
        Directory.CreateDirectory(versionsDirectory) |> ignore
        let filename = Path.Combine(versionsDirectory, String.Format("{0}.txt", extractVersion(releaseFile)))
        File.WriteAllText(filename, fileContent.ToString())

    let private saveFilesContent(workingDirectory: String, releaseFile: String, files: (String * String) seq) =
        let releaseVersion = extractVersion(releaseFile).ToString()
        let fileBucketDir = Path.Combine(workingDirectory, "FileBucket", releaseVersion)
        Directory.CreateDirectory(fileBucketDir) |> ignore

        // compute the new files that must be copied
        let allSha1Files = 
            getAllHashPerVersion(workingDirectory) 
            |> Array.filter(fun (version, _) -> version.Equals(releaseVersion, StringComparison.OrdinalIgnoreCase) |> not)
            |> Array.collect(snd) 
            |> Set.ofArray

        let allVersionSha1 = files |> Seq.map(fun (_, sha1) -> sha1) |> Set.ofSeq
        let newSha1Files = Set.difference allVersionSha1 allSha1Files

        // open the zip file again
        use fileHandle = File.OpenRead(releaseFile)
        let entries = (new System.IO.Compression.ZipArchive(fileHandle, ZipArchiveMode.Read)).Entries

        // save the new files
        files 
        |> Seq.filter(fun (_, sha1) -> newSha1Files.Contains(sha1) )
        |> Seq.iter(fun (name, sha1) ->
            let filename = Path.Combine(fileBucketDir, sha1)
            _logger?SavingFile(name, sha1)
            File.WriteAllBytes(filename, readZipEntryContent(name, entries))
        )

    let createReleaseMetadata(workingDirectory: String, releaseFile: String, logProvider: ILogProvider) =
        logProvider.AddLogSourceToLoggers(_logger)
        _logger?AnalyzeReleaseFile(Path.GetFileName(releaseFile))
        let files = getVersionFilesSummary(releaseFile)
        _logger?SaveMetadata()
        saveApplicationMetadata(workingDirectory, releaseFile, files)
        _logger?SavingFiles()
        saveFilesContent(workingDirectory, releaseFile, files)
        _logger?Completed()