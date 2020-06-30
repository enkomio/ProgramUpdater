namespace Installer

open System
open Argu
open ES.Update
open System.Diagnostics
open System.Threading
open System.Text
open System.Text.RegularExpressions
open ES.Fslog

module Program =
    open System.IO

    type CLIArguments = 
        | Public of path:String
        | Private of path:String
    with
        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | Public _ -> "if specified, the file where to save the public key."
                | Private _ -> "if specified, the file where to save the private key."

    let printColor(msg: String, color: ConsoleColor) =
        Console.ForegroundColor <- color
        Console.WriteLine(msg)
        Console.ResetColor() 

    let printError(errorMsg: String) =
        printColor(errorMsg, ConsoleColor.Red)

    let printBanner() =
        Console.ForegroundColor <- ConsoleColor.Cyan        
        let banner = "-=[ Keys Generator ]=-"
        let year = if DateTime.Now.Year = 2019 then "2019" else String.Format("2019-{0}", DateTime.Now.Year)
        let copy = String.Format("Copyright (c) {0} Enkomio {1}", year, Environment.NewLine)
        Console.WriteLine(banner)
        Console.WriteLine(copy)
        Console.ResetColor()
        
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
                let (publicKeyBytes, privateKeyBytes) = CryptoUtility.generateKeys()
                let (publicKey, privateKey) = (publicKeyBytes |> Convert.ToBase64String, privateKeyBytes |> Convert.ToBase64String)
                
                Console.WriteLine("Public key: " + publicKey)
                Console.WriteLine()
                Console.WriteLine("Private key: " + privateKey)
                Console.WriteLine()

                results.TryGetResult(<@ Public @>)
                |> Option.iter(fun file ->
                    File.WriteAllText(file, publicKey)
                    Console.WriteLine("Public key saved to: " + file)
                )

                results.TryGetResult(<@ Public @>)
                |> Option.iter(fun file ->
                    File.WriteAllText(file, privateKey)
                    Console.WriteLine("Private key saved to: " + file)
                )
                0
        with 
            | :? ArguParseException ->
                printUsage(parser.PrintUsage())   
                1
            | e ->
                printError(e.ToString())
                1
