namespace UpdateServer

open System
open System.IO
open System.Security.Cryptography
open ES.Fslog
open System.Reflection
open ES.Fslog.Loggers
open ES.Fslog.TextFormatters
open ES.Update.Backend

module Program =
    let private _logger =
        log "UpdateServer"
        |> info "CreateKeys" "Encryption keys not found. Generating them"
        |> info "PublicKey" "Public key: {0}"
        |> info "PrivateKey" "Private key first bytes: {0}"
        |> info "KeysCreated" "Encryption keys created and saved to files. The public key must be distributed togheter with the updater"
        |> build

    let private configureLogProvider(isVerbose: Boolean) =
        let path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
        let logProvider = new LogProvider()    
        let logLevel = if isVerbose then LogLevel.Verbose else LogLevel.Informational
        logProvider.AddLogger(new ConsoleLogger(logLevel, new ConsoleLogFormatter()))
        logProvider.AddLogger(new FileLogger(logLevel, Path.Combine(path, "updater-server.log")))
        logProvider :> ILogProvider

    let private createEncryptionKeys(settings: Settings) =
        let curDir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)
        let privateFile = Path.Combine(curDir, "private.bin")
        let publicFile = Path.Combine(curDir, "public.bin")

        if not(File.Exists(privateFile)) || not(File.Exists(publicFile)) then
            _logger?CreateKeys()
            use key =
                (new ECDiffieHellmanCng(
                    KeyDerivationFunction = ECDiffieHellmanKeyDerivationFunction.Hash,
                    HashAlgorithm = CngAlgorithm.Sha256
                )).Key

            let privateKey = key.Export(CngKeyBlobFormat.EccPrivateBlob) |> Convert.ToBase64String
            let publicKey = key.Export(CngKeyBlobFormat.EccPublicBlob) |> Convert.ToBase64String
        
            File.WriteAllText(privateFile, privateKey)
            File.WriteAllText(publicFile, publicKey)

            // data to print
            _logger?PublicKey(publicKey)
            _logger?PrivateKey(privateKey.[0..5])
            _logger?KeysCreated()

        settings.PrivateKey <- File.ReadAllText(privateFile)


    [<EntryPoint>]
    let main argv = 
        let logProvider = configureLogProvider(false)
        logProvider.AddLogSourceToLoggers(_logger)
        let settings = Settings.Read()

        // no private keys specified, read the content from file
        if String.IsNullOrWhiteSpace(settings.PrivateKey) then 
            createEncryptionKeys(settings)

        let server = new Server(settings.BindingAddress, settings.WorkspaceDirectory, settings.PrivateKey)
        server.Start()
        0
