using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
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
                    continue;
                }

                Log.LogMessage(MessageImportance.Low, "Checking if assembly is optimized: {0}", assemblyPath);

                using var stream = File.OpenRead(assemblyPath);
                using var reader = new PEReader(stream);
                var metadata = reader.GetMetadataReader();
                var assembly = metadata.GetAssemblyDefinition();
                var isOptimized = true;
                foreach (var customAttributeTypedArgument in
                    from attribute in assembly.GetCustomAttributes()
                    where !attribute.IsNil
                    select metadata.GetCustomAttribute(attribute)
                    into customAttribute
                    let ctor = metadata.GetMemberReference((MemberReferenceHandle)customAttribute.Constructor)
                    where metadata.GetString(metadata.GetTypeReference((TypeReferenceHandle)ctor.Parent).Name) is "DebuggableAttribute"
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

    internal class DisassemblingGenericContext
    {
        public DisassemblingGenericContext(ImmutableArray<string> typeParameters, ImmutableArray<string> methodParameters)
        {
            MethodParameters = methodParameters;
            TypeParameters = typeParameters;
        }

        public ImmutableArray<string> MethodParameters { get; }
        public ImmutableArray<string> TypeParameters { get; }
    }

    // Test implementation of ISignatureTypeProvider<TType, TGenericContext> that uses strings in ilasm syntax as TType.
    // A real provider in any sort of perf constraints would not want to allocate strings freely like this, but it keeps test code simple.
    internal class DisassemblingTypeProvider : ISignatureTypeProvider<string, DisassemblingGenericContext>
    {
        public virtual string GetPrimitiveType(PrimitiveTypeCode typeCode)
        {
            switch (typeCode)
            {
                case PrimitiveTypeCode.Boolean:
                    return "bool";

                case PrimitiveTypeCode.Byte:
                    return "uint8";

                case PrimitiveTypeCode.Char:
                    return "char";

                case PrimitiveTypeCode.Double:
                    return "float64";

                case PrimitiveTypeCode.Int16:
                    return "int16";

                case PrimitiveTypeCode.Int32:
                    return "int32";

                case PrimitiveTypeCode.Int64:
                    return "int64";

                case PrimitiveTypeCode.IntPtr:
                    return "native int";

                case PrimitiveTypeCode.Object:
                    return "object";

                case PrimitiveTypeCode.SByte:
                    return "int8";

                case PrimitiveTypeCode.Single:
                    return "float32";

                case PrimitiveTypeCode.String:
                    return "string";

                case PrimitiveTypeCode.TypedReference:
                    return "typedref";

                case PrimitiveTypeCode.UInt16:
                    return "uint16";

                case PrimitiveTypeCode.UInt32:
                    return "uint32";

                case PrimitiveTypeCode.UInt64:
                    return "uint64";

                case PrimitiveTypeCode.UIntPtr:
                    return "native uint";

                case PrimitiveTypeCode.Void:
                    return "void";

                default:
                    Debug.Fail("typeCode unknown");
                    throw new ArgumentOutOfRangeException(nameof(typeCode));
            }
        }

        public virtual string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind = 0)
        {
            var definition = reader.GetTypeDefinition(handle);

            string name = definition.Namespace.IsNil
                ? reader.GetString(definition.Name)
                : reader.GetString(definition.Namespace) + "." + reader.GetString(definition.Name);

            return name;
        }

        public virtual string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind = 0)
        {
            var reference = reader.GetTypeReference(handle);
            Handle scope = reference.ResolutionScope;

            string name = reference.Namespace.IsNil
                ? reader.GetString(reference.Name)
                : reader.GetString(reference.Namespace) + "." + reader.GetString(reference.Name);

            switch (scope.Kind)
            {
                case HandleKind.ModuleReference:
                    return "[.module  " + reader.GetString(reader.GetModuleReference((ModuleReferenceHandle)scope).Name) + "]" + name;

                case HandleKind.AssemblyReference:
                    var assemblyReferenceHandle = (AssemblyReferenceHandle)scope;
                    var assemblyReference = reader.GetAssemblyReference(assemblyReferenceHandle);
                    return "[" + reader.GetString(assemblyReference.Name) + "]" + name;

                case HandleKind.TypeReference:
                    return GetTypeFromReference(reader, (TypeReferenceHandle)scope) + "/" + name;

                default:
                    // rare cases:  ModuleDefinition means search within defs of current module (used by WinMDs for projections)
                    //              nil means search exported types of same module (haven't seen this in practice). For the test
                    //              purposes here, it's sufficient to format both like defs.
                    Debug.Assert(scope == Handle.ModuleDefinition || scope.IsNil);
                    return name;
            }
        }

        public virtual string GetTypeFromSpecification(MetadataReader reader, DisassemblingGenericContext genericContext, TypeSpecificationHandle handle, byte rawTypeKind = 0)
            => reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);

        public virtual string GetSZArrayType(string elementType) => elementType + "[]";

        public virtual string GetPointerType(string elementType) => elementType + "*";

        public virtual string GetByReferenceType(string elementType) => elementType + "&";

        public virtual string GetGenericMethodParameter(DisassemblingGenericContext genericContext, int index)
            => "!!" + genericContext.MethodParameters[index];

        public virtual string GetGenericTypeParameter(DisassemblingGenericContext genericContext, int index)
            => "!" + genericContext.TypeParameters[index];

        public virtual string GetPinnedType(string elementType) => elementType + " pinned";

        public virtual string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
            => genericType + "<" + string.Join(",", typeArguments) + ">";

        public virtual string GetArrayType(string elementType, ArrayShape shape)
        {
            var builder = new StringBuilder();

            builder.Append(elementType);
            builder.Append('[');

            for (var i = 0; i < shape.Rank; i++)
            {
                var lowerBound = 0;

                if (i < shape.LowerBounds.Length)
                {
                    lowerBound = shape.LowerBounds[i];
                    builder.Append(lowerBound);
                }

                builder.Append("...");

                if (i < shape.Sizes.Length)
                {
                    builder.Append(lowerBound + shape.Sizes[i] - 1);
                }

                if (i < shape.Rank - 1)
                {
                    builder.Append(',');
                }
            }

            builder.Append(']');
            return builder.ToString();
        }

        public virtual string GetModifiedType(string modifierType, string unmodifiedType, bool isRequired)
            => unmodifiedType + (isRequired ? " modreq(" : " modopt(") + modifierType + ")";

        public virtual string GetFunctionPointerType(MethodSignature<string> signature)
        {
            var parameterTypes = signature.ParameterTypes;

            var requiredParameterCount = signature.RequiredParameterCount;

            var builder = new StringBuilder();
            builder.Append("method ");
            builder.Append(signature.ReturnType);
            builder.Append(" *(");

            int i;
            for (i = 0; i < requiredParameterCount; i++)
            {
                builder.Append(parameterTypes[i]);
                if (i < parameterTypes.Length - 1)
                {
                    builder.Append(", ");
                }
            }

            if (i < parameterTypes.Length)
            {
                builder.Append("..., ");
                for (; i < parameterTypes.Length; i++)
                {
                    builder.Append(parameterTypes[i]);
                    if (i < parameterTypes.Length - 1)
                    {
                        builder.Append(", ");
                    }
                }
            }

            builder.Append(')');
            return builder.ToString();
        }
    }
}
