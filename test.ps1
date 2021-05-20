$ErrorActionPreference = "Stop"

.\build.ps1

Function Test-Output($buildOutput) {
    if (-Not ($buildOutput | select-string "warning : Unoptimized assembly detected: 'EmptyLibDebug.dll'"))
    {
        throw "Didn't detect EmptyLibDebug as unoptimized"
    }
    if (-Not ($buildOutput | select-string "warning : Unoptimized assembly detected: 'StructureMap.dll'"))
    {
        throw "Didn't detect StructureMap as unoptimized"
    }
}

# .NET Core MSBuild
Test-Output(dotnet build -c release -f net461 sample\Sample\Sample.csproj)
Test-Output(dotnet build -c release -f netcoreapp2.1 sample\Sample\Sample.csproj)

# .NET Framework MSBuild:
Test-Output(msbuild /t:"Restore;Build" /p:Configuration=Release /p:TargetFrameworks=net461 sample\Sample\Sample.csproj)
