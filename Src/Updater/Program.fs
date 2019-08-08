namespace Updater

open System
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
    with
        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | Directory _ -> "the directory where to apply the update. If not specified use the current one."
                | Verbose -> "print verbose log messages."

    let private _logger =
        log "Updater"
        |> info "NewVersion" "Found a more recent version: {0}. Start update"
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

    let private doUpdate(currentVersion: Version) =
        let settings = Settings.Read()
        let updater = new Updater(new Uri(settings.UpdateBaseUri), settings.ProjectName, currentVersion, Convert.FromBase64String(settings.ServerPublicKey))
        let latestVersion = updater.GetLatestVersion()
        
        if latestVersion > currentVersion then
            _logger?NewVersion(latestVersion)
            let updates = updater.GetUpdates(currentVersion)
            ()

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
                doUpdate(currentVersion)
            0
        with 
            | :? ArguParseException ->
                printUsage(parser.PrintUsage())   
                1
            | e ->
                printError(e.ToString())
                1
