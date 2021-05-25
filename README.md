<img src=".github/unoptimized.png" alt="Alerted Snail" width="70"/> 

# Unoptimized Assembly Detector
[![build](https://github.com/bruno-garcia/unoptimized-assembly-detector/workflows/ci/badge.svg?branch=main)](https://github.com/bruno-garcia/unoptimized-assembly-detector/actions?query=branch%3Aci)
[![NuGet](https://img.shields.io/nuget/v/UnoptimizedAssemblyDetector.svg)](https://www.nuget.org/packages/UnoptimizedAssemblyDetector)

NuGet package that detects when assemblies compiled without the `-optimized` flag are added to a project and warns you about it.

## How does it work?

This project hooks into the build process and detects if any referenced assembly was [compiled in _Debug_ mode](https://github.com/dotnet/runtime/blob/b9b876ab510e98ac741f1c82f1cb4fb1cb21e3ef/src/libraries/System.Private.CoreLib/src/System/Diagnostics/DebuggableAttribute.cs#L22). 

![UnoptimizedAssemblyDetector in action](https://raw.githubusercontent.com/bruno-garcia/unoptimized-assembly-detector/main/.github/unoptimized-assembly-detected.gif)

### Add to your project:

```xml
<ItemGroup>
  <PackageReference Include="UnoptimizedAssemblyDetector" Version="0.1.1" PrivateAssets="All" />
</ItemGroup>
```

## Motivation

It's surprising to many that `dotnet publish` and `dotnet pack` compile assemblies in **debug** mode.
This means that unless you've specified `-c release`, you're building .NET assemblies without the `-optimize` compiler flag.

With this package, you can get warned if one of your dependencies is being built in `Debug` mode and published to `nuget.org`. 
It can warn any referened assembly, not only added through NuGet.

Now that you know about it, you can  communicate to the author of the dependency. 
Wait until a fix is shipped, you can ignore the warning for said package, 
roll back to an older version or chose another dependency.

## Acknowledgements

* [Sentry](https://sentry.io/for/dot-net/) for the [craft](https://github.com/getsentry/craft) release tool used in this project.
* Icon made by Freepik from [www.flaticon.com](https://www.flaticon.com).
* Some of the blog posts that helped:
  * [Implementing and Debugging Custom MSBuild Tasks](https://ithrowexceptions.com/2020/08/04/implementing-and-debugging-custom-msbuild-tasks.html) by Matthias Koch.
  * [Shipping a cross-platform MSBuild task in a NuGet package](https://natemcmaster.com/blog/2017/07/05/msbuild-task-in-nuget/) by Nate MacMaster.
  * [Alexey Golub](https://github.com/Tyrrrz) for the links and rants.
