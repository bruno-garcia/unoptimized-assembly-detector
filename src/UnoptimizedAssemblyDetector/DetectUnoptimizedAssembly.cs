using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
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

        public override bool Execute()
        {
            var outputFileDll = $"{AssemblyName}.dll";
            var outputFileExe = $"{AssemblyName}.exe";
            var assemblyPaths = ReferencePath.Split(';');
            foreach (var assemblyPath in assemblyPaths)
            {
                var file = Path.GetFileName(assemblyPath);
                if (file.StartsWith("System")
                    || file.StartsWith("Microsoft")
                    || assemblyPath.Contains("NETCore.App.Ref")
                    || file.Equals(outputFileDll, StringComparison.InvariantCultureIgnoreCase)
                    || file.Equals(outputFileExe, StringComparison.InvariantCultureIgnoreCase))
                {
                    Log.LogMessage(MessageImportance.Low, "Skipping assembly: {0}", assemblyPath);
                    continue;
                }

                Log.LogMessage(MessageImportance.Low, "Checking if assembly is optimized: {0}", assemblyPath);

                using var stream = File.OpenRead(assemblyPath);
                using var reader = new PEReader(stream);
                var metadata = reader.GetMetadataReader();

                var isOptimized = true;
                foreach (var customAttributeTypedArgument in
                    from attribute in metadata.GetAssemblyDefinition().GetCustomAttributes()
                    where !attribute.IsNil
                    select metadata.GetCustomAttribute(attribute) into customAttribute
                    where metadata.GetString(metadata.GetTypeReference((TypeReferenceHandle)
                            metadata.GetMemberReference((MemberReferenceHandle)customAttribute.Constructor).Parent).Name)
                        is nameof(DebuggableAttribute)
                    select customAttribute.DecodeValue(new CustomAttributeTypeProvider()) into value
                    from customAttributeTypedArgument in value.FixedArguments
                    select customAttributeTypedArgument)
                {
                    if (!int.TryParse(customAttributeTypedArgument.Value?.ToString(), out var bitmask))
                    {
                        continue;
                    }
                    // https://github.com/dotnet/runtime/blob/478571ca82dedc4f07f6a176709224adf3ee367a/src/libraries/System.Private.CoreLib/src/System/Diagnostics/DebuggableAttribute.cs#L49
                    var isJitOptimizerDisabled = (bitmask & (int) DebuggableAttribute.DebuggingModes.DisableOptimizations) != 0;
                    Log.LogMessage(MessageImportance.Low, "DebuggableAttribute flags for: {0} is {1}. IsJitOptimizerDisabled: {2}.", file, bitmask, isJitOptimizerDisabled);
                    isOptimized = !isJitOptimizerDisabled;
                }

                if (isOptimized)
                {
                    Log.LogMessage(MessageImportance.Low, "Optimized assembly detected: " + assemblyPath);
                }
                else
                {
                    Log.LogWarning("Unoptimized assembly detected: {0}{1}{2}", file, Environment.NewLine, assemblyPath);
                }
            }
            return true;
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
