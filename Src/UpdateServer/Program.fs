namespace UpdateServer

open System
open System.IO
open ES.Fslog
open System.Reflection
open ES.Fslog.Loggers
open ES.Fslog.TextFormatters
open ES.Update.Backend

module Program =
    let private configureLogProvider(isVerbose: Boolean) =
        let path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
        let logProvider = new LogProvider()    
        let logLevel = if isVerbose then LogLevel.Verbose else LogLevel.Informational
        logProvider.AddLogger(new ConsoleLogger(logLevel, new ConsoleLogFormatter()))
        logProvider.AddLogger(new FileLogger(logLevel, Path.Combine(path, "updater-server.log")))
        logProvider :> ILogProvider

    [<EntryPoint>]
    let main argv = 
        let logProvider = configureLogProvider(false)
        let settings = Settings.Read()
        let server = new Server(settings.BindingAddress, settings.WorkspaceDirectory, logProvider)
        server.Start()
        0
