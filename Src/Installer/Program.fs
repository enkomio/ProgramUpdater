namespace Installer

open System
open Argu
open ES.Update
open System.Diagnostics
open System.Threading
open System.Text
open System.Text.RegularExpressions

module Program =
    type CLIArguments = 
        | Source of path:String
        | Dest of path:String
        | Exec of fileName:String
        | Args of args:String
    with
        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | Source _ -> "the source directory containing the updated files."
                | Dest _ -> "the destination directory where the updated files must be copied."                
                | Exec _ -> "an option program to execute after the installation."
                | Args _ -> "the arguments to pass to the external program to execute after installation."                

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

    let runProgram(args: String) (fileName: String) =
        Process.Start(fileName, args) |> ignore

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
                let execArguments = results.GetResult(<@ Args @>, String.Empty)

                match (sourceDirectory, destinationDirectory) with
                | (Some sourceDirectory, Some destinationDirectory) ->
                    if waitForParentCompletation() then
                        runInstaller(sourceDirectory, destinationDirectory)
                        Console.WriteLine("Installation done!")
                        results.TryGetResult(<@ Exec @>) |> Option.iter(runProgram execArguments)
                        0
                    else
                        printError("Parent process didn't completed successfully")
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
