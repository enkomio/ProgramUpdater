open System
open System.IO
open System.Diagnostics

let ilMerge = Path.Combine("packages", "ilmerge", "tools", "net452", "ILMerge.exe")

[
    (Path.Combine("Installer", "bin", "Release"), "Installer.exe")
    (Path.Combine("Installer.Core", "bin", "Release", "netcoreapp3.1"), "Installer.exe")
]
|> List.iter(fun (projectDir, mainExecutable) ->
    let mainExe = Path.Combine(projectDir, mainExecutable)
    let allFiles = 
        Directory.GetFiles(projectDir, "*.dll", SearchOption.AllDirectories) 
        |> List.ofArray
        |> List.map(Path.GetFullPath)

    let arguments = String.Join(" ", mainExe::allFiles)
    Console.WriteLine("Cmd: {0} {1}", ilMerge, arguments)
    Process.Start(ilMerge, arguments).WaitForExit()
)