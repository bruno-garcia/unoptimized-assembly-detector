using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security;
using Microsoft.Build.Framework;
using MSBuildTask = Microsoft.Build.Utilities.Task;

namespace UnoptimizedAssemblyDetector
{
    public class DetectUnoptimizedAssembly : MSBuildTask
    {
        /// <summary>
        /// A list of referenced assembly paths split by semi colon.
        /// </summary>
       [Required] public string ReferencePath { get; set; } = null!;

        /// <summary>
        /// The name of the assembly being built.
        /// </summary>
        [Required] public string AssemblyName { get; set; } = null!;

        /// <summary>
        /// The Configuration of the current build.
        /// </summary>
        [Required] public string Configuration { get; set; } = null!;

        /// <summary>
        /// The project directory.
        /// </summary>
        [Required] public string ProjectDirectory { get; set; } = null!;

        public override bool Execute()
        {
            // In Debug build of this project, ignore anything under the solution directory
            // since project dependencies will also be compiled in debug mode.
            string? solutionDirectory = null;
            if (Configuration.Equals("Debug", StringComparison.InvariantCultureIgnoreCase))
            {
                solutionDirectory = GetSolutionDirectory();
            }

            var outputFileDll = $"{AssemblyName}.dll";
            var outputFileExe = $"{AssemblyName}.exe";
            var assemblyPaths = ReferencePath.Split(';');
            foreach (var assemblyPath in assemblyPaths)
            {
                var file = Path.GetFileName(assemblyPath);
                if (file.StartsWith("System")
                    || file.StartsWith("Microsoft")
                    || (solutionDirectory is not null && assemblyPath.StartsWith(solutionDirectory))
                    || assemblyPath.Contains($"{Path.DirectorySeparatorChar}ref{Path.DirectorySeparatorChar}")
                    || file.Equals(outputFileDll, StringComparison.InvariantCultureIgnoreCase)
                    || file.Equals(outputFileExe, StringComparison.InvariantCultureIgnoreCase))
                {
                    Log.LogMessage(MessageImportance.Low, "Skipping assembly: {0}", assemblyPath);
                    continue;
                }

                Log.LogMessage(MessageImportance.Low, "Checking if assembly is optimized: {0}", assemblyPath);

                try
                {
                    var isOptimized = CheckAssemblyIsOptimized(assemblyPath, file);

                    if (isOptimized)
                    {
                        Log.LogMessage(MessageImportance.Low, "Optimized assembly detected: " + assemblyPath);
                    }
                    else
                    {
                        Log.LogWarning("Unoptimized assembly detected: '{0}' at {1}", file, assemblyPath);
                    }
                }
                catch
                {
                    Log.LogMessage(MessageImportance.High, "Failed checking if assembly is optimized: {0}", assemblyPath);
                    throw;
                }

            }
            return true;
        }

        private string GetSolutionDirectory()
        {
            var projectDirectoryInfo = new DirectoryInfo(ProjectDirectory);
            var solutionDirectoryInfo = projectDirectoryInfo;
            do
            {
                if (solutionDirectoryInfo.GetFiles("*.sln").Length > 0)
                {
                    Log.LogMessage(MessageImportance.Low, "Using solution directory at: {0}", solutionDirectoryInfo.FullName);
                    break;
                }

                try
                {
                    if (solutionDirectoryInfo.Parent is null || !solutionDirectoryInfo.Parent.Exists)
                    {
                        solutionDirectoryInfo = projectDirectoryInfo;
                        Log.LogMessage(MessageImportance.Low, "No solution found, use the project directory: {0}",
                            solutionDirectoryInfo.FullName);
                        break;
                    }
                    solutionDirectoryInfo = solutionDirectoryInfo.Parent;
                }
                catch (SecurityException)
                {
                    Log.LogMessage(MessageImportance.Low, "Couldn't access: {0}. Using project directory {1}",
                        solutionDirectoryInfo.FullName,
                        projectDirectoryInfo.FullName);
                    solutionDirectoryInfo = projectDirectoryInfo;
                    break;
                }
            } while (true);

            return solutionDirectoryInfo.FullName;
        }

        private bool CheckAssemblyIsOptimized(string assemblyPath, string file)
        {
            using var stream = File.OpenRead(assemblyPath);
            using var reader = new PEReader(stream);
            var metadata = reader.GetMetadataReader();

            var isOptimized = true;
            foreach (var customAttributeTypedArgument in
                from attribute in metadata.GetAssemblyDefinition().GetCustomAttributes()
                where !attribute.IsNil
                select metadata.GetCustomAttribute(attribute)
                into customAttribute
                where SafeGetString(metadata, customAttribute)
                    is nameof(DebuggableAttribute)
                select customAttribute.DecodeValue(new CustomAttributeTypeProvider())
                into value
                from customAttributeTypedArgument in value.FixedArguments
                select customAttributeTypedArgument)
            {
                if (!int.TryParse(customAttributeTypedArgument.Value?.ToString(), out var bitmask))
                {
                    continue;
                }

                // https://github.com/dotnet/runtime/blob/478571ca82dedc4f07f6a176709224adf3ee367a/src/libraries/System.Private.CoreLib/src/System/Diagnostics/DebuggableAttribute.cs#L49
                var isJitOptimizerDisabled = (bitmask & (int) DebuggableAttribute.DebuggingModes.DisableOptimizations) != 0;
                Log.LogMessage(MessageImportance.Low, "DebuggableAttribute flags for: {0} is {1}. IsJitOptimizerDisabled: {2}.",
                    file, bitmask, isJitOptimizerDisabled);
                isOptimized = !isJitOptimizerDisabled;
            }

            return isOptimized;
        }

        private string SafeGetString(MetadataReader metadata, CustomAttribute customAttribute)
        {
            try
            {
                // If this is a ref assembly, VType comparison to 'uint MemberRef = 10' on explicit operator throws InvalidCast
                var memberReferenceHandle = (MemberReferenceHandle)customAttribute.Constructor;
                return metadata.GetString(metadata.GetTypeReference((TypeReferenceHandle)
                    metadata.GetMemberReference(memberReferenceHandle).Parent).Name);
            }
            catch (Exception e)
            {
                Log.LogMessage(MessageImportance.Low, "Failed to get attribute name: {0}.", e);
            }

            return "";
        }
    }

    internal class CustomAttributeTypeProvider : DisassemblingTypeProvider, ICustomAttributeTypeProvider<string>
    {
        public string GetSystemType() => "[System.Runtime]System.Type";

        public bool IsSystemType(string type) => type == "[System.Runtime]System.Type" || Type.GetType(type) == typeof(Type);

        public string GetTypeFromSerializedName(string name) => name;

        public PrimitiveTypeCode GetUnderlyingEnumType(string type) => PrimitiveTypeCode.Int32;
    }

    internal class DisassemblingTypeProvider : ISignatureTypeProvider<string, object>
    {
        public virtual string GetPrimitiveType(PrimitiveTypeCode typeCode) => "";

        public virtual string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind = 0)
            => throw new NotSupportedException();

        public virtual string GetTypeFromReference(
            MetadataReader reader,
            TypeReferenceHandle handle,
            byte rawTypeKind = 0)
            => "";

        public virtual string GetTypeFromSpecification(
            MetadataReader reader,
            object genericContext,
            TypeSpecificationHandle handle,
            byte rawTypeKind = 0)
            => reader
                .GetTypeSpecification(handle)
                .DecodeSignature(this, genericContext);

        public virtual string GetSZArrayType(string elementType) => throw new NotSupportedException();

        public virtual string GetPointerType(string elementType) => throw new NotSupportedException();

        public virtual string GetByReferenceType(string elementType) => throw new NotSupportedException();

        public virtual string GetGenericMethodParameter(object genericContext, int index)
            => throw new NotSupportedException();

        public virtual string GetGenericTypeParameter(object genericContext, int index)
            => throw new NotSupportedException();

        public virtual string GetPinnedType(string elementType)
            => throw new NotSupportedException();

        public virtual string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
            => throw new NotSupportedException();

        public virtual string GetArrayType(string elementType, ArrayShape shape)
            => throw new NotSupportedException();

        public virtual string GetModifiedType(string modifierType, string unmodifiedType, bool isRequired)
            => throw new NotSupportedException();

        public virtual string GetFunctionPointerType(MethodSignature<string> signature)
            => throw new NotSupportedException();
    }
}
