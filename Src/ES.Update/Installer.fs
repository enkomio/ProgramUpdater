﻿namespace ES.Update

open System
open System.IO
open System.IO.Compression
open System.Diagnostics

type Installer(destinationDirectory: String) =
    let compareHash(hashValue: String, content: Byte array) =
        let computedHashValue = CryptoUtility.sha256(content)
        hashValue.Equals(computedHashValue, StringComparison.OrdinalIgnoreCase)

    let copyFile(filePath: String, content: Byte array) =
        let destinationFile = Path.Combine(destinationDirectory, filePath)
        Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)) |> ignore
        File.WriteAllBytes(destinationFile, content)

    let getFiles(fileList: String) =
        fileList.Split()
        |> Array.filter(String.IsNullOrWhiteSpace >> not)
        |> Array.map(fun line -> 
            let items = line.Split([|','|])
            (items.[0], String.Join(",", items.[1..]))
        )

    let verifyIntegrity(extractedDirectory: String, files: (String * String) array) =
        let mutable integrityFailedOnFile = String.Empty
        
        files
        |> Array.forall(fun (hashValue, filePath) ->
            let content = File.ReadAllBytes(Path.Combine(extractedDirectory, hashValue))
            if compareHash(hashValue, content) then
                true
            else
                integrityFailedOnFile <- filePath
                false
        )
        |> fun result ->
            if result then Ok ()
            else Error("Integrity check failed on file: " + integrityFailedOnFile)

    let extractZip(zipArchive: ZipArchive) =
        let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(tempDir) |> ignore

        zipArchive.Entries
        |> Seq.iter(fun entry ->
            let fileName = Path.Combine(tempDir, entry.Name)
            Directory.CreateDirectory(Path.GetDirectoryName(fileName)) |> ignore
            let content = Utility.readEntry(zipArchive, entry.Name)
            File.WriteAllBytes(fileName, content)
        )

        tempDir

    let copyAllFiles(extractedDirectory: String, files: (String * String) array) =
        files
        |> Array.iter(fun (hashValue, filePath) ->
            let content = File.ReadAllBytes(Path.Combine(extractedDirectory, hashValue))
            copyFile(filePath, content)
        )   

    let runInstaller(installerProgram: String, extractedDirectory: String) =
        Process.Start(
            new ProcessStartInfo(
                FileName = installerProgram,
                UseShellExecute = false,
                Arguments = String.Format("--source \"{0}\" --dest \"{1}\"", extractedDirectory, destinationDirectory)
            )) |> ignore
        Environment.Exit(0)

    let install(extractedDirectory: String, files: (String * String) array) =        
        let installerProgram = Path.Combine(extractedDirectory, "Installer.exe")
        if File.Exists(installerProgram)
        then runInstaller(installerProgram, extractedDirectory)
        else copyAllFiles(extractedDirectory, files)

    member this.CopyUpdates(sourceDirectory: String) =
        let catalog = File.ReadAllText(Path.Combine(sourceDirectory, "catalog"))
        let files = getFiles(catalog)
        copyAllFiles(sourceDirectory, files)

    member this.InstallUpdate(zipArchive: ZipArchive, fileList: String) =
        let extractedDirectory = extractZip(zipArchive)
        let files = getFiles(fileList)
        let integrityCheckResult = verifyIntegrity(extractedDirectory, files)

        // install
        match integrityCheckResult with
        | Ok _ -> install(extractedDirectory, files)            
        | Error _ -> ()
        
        // cleanup
        Directory.Delete(extractedDirectory, true)

        // return
        integrityCheckResult