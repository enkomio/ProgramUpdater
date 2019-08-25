namespace ES.Update

open System
open System.IO
open System.IO.Compression
open System.Diagnostics
open System.Text
open System.Threading
open System.Reflection
open System.Text.RegularExpressions

module AbbandonedMutex =
    let mutable mutex: Mutex option = None

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
            else Error("Integrity check failed on file: " + integrityFailedOnFile)

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

    let copyAllFiles(extractedDirectory: String, files: (String * String) array) =
        files
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
        
    let runInstaller(installerProgram: String, extractedDirectory: String) =
        match verifyIntegrity(extractedDirectory, "installer-catalog") with
        | Ok _ ->
            let argumentString = 
                String.Format(
                    "--source \"{0}\" --dest \"{1}\" --exec \"{2}\" --args \"{3}\"", 
                    extractedDirectory, 
                    destinationDirectory,
                    Process.GetCurrentProcess().MainModule.FileName,
                    String.Join(" ", Environment.GetCommandLineArgs() |> Array.skip 1)
                )
            
            createInstallerMutex(argumentString)            

            Process.Start(
                new ProcessStartInfo(
                    FileName = installerProgram,
                    UseShellExecute = false,
                    Arguments = argumentString
                )) |> ignore
            Ok ()
        | Error e -> 
            Error e

    let install(extractedDirectory: String, fileList: String) =        
        let installerProgram = Path.Combine(extractedDirectory, "Installer.exe")
        if File.Exists(installerProgram) then 
            runInstaller(installerProgram, extractedDirectory)
        else 
            let files = getFiles(fileList)
            copyAllFiles(extractedDirectory, files)

            // cleanup spurious files
            Directory.Delete(extractedDirectory, true)
            Ok ()

    member this.CopyUpdates(sourceDirectory: String) =
        let catalog = File.ReadAllText(Path.Combine(sourceDirectory, "catalog"))
        let files = getFiles(catalog)
        copyAllFiles(sourceDirectory, files)

    member this.InstallUpdate(zipArchive: ZipArchive, fileList: String) =
        let tempDir = Path.Combine(Path.GetTempPath(), fileList |> Encoding.UTF8.GetBytes |> sha256)
        let extractedDirectory = extractZip(zipArchive, tempDir)

        // install
        let result =
            match verifyIntegrity(extractedDirectory, "catalog") with
            | Ok _ -> install(extractedDirectory, fileList)            
            | Error e -> Error e

        // return
        result