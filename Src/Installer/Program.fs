namespace Installer

open System
open Argu
open ES.Update

module Program =
    type CLIArguments = 
        | Source of path:String
        | Dest of path:String
    with
        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | Source _ -> "the source directory containing the updated files."
                | Dest _ -> "the destination directory where the updated files must be copied."

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

    let runInstaller(sourceDirectory: String, destinationDirectory: String) =
        let installer = new Installer(destinationDirectory)
        installer.CopyUpdates(sourceDirectory)

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
                | (Some sourceDirectory, Some destinationDirectory) -> 
                    runInstaller(sourceDirectory, destinationDirectory)
                    0
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
