dotnet build -c debug -f netstandard2.0 -o ../Sample/lib/netstandard2.0 /p:AssemblyName=EmptyLibDebug
dotnet build -c debug -f net461 -o ../Sample/lib/net461 /p:AssemblyName=EmptyLibDebug

dotnet build -c release -f netstandard2.0 -o ../Sample/lib/netstandard2.0 /p:AssemblyName=EmptyLibRelease
dotnet build -c release -f net461 -o ../Sample/lib/net461 /p:AssemblyName=EmptyLibRelease
