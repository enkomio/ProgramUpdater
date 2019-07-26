namespace VersionReleaser

open System
open System.IO
open System.Text
open System.Text.RegularExpressions
open ES.Fslog
open ES.Update
open ES.Update.Entities
open ES.Update.Backend

module MetadataBuilder =
    let private extractVersion(releaseFile: String) =
        let m = Regex.Match(releaseFile |> Path.GetFileName, "[0-9]+(\.[0-9]+)+")
        m.Value |> Version.Parse

    let private getVersionFilesSummary(releaseFile: String) =
        iterZip(releaseFile) 
        |> Seq.map(fun (name, content) -> (name, content, sha1(content)))
        |> Seq.toArray

    let private saveApplication(workingDirectory: String, releaseFile: String, files: (String * Byte array * String) array) =
        let fileContent = new StringBuilder()        
        files |> Array.iter(fun (name, _, sha1) ->
            fileContent.AppendFormat("{0},{1}", sha1, name).AppendLine() |> ignore
        )

        // save the content
        let versionsDirectory = Path.Combine(workingDirectory, "Versions")
        Directory.CreateDirectory(versionsDirectory) |> ignore
        let filename = Path.Combine(versionsDirectory, String.Format("{0}.txt", extractVersion(releaseFile)))
        File.WriteAllText(filename, fileContent.ToString())

    let private saveFilesContent(workingDirectory: String, files: (String * Byte array * String) array) =
        let fileBucketDir = Path.Combine(workingDirectory, "FileBucket")
        Directory.CreateDirectory(fileBucketDir) |> ignore

        // compute the new files that must be copied
        let allSha1Files = Directory.GetFiles(fileBucketDir) |> Array.map(Path.GetFileNameWithoutExtension) |> Set.ofArray
        let allVersionSha1 = files |> Array.map(fun (_, _, sha1) -> sha1) |> Set.ofArray
        let newSha1Files = Set.difference allVersionSha1 allSha1Files

        // save the new files
        files 
        |> Array.filter(fun (_, _, sha1) -> newSha1Files.Contains(sha1) )
        |> Array.iter(fun (_, content, sha1) ->
            let filename = Path.Combine(fileBucketDir, sha1)
            File.WriteAllBytes(filename, content)
        )

    let createReleaseMetadata(workingDirectory: String, releaseFile: String, logProvider: ILogProvider) =
        let files = getVersionFilesSummary(releaseFile)
        saveApplication(workingDirectory, releaseFile, files)
        saveFilesContent(workingDirectory, files)