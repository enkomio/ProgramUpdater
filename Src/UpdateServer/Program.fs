namespace UpdateServer

open System
open System.IO
open Argu
open ES.Fslog
open System.Reflection
open ES.Fslog.Loggers
open ES.Fslog.TextFormatters
open ES.Update.Backend

module Program =
    open ES.Update

    type CLIArguments =
        | Export_Key of file:String
        | Import_Key of file:String
        | Working_Dir of path:String
        | Binding_Address of uri:String
        | Verbose
    with
        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | Export_Key _ -> "export the private key to the specified file."
                | Import_Key _ -> "import the private key from the specified file."
                | Working_Dir _ -> "the directory where the update artifacts will be saved."
                | Binding_Address _ -> "the binding address of the update server."
                | Verbose -> "print verbose log messages."

    let private _logger =
        log "UpdateServer"
        |> info "CreateKeys" "Encryption keys not found. Generating them"
        |> info "PublicKey" "Public key: {0}"
        |> info "PrivateKey" "Private key first bytes: {0}"
        |> info "KeysCreated" "Encryption keys created and saved to files. The public key must be distributed togheter with the updater"
        |> info "KeyExported" "Private key exported to file: {0}"
        |> info "KeyImported" "Private key from file '{0}' imported. Be sure to set the public key accordingly."
        |> build

    let private printColor(msg: String, color: ConsoleColor) =
        Console.ForegroundColor <- color
        Console.WriteLine(msg)
        Console.ResetColor() 

    let private printError(errorMsg: String) =
        printColor(errorMsg, ConsoleColor.Red)

    let private printBanner() =
        Console.ForegroundColor <- ConsoleColor.Cyan        
        let banner = "-=[ Version Releaser ]=-"
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
        logProvider.AddLogger(new FileLogger(logLevel, Path.Combine(path, "updater-server.log")))
        logProvider :> ILogProvider  
        
    let private getKeyFileNames() =
        let curDir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)
        (
            Path.Combine(curDir, "private.txt"),
            Path.Combine(curDir, "public.txt")
        )

    let private createEncryptionKeys(settings: Settings) =
        let (privateFile, publicFile) = getKeyFileNames()

        if not(File.Exists(privateFile)) || not(File.Exists(publicFile)) then
            _logger?CreateKeys()
            let (privateKey, publicKey) = CryptoUtility.generateKeys()                  
            File.WriteAllText(privateFile, privateKey |> Utility.encryptKey |> Convert.ToBase64String)
            File.WriteAllText(publicFile, publicKey |> Convert.ToBase64String)
            _logger?KeysCreated()
        
        // read keys
        settings.PrivateKey <- Utility.readPrivateKey(privateFile)

        // log data
        _logger?PublicKey(File.ReadAllText(publicFile))
        _logger?PrivateKey(settings.PrivateKey.[0..5])

    [<EntryPoint>]
    let main argv = 
        printBanner()
        
        let parser = ArgumentParser.Create<CLIArguments>()
        try            
            let results = parser.Parse(argv)
            
            let logProvider = configureLogProvider(results.Contains(<@ Verbose @>))
            logProvider.AddLogSourceToLoggers(_logger)            

            if results.IsUsageRequested then
                printUsage(parser.PrintUsage())
                0
            else
                let settings = Settings.Read()

                // no private keys specified, read the content from file
                if String.IsNullOrWhiteSpace(settings.PrivateKey) then 
                    createEncryptionKeys(settings)

                if results.Contains(<@ Export_Key @>) then
                    let fileName = results.GetResult(<@ Export_Key @>)
                    File.WriteAllText(fileName, settings.PrivateKey)
                    _logger?KeyExported(fileName)

                elif results.Contains(<@ Import_Key @>) then
                    let fileName = results.GetResult(<@ Import_Key @>)
                    let key = File.ReadAllText(fileName) |> Convert.FromBase64String
                    let encryptedKey = Utility.encryptKey(key) |> Convert.ToBase64String
                    let (privateFile, _) = getKeyFileNames()
                    File.WriteAllText(privateFile, encryptedKey)
                    _logger?KeyImported(fileName)

                else
                    let settings = Settings.Read()
                    let workingDir = results.GetResult(<@ Working_Dir @>, settings.WorkspaceDirectory)
                    Directory.CreateDirectory(workingDir) |> ignore

                    let bindingAddress = results.GetResult(<@ Binding_Address @>, settings.BindingAddress)

                    let server = 
                        new WebServer(
                            new Uri(bindingAddress), 
                            workingDir, 
                            settings.PrivateKey |> Convert.FromBase64String,
                            logProvider
                        )
                    server.Start()       
                0
        with 
            | :? ArguParseException ->
                printUsage(parser.PrintUsage())   
                1
            | e ->
                printError(e.ToString())
                1