namespace Updater

open System
open System.Collections.Generic
open Argu
open System.IO
open ES.Fslog
open System.Reflection
open ES.Fslog.Loggers
open ES.Fslog.TextFormatters
open ES.Update

module Program =
    type CLIArguments =
        | Verbose
        | Directory of path:String
        | Server_Uri of uri:String     
        | Project of name:String
        | Server_Key of key:String
        | Skip_On_Exist of patterns:String
    with
        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | Server_Key _ -> "the server key to sign updates."
                | Project _ -> "the name of the project that must be updated."
                | Server_Uri _ -> "the base uri of the update server."
                | Directory _ -> "the directory where to apply the update. If not specified use the current one."
                | Skip_On_Exist _ -> "a list of patterns for files fo copy only if not exist (config file, ...)."
                | Verbose -> "print verbose log messages."

    let private _logger =
        log "Updater"
        |> info "NewVersion" "Found a more recent version: {0}. Start update"
        |> info "UpdateDone" "Project '{0}' was updated to version '{1}' in directory: {2}"
        |> critical "UpdateError" "Update error: {0}"
        |> build

    let private printColor(msg: String, color: ConsoleColor) =
        Console.ForegroundColor <- color
        Console.WriteLine(msg)
        Console.ResetColor() 

    let private printError(errorMsg: String) =
        printColor(errorMsg, ConsoleColor.Red)

    let private printBanner() =
        Console.ForegroundColor <- ConsoleColor.Cyan        
        let banner = "-=[ Program Updater ]=-"
        let year = if DateTime.Now.Year = 2019 then "2019" else String.Format("2019-{0}", DateTime.Now.Year)
        let copy = String.Format("Copyright (c) {0} Enkomio {1}", year, Environment.NewLine)
        Console.WriteLine(banner)
        Console.WriteLine(copy)
        Console.ResetColor()
        
    let private printUsage(body: String) =
        Console.WriteLine(body)

    let private configureLogProvider(isVerbose: Boolean) =
        let path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
        let logProvider = new LogProvider()    
        let logLevel = if isVerbose then LogLevel.Verbose else LogLevel.Informational
        logProvider.AddLogger(new ConsoleLogger(logLevel, new ConsoleLogFormatter()))
        logProvider.AddLogger(new FileLogger(logLevel, Path.Combine(path, "updater-client.log")))
        logProvider :> ILogProvider  

    let private doUpdate(currentVersion: Version, baseUri: Uri, projectName: String, serverPublicKey: String, destinationDirectory: String, patternsSkipOnExist: String) =
        let updater = 
            new Updater(
                baseUri, 
                projectName, 
                currentVersion, 
                destinationDirectory,
                Convert.FromBase64String(serverPublicKey),
                PatternsSkipOnExist =
                    new List<String>(
                        if String.IsNullOrWhiteSpace(patternsSkipOnExist) then Array.empty
                        else patternsSkipOnExist.Split([|","|], StringSplitOptions.RemoveEmptyEntries)
                    )
            )

        let latestVersion = updater.GetLatestVersion()
        
        if latestVersion > currentVersion then
            _logger?NewVersion(latestVersion)
            let updateResult = updater.Update(currentVersion)
            if updateResult.Success 
            then _logger?UpdateDone(projectName, latestVersion, destinationDirectory)
            else _logger?UpdateError(updateResult.Error)

    [<EntryPoint>]
    let main argv = 
        printBanner()
        
        let parser = ArgumentParser.Create<CLIArguments>()
        try 
            let results = parser.Parse(argv)
            if results.IsUsageRequested then
                printUsage(parser.PrintUsage())                
            else                
                let logProvider = configureLogProvider(results.Contains(<@ Verbose @>))
                logProvider.AddLogSourceToLoggers(_logger)
                let currentVersion = Utility.readCurrentVersion()

                // run the update
                let settings = Settings.Read()                
                let serverUri = results.GetResult(<@ Server_Uri @>, settings.UpdateBaseUri)
                let projectName = results.GetResult(<@ Project @>, settings.ProjectName)
                let destinationDirectory = results.GetResult(<@ Directory @>, settings.DestinationDirectory)
                let serverKey = results.GetResult(<@ Server_Key @>, settings.ServerPublicKey)
                let patternsSkipOnExist = results.GetResult(<@ Skip_On_Exist @>, settings.PatternsSkipOnExist)
                doUpdate(
                    currentVersion, 
                    new Uri(serverUri), 
                    projectName, 
                    serverKey, 
                    destinationDirectory, 
                    patternsSkipOnExist
                )
            0
        with 
            | :? ArguParseException ->
                printUsage(parser.PrintUsage())   
                1
            | e ->
                printError(e.ToString())
                1
