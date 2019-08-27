// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------
#r "paket: groupref FakeBuild //"
#load ".fake/build.fsx/intellisense.fsx"

#r @"System.IO.Compression"
#r @"System.IO.Compression.FileSystem"

open System
open System.Text
open System.IO
open Fake
open Fake.Core.TargetOperators
open Fake.DotNet
open Fake.Core
open Fake.IO
 
// the project file name
let projectFileName = "ProgramUpdater"

// The name of the project
let project = "Program Updater Framework"

// Short summary of the project
let summary = "A framework to automatize the process of updating a program in an efficent and secure way."

// List of author names
let authors = "Enkomio"
    
// Build dir
let buildDir = "build"

// Release dir
let releaseDir = "release"

// Extension to not include in release
let forbiddenExtensions = [".pdb"]

// Projecy Guid
let projectGuid = "0F026EA5-501A-4947-B8E2-5860D3520E99"

// F# project names
let fsharpProjects = [
    "ES.Update"
    "ES.Update.Backend"
    "ES.Update.Releaser"
    "Installer"
    "Updater"
    "UpdateServer"
    "VersionReleaser"    
]

// C# project names
let csharpProjects = [    
]

//////////////////////////////////////////////////////////
// All code below should be generic enought             //  
// to not be modified in order to build the solution    //
//////////////////////////////////////////////////////////

// set the script dir as current
Directory.SetCurrentDirectory(__SOURCE_DIRECTORY__)

// Read additional information from the release notes document
let releaseNotesData = 
    let changelogFile = Path.Combine("..", "RELEASE_NOTES.md")
    File.ReadAllLines(changelogFile)
    |> ReleaseNotes.parse

let releaseNoteVersion = Version.Parse(releaseNotesData.AssemblyVersion)
let now = DateTime.UtcNow
let timeSpan = now.Subtract(new DateTime(1980,2,1,0,0,0))
let remaining = timeSpan.TotalDays % 30. |> int32
let months = timeSpan.TotalDays / 30. |> int32
let releaseVersion = string <| new Version(releaseNoteVersion.Major, releaseNoteVersion.Minor, months, now.Day + remaining)
Trace.trace("Build Version: " + releaseVersion)

// Targets
Core.Target.create "Clean" (fun _ ->
    Fake.IO.Shell.cleanDirs [buildDir; releaseDir]
)

Core.Target.create "SetAssemblyInfo" (fun _ ->
    fsharpProjects
    |> List.iter(fun projName ->
        let fileName = Path.Combine(projName, "AssemblyInfo.fs")    
        AssemblyInfoFile.createFSharp fileName [         
            DotNet.AssemblyInfo.Title project
            DotNet.AssemblyInfo.Product project
            DotNet.AssemblyInfo.Guid projectGuid
            DotNet.AssemblyInfo.Company authors
            DotNet.AssemblyInfo.Description summary
            DotNet.AssemblyInfo.Version releaseVersion        
            DotNet.AssemblyInfo.FileVersion releaseVersion
            DotNet.AssemblyInfo.InformationalVersion releaseVersion
            DotNet.AssemblyInfo.Metadata("BuildDate", DateTime.UtcNow.ToString("yyyy-MM-dd")) 
        ]
    )    

    csharpProjects
    |> List.iter(fun projName ->
        let fileName = Path.Combine(projName, "AssemblyInfo.cs")    
        AssemblyInfoFile.createCSharp fileName [         
            DotNet.AssemblyInfo.Title project
            DotNet.AssemblyInfo.Product project
            DotNet.AssemblyInfo.Guid projectGuid
            DotNet.AssemblyInfo.Company authors
            DotNet.AssemblyInfo.Description summary
            DotNet.AssemblyInfo.Version releaseVersion        
            DotNet.AssemblyInfo.FileVersion releaseVersion
            DotNet.AssemblyInfo.InformationalVersion releaseVersion
            DotNet.AssemblyInfo.Metadata("BuildDate", DateTime.UtcNow.ToString("yyyy-MM-dd")) 
        ]
    )    
)

Core.Target.create "Compile" (fun _ ->
    fsharpProjects
    |> List.iter(fun projectName ->
        let project = Path.Combine(projectName, projectName + ".fsproj")        
        let buildAppDir = Path.Combine(buildDir, projectName)
        Fake.IO.Directory.ensure buildAppDir

        // compile
        DotNet.MSBuild.runRelease id buildAppDir "Build" [project]
        |> Trace.logItems "Build Output: "
    )

    csharpProjects
    |> List.iter(fun projectName ->
        let project = Path.Combine(projectName, projectName + ".csproj")        
        let buildAppDir = Path.Combine(buildDir, projectName)
        Fake.IO.Directory.ensure buildAppDir

        // compile
        DotNet.MSBuild.runRelease id buildAppDir "Build" [project]
        |> Trace.logItems "Build Output: "
    )
)

Core.Target.create "Release" (fun _ ->
    let releaseDirectory = Path.Combine(releaseDir, String.Format("{0}.v{1}", projectFileName, releaseVersion))
    Directory.CreateDirectory(releaseDirectory) |> ignore

    // copy all files in the release dir    
    //Shell.copyDir releaseDirectory buildDir  (fun _ -> true)
    
    fsharpProjects@csharpProjects
    |> List.iter(fun projName -> 
        let buildProjectDir = Path.Combine(buildDir, projName)
        let releaseProjectDir = Path.Combine(releaseDirectory, projName)
        Shell.copyDir releaseProjectDir buildProjectDir (fun _ -> true)
    )
    
    // create zip file
    let releaseFilename = releaseDirectory + ".zip"
    Directory.GetFiles(releaseDirectory, "*.*", SearchOption.AllDirectories)
    |> Array.filter(fun file ->
        forbiddenExtensions
        |> List.contains (Path.GetExtension(file).ToLowerInvariant())
        |> not
    )
    |> Fake.IO.Zip.zip releaseDirectory releaseFilename
)

Core.Target.create "NuGet" (fun _ ->    
    let libDirectory = nugetDirectory + @"lib\"
    
    CleanDir nugetDirectory
    CreateDir nugetDirectory
    CreateDir libDirectory   
    
    packageFiles
    |> List.map (fun file -> buildDirectory + file)
    |> Copy libDirectory
     
    for package,description in packages do        
        NuGet (fun p ->
            {p with
                Authors = authors
                Project = package
                Description = description
                Version = release.NugetVersion
                OutputPath = nugetDirectory
                WorkingDir = nugetDirectory
                Summary = projectSummary
                Title = projectName
                ReleaseNotes = release.Notes |> toLines
                Dependencies = p.Dependencies
                AccessKey = getBuildParamOrDefault "nugetkey" String.Empty
                Publish = hasBuildParam "nugetkey"
                ToolPath = nuget  }) "fslog.nuspec"
)

"Clean"        
    ==> "SetAssemblyInfo"
    ==> "Compile" 
    ==> "Release"
    ==> "NuGet"
    
// Start build
Core.Target.runOrDefault "Release"