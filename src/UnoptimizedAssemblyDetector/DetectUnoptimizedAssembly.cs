using System;
using System.Runtime.InteropServices;
using Microsoft.Build.Framework;
using MSBuildTask = Microsoft.Build.Utilities.Task;

// ReSharper disable once CheckNamespace
namespace MSBuildTasks
{
    public class DetectUnoptimizedAssembly : MSBuildTask
    {
        public string? CdnUrl { get; set; }
        public string Version { get; set; } = "latest";

        public string Architectures { get; set; } = "current";
        public string DestinationPath { get; set; } = "bin";

        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, "DetectUnoptimizedAssembly is running ...");
            return true;
        }

    }
}