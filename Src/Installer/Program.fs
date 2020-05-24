namespace Installer

open System
open System.Threading
open System.Text
open System.Text.RegularExpressions
open System.IO

module Program =
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
        
    let printUsage() =
        Console.WriteLine("Usage: installer.exe <source dir> <destination dir>")
        
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

    let normalizeDestinationDirectory(destinationDirectory: String) =
        if destinationDirectory.EndsWith(string Path.DirectorySeparatorChar)
        then destinationDirectory
        else destinationDirectory + string Path.DirectorySeparatorChar

    let wait() =
        let secondsToWait = 5
        for i=0 to secondsToWait-1 do            
            Thread.Sleep(TimeSpan.FromSeconds(1.0))

    [<EntryPoint>]
    let main argv = 
        printBanner()
        
        try                      
            if argv.Length < 2 then
                printUsage()
                0
            else
                let sourceDirectory = argv.[0]
                let destinationDirectory = argv.[1]

                let destinationDirectory = normalizeDestinationDirectory(destinationDirectory)                
                if waitForParentCompletation() then
                    wait()
                    runInstaller(sourceDirectory, destinationDirectory)
                    0
                else
                    1
        with 
            | e ->
                printError(e.ToString())
                1
