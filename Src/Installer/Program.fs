namespace Installer

open System
open Argu
open ES.Update
open System.Diagnostics
open System.Threading
open System.Text
open System.Text.RegularExpressions
open ES.Fslog
open System.IO
open System.Reflection
open ES.Fslog.Loggers
open ES.Fslog.TextFormatters

module Program =
    type CLIArguments = 
        | Source of path:String
        | Dest of path:String
        | Verbose
    with
        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | Source _ -> "the source directory containing the updated files."
                | Dest _ -> "the destination directory where the updated files must be copied."
                | Verbose -> "log verbose messages."

    let private _logger =
        log "Installer"
        |> info "InstallationDone" "The installation is completed"
        |> critical "ParentNotExited" "Parent process didn't completed successfully"
        |> build

    let printColor(msg: String, color: ConsoleColor) =
        Console.ForegroundColor <- color
        Console.WriteLine(msg)
        Console.ResetColor() 

    let printError(errorMsg: String) =
        printColor(errorMsg, ConsoleColor.Red)

    let printBanner() =
        Console.ForegroundColor <- ConsoleColor.Cyan        
        let banner = "-=[ Installer ]=-"
        let year = if DateTime.Now.Year = 2019 then "2019" else String.Format("2019-{0}", DateTime.Now.Year)
        let copy = String.Format("Copyright (c) {0} Enkomio {1}", year, Environment.NewLine)
        Console.WriteLine(banner)
        Console.WriteLine(copy)
        Console.ResetColor()
        
    let printUsage(body: String) =
        Console.WriteLine(body)

    let private configureLogProvider(destinationDirectory: String, verbose: Boolean) =
        let path = Path.Combine(destinationDirectory, "installer.log")
        let logProvider = new LogProvider()  
        let logLevel = if verbose then LogLevel.Verbose else LogLevel.Informational
        logProvider.AddLogger(new ConsoleLogger(logLevel, new ConsoleLogFormatter()))
        logProvider.AddLogger(new FileLogger(logLevel, path))
        logProvider :> ILogProvider  

    let runInstaller(sourceDirectory: String, destinationDirectory: String, logProvider: ILogProvider) =
        let installer = new Installer(destinationDirectory, logProvider)
        installer.CopyUpdates(sourceDirectory)

    let waitForParentCompletation() =
        let arguments = 
            String.Join(" ", Environment.GetCommandLineArgs() |> Array.skip 1)
            |> fun argumentString -> Regex.Replace(argumentString, "[^a-zA-Z]+", String.Empty)   
                    
        let mutexName = 
            arguments
            |> Encoding.UTF8.GetBytes
            |> sha256

        use mutex = new Mutex(false, mutexName)  
        
        try
            let result = mutex.WaitOne(TimeSpan.FromSeconds(10.))
            mutex.ReleaseMutex()
            result
        with :? AbandonedMutexException ->
            mutex.ReleaseMutex()
            true

    let normalizeDestinationDirectory(destinationDirectory: String) =
        if destinationDirectory.EndsWith(string Path.DirectorySeparatorChar)
        then destinationDirectory
        else destinationDirectory + string Path.DirectorySeparatorChar

    [<EntryPoint>]
    let main argv = 
        printBanner()
        
        let parser = ArgumentParser.Create<CLIArguments>()
        try            
            let results = parser.Parse(argv)
                    
            if results.IsUsageRequested then
                printUsage(parser.PrintUsage())
                0
            else
                let sourceDirectory = results.TryGetResult(<@ Source @>)
                let destinationDirectory = results.TryGetResult(<@ Dest @>)

                match (sourceDirectory, destinationDirectory) with
                | (Some sourceDirectory, Some rawDestinationDirectory) ->
                    let destinationDirectory = normalizeDestinationDirectory(rawDestinationDirectory)
                    let logProvider = configureLogProvider(destinationDirectory, results.Contains(<@ Verbose @>))
                    if waitForParentCompletation() then
                        logProvider.AddLogSourceToLoggers(_logger)
                        runInstaller(sourceDirectory, destinationDirectory, logProvider)
                        _logger?InstallationDone()
                        0
                    else
                        _logger?ParentNotExited("Parent process didn't completed successfully")
                        1
                | _ -> 
                    printError("Source or Destination directory not specified")
                    1
        with 
            | :? ArguParseException ->
                printUsage(parser.PrintUsage())   
                1
            | e ->
                printError(e.ToString())
                1
