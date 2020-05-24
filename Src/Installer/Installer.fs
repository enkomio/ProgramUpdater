namespace Installer

open System
open System.IO
open System.IO.Compression
open System.Diagnostics
open System.Text
open System.Threading
open System.Collections.Generic
open System.Text.RegularExpressions

module AbbandonedMutex =
    let mutable mutex: Mutex option = None
    
type Installer(destinationDirectory: String) as this =    
    
    let compareHash(hashValue: String, content: Byte array) =        
        let computedHashValue = sha256(content)
        hashValue.Equals(computedHashValue, StringComparison.OrdinalIgnoreCase)

    let moveFile(destinationFile: String) =
        let oldFilesDir = Path.Combine(destinationDirectory, "OLD-files")
        Directory.CreateDirectory(oldFilesDir) |> ignore
        let copyFile = Path.Combine(oldFilesDir, Path.GetFileName(destinationFile))
        if not <| File.Exists(copyFile) then
            Console.WriteLine("Move existing file '{0}' to '{1}'", destinationFile, copyFile)
            File.Move(destinationFile, copyFile)        

    let copyFile(filePath: String, contantHashValue: String, content: Byte array) =
        let destinationFile = Path.Combine(destinationDirectory, filePath)
        Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)) |> ignore
        if File.Exists(destinationFile) then
            // check if same hash, if so, skip the file move
            if not <| sha256(File.ReadAllBytes(destinationFile)).Equals(contantHashValue) then
                moveFile(destinationFile)
                Console.WriteLine("Copy '{0}' to '{1}'", filePath, destinationFile)
        else
            File.WriteAllBytes(destinationFile, content)
            Console.WriteLine("Copy '{0}' to '{1}'", filePath, destinationFile)

    let getFiles(fileList: String) =
        fileList.Split([|'\r'; '\n'|], StringSplitOptions.RemoveEmptyEntries)
        |> Array.filter(String.IsNullOrWhiteSpace >> not)
        |> Array.map(fun line -> 
            let items = line.Split([|','|])
            (items.[0], String.Join(",", items.[1..]))
        )

    let verifyIntegrity(directory: String, catalogFileName: String) =
        // read catalog
        let catalog = File.ReadAllText(Path.Combine(directory, catalogFileName))
        let files = getFiles(catalog)
        
        // check integrity
        let mutable integrityFailedOnFile = String.Empty        
        files
        |> Array.filter(fun (hashValue, _) -> not(String.IsNullOrWhiteSpace(hashValue)))
        |> Array.forall(fun (hashValue, filePath) ->
            // try to read the file content via hash or file name (in case of installer)
            let fullFileName =
                let tmp = Path.Combine(directory, hashValue)
                if File.Exists(tmp) then tmp
                else Path.Combine(directory, filePath)

            let content = File.ReadAllBytes(fullFileName)
            if compareHash(hashValue, content) then
                true
            else
                integrityFailedOnFile <- filePath
                false
        )
        |> fun result ->
            if result then Ok ()
            else Error("Integrity check failed on file: " + integrityFailedOnFile)

    let extractZip(zipArchive: ZipArchive, destinationDirectory: String) =
        Directory.CreateDirectory(destinationDirectory) |> ignore

        zipArchive.Entries
        |> Seq.iter(fun entry ->
            let fileName = Path.Combine(destinationDirectory, entry.FullName)
            Directory.CreateDirectory(Path.GetDirectoryName(fileName)) |> ignore
            let content = readEntry(zipArchive, entry.FullName)
            File.WriteAllBytes(fileName, content)
        )

        destinationDirectory

    let isOkToCopy(patternsSkipOnExist: List<String>) (hashValueFile: String, filePath: String) =
        if File.Exists(filePath) && patternsSkipOnExist.Count > 0 then
            // is forbidden pattern            
            patternsSkipOnExist 
            |> Seq.exists(fun pattern -> Regex.IsMatch(filePath, pattern))
            |> not
        else
            true

    let copyAllFiles(extractedDirectory: String, files: (String * String) array, patternsSkipOnExist: List<String>) =
        files        
        |> Array.filter(isOkToCopy patternsSkipOnExist)
        |> Array.iter(fun (hashValue, filePath) ->
            let sourceFilePath = Path.Combine(extractedDirectory, hashValue)
            if File.Exists(sourceFilePath) then
                let content = File.ReadAllBytes(sourceFilePath)
                copyFile(filePath, hashValue, content)
        )  

    let createInstallerMutex(argumentString: String) =
        let mutexName = 
            Regex.Replace(argumentString, "[^a-zA-Z]+", String.Empty)            
            |> Encoding.UTF8.GetBytes
            |> sha256
            
        AbbandonedMutex.mutex <- Some <| new Mutex(true, mutexName)        

    let runInstaller(processInfo: ProcessStartInfo) =
        try            
            createInstallerMutex(processInfo.Arguments)
            Console.WriteLine("Run installer: {0} {1}", processInfo.FileName, processInfo.Arguments)
            Process.Start(processInfo) |> ignore
            Ok ()
        with e ->
            Error(e.ToString())

    let buildProcessObject(extractedDirectory: String) =
        let baseArgumentString = new StringBuilder()
        baseArgumentString.AppendFormat("--source \"{0}\" --dest \"{1}\"", extractedDirectory, destinationDirectory) |> ignore
                    
        let dllInstaller = Path.Combine(extractedDirectory, "Installer.dll")
        let exeInstaller = Path.Combine(extractedDirectory, "Installer.exe")
                
        if File.Exists(dllInstaller) then 
            Some("dotnet", String.Format("\"{0}\" {1}", dllInstaller, baseArgumentString.ToString()))
        elif File.Exists(exeInstaller) then 
            Some(exeInstaller, baseArgumentString.ToString())
        else None
        |> function
            | Some (installer, argumentString) ->
                new ProcessStartInfo(
                    FileName = installer,
                    UseShellExecute = false,
                    Arguments = argumentString
                )
                |> Some
            | None ->
                None        
        
    let runVerifiedInstaller(processInfo, extractedDirectory: String) =
        match verifyIntegrity(extractedDirectory, "installer-catalog") with
        | Ok _ -> runInstaller(processInfo)            
        | Error e -> Error e

    member private this.DoInstall(extractedDirectory: String, fileList: String, patternsSkipOnExist: List<String>) =
        match buildProcessObject(extractedDirectory) with
        | Some processInfo ->
            if this.SkipIntegrityCheck
            then runInstaller(processInfo)
            else runVerifiedInstaller(processInfo, extractedDirectory)
        | None ->
            let files = getFiles(fileList)
            copyAllFiles(extractedDirectory, files, patternsSkipOnExist)

            // cleanup spurious files
            if this.RemoveTempFile then Directory.Delete(extractedDirectory, true)
            Ok ()

    member val PatternsSkipOnExist = new List<String>() with get, set
    member val SkipIntegrityCheck = false with get, set
    member val RemoveTempFile = true with get, set

    member this.CopyUpdates(sourceDirectory: String) =
        let catalog = File.ReadAllText(Path.Combine(sourceDirectory, "catalog"))
        let files = getFiles(catalog)
        copyAllFiles(sourceDirectory, files, this.PatternsSkipOnExist)

    member internal this.InstallUpdate(directory: String, fileList: String) =
        match verifyIntegrity(directory, "catalog") with
        | Ok _ -> this.DoInstall(directory, fileList, this.PatternsSkipOnExist)            
        | Error e -> Error e

    member internal this.InstallUpdate(zipArchive: ZipArchive, fileList: String) =
        let tempDir = Path.Combine(Path.GetTempPath(), fileList |> Encoding.UTF8.GetBytes |> sha256)
        let extractedDirectory = extractZip(zipArchive, tempDir)
        Console.WriteLine("Update zip extracted to: {0}", extractedDirectory)
        this.InstallUpdate(extractedDirectory, fileList)