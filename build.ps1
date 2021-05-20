$ErrorActionPreference = "Stop"

Remove-Item -Force -Recurse -ErrorAction SilentlyContinue $env:USERPROFILE\.nuget\packages\unoptimizedassemblydetector\
Remove-Item -Force -Recurse -ErrorAction SilentlyContinue $env:USERPROFILE\nuget-local-feed\packages\unoptimizedassemblydetector\
Remove-Item .\nupkg\*.nupkg -ErrorAction SilentlyContinue 

dotnet pack -c release -nologo src/UnoptimizedAssemblyDetector/UnoptimizedAssemblyDetector.csproj

# .NET Core MSBuild
dotnet build -c release -f net461 sample\Sample\Sample.csproj
dotnet build -c release -f netcoreapp2.1 sample\Sample\Sample.csproj

# .NET Framework MSBuild:
msbuild /t:"Restore;Build" /p:Configuration=Release /p:TargetFrameworks=net461 sample\Sample\Sample.csproj
