﻿namespace ES.Update

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
    
type Installer(destinationDirectory: String, logProvider: ILogProvider) as this =    
    let _logger =
        log "Installer"
        |> info "ZipExtracted" "Update zip extracted to: {0}"
        |> info "FilesCopied" "All update files were copied to: {0}"
        |> info "RunInstaller" "Run installer: {0} {1}"
        |> info "NoInstaller" "No installer found in directory '{0}'. Copy files to '{1}'"
        |> verbose "CopyFile" "Copy '{0}' to '{1}'"
        |> verbose "MoveFile" "Move existing file '{0}' to '{1}'"
        |> verbose "SkipFile" "Skip file due to forbidden pattern: {0}"
        |> verbose "SkipFileSameHash" "Skip file becasue same hash: {0}"
        |> verbose "InstallerNotFound" "Installer {0} not found"
        |> critical "InstallerIntegrityFail" "The integrity check of the installer failed"
        |> buildAndAdd logProvider

    let compareHash(hashValue: String, content: Byte array) =
        let computedHashValue = CryptoUtility.sha256(content)
        hashValue.Equals(computedHashValue, StringComparison.OrdinalIgnoreCase)

    let moveFile(destinationFile: String) =
        let oldFilesDir = Path.Combine(destinationDirectory, "OLD-files")
        Directory.CreateDirectory(oldFilesDir) |> ignore
        let copyFile = Path.Combine(oldFilesDir, Path.GetFileName(destinationFile))
        if not <| File.Exists(copyFile) then
            _logger?MoveFile(destinationFile, copyFile)
            File.Move(destinationFile, copyFile)        

    let copyFile(filePath: String, contantHashValue: String, content: Byte array) =
        let destinationFile = Path.Combine(destinationDirectory, filePath)
        Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)) |> ignore
        if File.Exists(destinationFile) then
            // check if same hash, if so, skip the file move
            if not <| sha256(File.ReadAllBytes(destinationFile)).Equals(contantHashValue) then
                moveFile(destinationFile)
                _logger?CopyFile(filePath, destinationFile)
        else
            File.WriteAllBytes(destinationFile, content)
            _logger?CopyFile(filePath, destinationFile)

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

    let isOkToCopy(patternsSkipOnExist: List<String>) (hashValueFile: String, filePath: String) =
        if File.Exists(filePath) && patternsSkipOnExist.Count > 0 then
            // is forbidden pattern
            let skipFile =
                patternsSkipOnExist 
                |> Seq.exists(fun pattern -> Regex.IsMatch(filePath, pattern))

            if skipFile then _logger?SkipFile(filePath)
            not skipFile            
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
            _logger?RunInstaller(processInfo.FileName, processInfo.Arguments)
            Process.Start(processInfo) |> ignore
            Ok ()
        with e ->
            Error(e.ToString())

    let buildProcessObject(extractedDirectory: String) =
        let isVerbose =
            logProvider.GetLoggers()
            |> Seq.exists(fun logger -> logger.Level = LogLevel.Verbose)

        let baseArgumentString = new StringBuilder()
        baseArgumentString.AppendFormat("--source \"{0}\" --dest \"{1}\"", extractedDirectory, destinationDirectory) |> ignore
        
        if isVerbose then
            baseArgumentString.Append(" --verbose") |> ignore

        if not this.RemoveTempFile then
            baseArgumentString.Append(" --no-clean") |> ignore
            
        let dllInstaller = Path.Combine(extractedDirectory, "Installer.dll")
        if not <| File.Exists(dllInstaller) then
            _logger?InstallerNotFound(dllInstaller)

        let exeInstaller = Path.Combine(extractedDirectory, "Installer.exe")        
        if not <| File.Exists(dllInstaller) then
            _logger?InstallerNotFound(exeInstaller)
                
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
                _logger?NoInstaller(extractedDirectory, destinationDirectory)
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
            _logger?FilesCopied(destinationDirectory)

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
        _logger?ZipExtracted(extractedDirectory)
        this.InstallUpdate(extractedDirectory, fileList)