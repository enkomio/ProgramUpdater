namespace ES.Update

open System
open System.IO
open System.IO.Compression
open System.Diagnostics
open System.Text
open System.Threading
open System.Collections.Generic
open System.Text.RegularExpressions
open ES.Fslog

module AbbandonedMutex =
    let mutable mutex: Mutex option = None
    
type Installer(destinationDirectory: String, logProvider: ILogProvider) =    
    let _logger =
        log "Installer"
        |> info "ZipExtracted" "Update zip extracted to: {0}"
        |> info "RunInstaller" "Execute the configured installer"
        |> info "FilesCopied" "All update files were copied to: {0}"
        |> verbose "InstallerProcess" "Execute: {0} {1}"
        |> verbose "CopyFile" "Copy '{0}' to '{1}'"
        |> critical "InstallerIntegrityFail" "The integrity check of the installer failed"
        |> buildAndAdd logProvider

    let compareHash(hashValue: String, content: Byte array) =
        let computedHashValue = CryptoUtility.sha256(content)
        hashValue.Equals(computedHashValue, StringComparison.OrdinalIgnoreCase)

    let copyFile(filePath: String, content: Byte array) =
        let destinationFile = Path.Combine(destinationDirectory, filePath)
        Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)) |> ignore
        _logger?CopyFile(filePath, destinationFile)
        File.WriteAllBytes(destinationFile, content)

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
        |> Array.forall(fun (hashValue, filePath) ->
            // try to read the file content via hash or file name (in case of installer)
            let fullFileName =
                let tmp = Path.Combine(directory, hashValue)
                if File.Exists(tmp)
                then tmp
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
            else 
                _logger?InstallerIntegrityFail()
                Error("Integrity check failed on file: " + integrityFailedOnFile)

    let extractZip(zipArchive: ZipArchive, destinationDirectory: String) =
        Directory.CreateDirectory(destinationDirectory) |> ignore

        zipArchive.Entries
        |> Seq.iter(fun entry ->
            let fileName = Path.Combine(destinationDirectory, entry.FullName)
            Directory.CreateDirectory(Path.GetDirectoryName(fileName)) |> ignore
            let content = Utility.readEntry(zipArchive, entry.FullName)
            File.WriteAllBytes(fileName, content)
        )

        destinationDirectory

    let skipFile(patternsSkipOnExist: List<String>) (_: String, filePath: String) =
        if File.Exists(filePath) then
            // check if it matches the pattern, if so, skip it
            patternsSkipOnExist
            |> Seq.exists(fun pattern -> Regex.IsMatch(filePath, pattern) |> not)
        else
            true

    let copyAllFiles(extractedDirectory: String, files: (String * String) array, patternsSkipOnExist: List<String>) =
        files
        |> Array.filter(skipFile patternsSkipOnExist)
        |> Array.iter(fun (hashValue, filePath) ->
            let content = File.ReadAllBytes(Path.Combine(extractedDirectory, hashValue))
            copyFile(filePath, content)
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
            _logger?RunInstaller()
            _logger?InstallerProcess(processInfo.FileName, processInfo.Arguments)
            Process.Start(processInfo) |> ignore
            Ok ()
        with e ->
            Error(e.ToString())

    let buildProcessObject(extractedDirectory: String) =
        let isVerbose =
            logProvider.GetLoggers()
            |> Seq.exists(fun logger -> logger.Level = LogLevel.Verbose)
        
        let baseArgumentString =
            if isVerbose 
            then String.Format("--source \"{0}\" --dest \"{1}\" --verbose", extractedDirectory, destinationDirectory)
            else String.Format("--source \"{0}\" --dest \"{1}\"", extractedDirectory, destinationDirectory)

        let exeInstaller = Path.Combine(extractedDirectory, "Installer.exe")
        let dllInstaller = Path.Combine(extractedDirectory, "Installer.dll")

        if File.Exists(exeInstaller) then Some(exeInstaller, baseArgumentString)
        elif File.Exists(dllInstaller) then Some("dotnet", String.Format("{0} {1}", dllInstaller, baseArgumentString))
        else None
        |> function
            | Some (installer, argumentString) ->
                new ProcessStartInfo(
                    FileName = installer,
                    UseShellExecute = false,
                    Arguments = argumentString
                )
                |> Some
            | None -> None        
        
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
            _logger?FilesCopied(extractedDirectory)

            // cleanup spurious files
            Directory.Delete(extractedDirectory, true)
            Ok ()

    member val PatternsSkipOnExist = new List<String>() with get, set
    member val SkipIntegrityCheck = false with get, set

    member this.CopyUpdates(sourceDirectory: String) =
        let catalog = File.ReadAllText(Path.Combine(sourceDirectory, "catalog"))
        let files = getFiles(catalog)
        copyAllFiles(sourceDirectory, files, this.PatternsSkipOnExist)

    member this.InstallUpdate(zipArchive: ZipArchive, fileList: String) =
        let tempDir = Path.Combine(Path.GetTempPath(), fileList |> Encoding.UTF8.GetBytes |> sha256)
        let extractedDirectory = extractZip(zipArchive, tempDir)
        _logger?ZipExtracted(extractedDirectory)

        // install
        let result =
            match verifyIntegrity(extractedDirectory, "catalog") with
            | Ok _ -> this.DoInstall(extractedDirectory, fileList, this.PatternsSkipOnExist)            
            | Error e -> Error e

        // return
        result