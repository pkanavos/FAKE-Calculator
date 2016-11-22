// include Fake lib
#r @"packages\FAKE\tools\FakeLib.dll"

//That's for Powershell
#r "System.Management.Automation"

open Fake
open Fake.AssemblyInfoFile


RestorePackages()


// Directories
let buildDir  = @".\build\"
let testDir   = @".\test\"
let deployDir = @".\deploy\"
let packagesDir = @".\packages"

// tools
let fxCopRoot = @".\Tools\FxCop\FxCopCmd.exe"

// version info
let version = "0.2"  // or retrieve from CI server

// That's my version function! Year.WeekNumber.DayOfWeek.Hour
let myVersion =
     let dfi=System.Globalization.DateTimeFormatInfo.CurrentInfo
     let calendar=dfi.Calendar

     let now=DateTime.Now
     let weekNum=calendar.GetWeekOfYear(now,dfi.CalendarWeekRule,dfi.FirstDayOfWeek)
     String.Format("{0:yy}.{1}.{2}.{0:HHmm}",now,weekNum,(int)now.DayOfWeek  )

  let InvokeRemote server command =
    let block = ScriptBlock.Create(command)
    let pipe=PowerShell.Create()    
              .AddCommand("invoke-command")
    pipe.AddParameter("ComputerName", server)            
        .AddParameter("ScriptBlock", block)
        .Invoke() 
        |> Seq.map (sprintf  "%O")
        |> Seq.iter (fun line ->
                            let tracer=if line.Contains("not installed") then
                                            traceError 
                                       else 
                                            trace
                            tracer line)
    pipe.Streams.Error |> Seq.iter (traceError << sprintf "%O" )

// Targets
Target "Clean" (fun _ ->
    CleanDirs [buildDir; testDir; deployDir]
)

Target "SetVersions" (fun _ ->
    CreateCSharpAssemblyInfo "./src/app/Calculator/Properties/AssemblyInfo.cs"
        [Attribute.Title "Calculator Command line tool"
         Attribute.Description "Sample project for FAKE - F# MAKE"
         Attribute.Guid "A539B42C-CB9F-4a23-8E57-AF4E7CEE5BAA"
         Attribute.Product "Calculator"
         Attribute.Version version
         Attribute.FileVersion version]

    CreateCSharpAssemblyInfo "./src/app/CalculatorLib/Properties/AssemblyInfo.cs"
        [Attribute.Title "Calculator library"
         Attribute.Description "Sample project for FAKE - F# MAKE"
         Attribute.Guid "EE5621DB-B86B-44eb-987F-9C94BCC98441"
         Attribute.Product "Calculator"
         Attribute.Version version
         Attribute.FileVersion version]
)

Target "CompileApp" (fun _ ->
    !! @"src\app\**\*.csproj"
      |> MSBuildRelease buildDir "Build"
      |> Log "AppBuild-Output: "
)

Target "CompileTest" (fun _ ->
    !! @"src\test\**\*.csproj"
      |> MSBuildDebug testDir "Build"
      |> Log "TestBuild-Output: "
)

Target "NUnitTest" (fun _ ->
    !! (testDir + @"\NUnit.Test.*.dll")
      |> NUnit (fun p ->
                 {p with
                   DisableShadowCopy = true;
                   OutputFile = testDir + @"TestResults.xml"})
)

Target "FxCop" (fun _ ->
    !! (buildDir + @"\**\*.dll")
      ++ (buildDir + @"\**\*.exe")
        |> FxCop (fun p ->
            {p with
                ReportFileName = testDir + "FXCopResults.xml";
                ToolPath = fxCopRoot})
)

Target "Zip" (fun _ ->
    !! (buildDir + "\**\*.*")
        -- "*.zip"
        |> Zip buildDir (deployDir + "Calculator." + version + ".zip")
)

// Dependencies
"Clean"
  ==> "SetVersions"
  ==> "CompileApp"
  ==> "CompileTest"
  ==> "FxCop"
  ==> "NUnitTest"
  ==> "Zip"

// start build
RunTargetOrDefault "Zip"