# Unoptimized Assembly Detector

A NuGet package that detects when assemblies compiled without the `-optimized` flag are added to a project warns you about it.

## How does it work?

This project hooks into the build process and detects if any referenced assembly was [compiled in _Debug_ mode](https://github.com/dotnet/runtime/blob/b9b876ab510e98ac741f1c82f1cb4fb1cb21e3ef/src/libraries/System.Private.CoreLib/src/System/Diagnostics/DebuggableAttribute.cs#L22). 

### Add to your project:

```xml
<ItemGroup>
  <PackageReference Include="UnoptimizedAssemblyDetector" Version="0.0.2" PrivateAssets="All" />
</ItemGroup>
```

## Motivation

It's surprising to many that `dotnet publish` compiles assemblies in **debug** mode.
This means that unless you've specified `-c release`, you're building .NET assemblies without the `-optimize` compiler flag.

With this package, you can get warned if one of your dependencies is being built in `Debug` mode and published to `nuget.org`. 
It can warn any referened assembly, not only added through NuGet.

Now that you know about it, you can  communicate to the author of the dependency. 
Wait until a fix is shipped, you can ignore the warning for said package, 
roll back to an older version or chose another dependency.
