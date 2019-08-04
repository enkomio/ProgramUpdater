namespace VersionReleaser

open System
open System.IO
open System.Reflection
open Argu
open ES.Fslog
open ES.Fslog.Loggers
open ES.Fslog.TextFormatters

module Program =
    type CLIArguments =
        | [<MainCommand; Last>] File of file:String   
        | Working_Dir of path:String
    with
        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | File _ -> "the release file to analyze in order to generate the metadata."
                | Working_Dir _ -> "the directory where the update artifacts will be saved."

    let printColor(msg: String, color: ConsoleColor) =
        Console.ForegroundColor <- color
        Console.WriteLine(msg)
        Console.ResetColor() 

    let printError(errorMsg: String) =
        printColor(errorMsg, ConsoleColor.Red)

    let printBanner() =
        Console.ForegroundColor <- ConsoleColor.Cyan        
        let banner = "-=[ Version Releaser ]=-"
        let year = if DateTime.Now.Year = 2019 then "2019" else String.Format("2019-{0}", DateTime.Now.Year)
        let copy = String.Format("Copyright (c) {0} Enkomio {1}", year, Environment.NewLine)
        Console.WriteLine(banner)
        Console.WriteLine(copy)
        Console.ResetColor()

    let createLogProvider() =
        let logProvider = new LogProvider()    
        let logFile = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "version-releaser.log")                
        logProvider.AddLogger(new ConsoleLogger(LogLevel.Informational, new ConsoleLogFormatter()))
        logProvider.AddLogger(new FileLogger(LogLevel.Informational, logFile))
        logProvider

    let printUsage(body: String) =
        Console.WriteLine(body)

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
                let workingDir = results.GetResult(<@ Working_Dir @>, Settings.Read().WorkspaceDirectory)
                Directory.CreateDirectory(workingDir) |> ignore
                let filename = results.GetResult(<@ File @>)
                let logProvider = createLogProvider()
                MetadataBuilder.createReleaseMetadata(workingDir, filename, logProvider)                
                0
        with 
            | :? ArguParseException ->
                printUsage(parser.PrintUsage())   
                1
            | e ->
                printError(e.ToString())
                1
