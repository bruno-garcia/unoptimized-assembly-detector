# Unoptimized Assembly Detector

A NuGet package that detects when assemblies compiled without the `-optimized` flag are added to a project.

## How does it work?

This project hooks into the build process and detects if any referenced assembly was compiled in debug mode.

## Motivation

It's surprising to many that `dotnet publish` compiles assemblies in **debug** mode.
This means that unless you've specified `-c release`, you're building .NET assemblies without the `-optimize` compiler flag.

With this package, you can get warned if one of your dependencies is being built in `Debug` mode and published to `nuget.org`.
Now that you know about it at least, until a fix is shipped, you can ignore the warning for said package, 
roll back to an older version or chose another dependency.

## Configuration

#### Ignoring Assemblies

You can disable the verification and warning of specific assemblies by specifying:

```xml
<IgnoreAssemblies>MyAssembly1;AnotherAssembly2;TestCompany.*</IgnoreAssemblies>
```

Note that wildcards are accepted.

#### Cache Directory

By default this package uses the `obj` directory to cache the result of analysis. This can be configured through:

`<CacheDirectory>`