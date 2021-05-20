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
            // Thread.Sleep(10000);
            var outputFileDll = $"{AssemblyName}.dll";
            var outputFileExe = $"{AssemblyName}.exe";
            var assemblyPaths = ReferencePath.Split(';');
            foreach (var assemblyPath in assemblyPaths)
            {
                var file = Path.GetFileName(assemblyPath);
                if (file.StartsWith("System")
                    // || file.StartsWith("Microsoft")
                    || assemblyPath.Contains("NETCore.App.Ref")
                    // || file.Equals(outputFileDll, StringComparison.InvariantCultureIgnoreCase)
                    // || file.Equals(outputFileExe, StringComparison.InvariantCultureIgnoreCase)
                    )
                {
                    continue;
                }
                Log.LogMessage(MessageImportance.Low, "Checking if assembly is optimized: {0}", assemblyPath);

                using var stream = File.OpenRead(assemblyPath);
                using var reader = new PEReader(stream);
                var metadata = reader.GetMetadataReader();
                var assembly = metadata.GetAssemblyDefinition();
                foreach (var attribute in assembly.GetCustomAttributes())
                {
                    if (attribute.IsNil)
                    {
                        continue;
                    }
                    var customAttribute = metadata.GetCustomAttribute(attribute);
                    var ctor = metadata.GetMemberReference((MemberReferenceHandle)customAttribute.Constructor);
                    var attrType = metadata.GetTypeReference((TypeReferenceHandle)ctor.Parent);
                    // metadata.GetTypeDefinition(attrType.ResolutionScope)

                    if (metadata.GetString(attrType.Name) is not "DebuggableAttribute")
                    {
                        continue;
                    }
                    var sig = metadata.GetBlobBytes(customAttribute.Value);
                    Console.WriteLine("BitConverter: "+ BitConverter.ToInt32(sig, 0));
                    if (sig.Length != 8)
                    {
                        Console.WriteLine("Expected 8 bytes");
                    }
                    else
                    {
                        if (sig[2] == 7 && sig[3] == 1)
                        {
                            Log.LogMessage(MessageImportance.High, "Unoptimized assembly detected: " + assemblyPath);
                        }
                        else
                        {
                            Debug.Assert(sig[2] == 2 && sig[3] == 0);
                            Log.LogMessage(MessageImportance.Low, "Optimized assembly detected: " + assemblyPath);
                        }
                    }
                    var provider = new CustomAttributeTypeProvider();

                    CustomAttributeValue<string> value = customAttribute.DecodeValue(provider);
                    foreach (var customAttributeTypedArgument in value.FixedArguments)
                    {
                        var bitmask = int.Parse(customAttributeTypedArgument.Value!.ToString());
                        var isJitOptimizerDisabled = (bitmask & (int) DebuggableAttribute.DebuggingModes.DisableOptimizations) != 0;
                        // https://github.com/dotnet/runtime/blob/478571ca82dedc4f07f6a176709224adf3ee367a/src/libraries/System.Private.CoreLib/src/System/Diagnostics/DebuggableAttribute.cs#L49
                        Console.WriteLine("IsJITOptimizerDisabled " + isJitOptimizerDisabled);
                    }
                }

                // var asm = Assembly.ReflectionOnlyLoadFrom(assemblyPath);
                // if (!asm.IsOptimized())
                // {
                //     Log.LogMessage(MessageImportance.High, "Unoptimized assembly detected: " + assemblyPath);
                // }
                // else
                // {
                //     Log.LogMessage(MessageImportance.High, "Optimized assembly detected: " + assemblyPath);
                // }
            }
            return true;
        }
    }
    
    internal class CustomAttributeTypeProvider : DisassemblingTypeProvider, ICustomAttributeTypeProvider<string>
    {
        public string GetSystemType()
        {
            return "[System.Runtime]System.Type";
        }

        public bool IsSystemType(string type)
        {
            return type == "[System.Runtime]System.Type"  // encountered as typeref
                   || Type.GetType(type) == typeof(Type);    // encountered as serialized to reflection notation
        }

        public string GetTypeFromSerializedName(string name)
        {
            return name;
        }

        public PrimitiveTypeCode GetUnderlyingEnumType(string type)
        {
            return PrimitiveTypeCode.Int32;
        }
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
            Console.WriteLine("GetPrimitiveType: " + typeCode);
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
                    Debug.Assert(false);
                    throw new ArgumentOutOfRangeException(nameof(typeCode));
            }
        }

        public virtual string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind = 0)
        {
            TypeDefinition definition = reader.GetTypeDefinition(handle);

            string name = definition.Namespace.IsNil
                ? reader.GetString(definition.Name)
                : reader.GetString(definition.Namespace) + "." + reader.GetString(definition.Name);
            Console.WriteLine("GetTypeFromDefinition: " + name);

            // if (definition.Attributes.IsNested())
            // {
            //     TypeDefinitionHandle declaringTypeHandle = definition.GetDeclaringType();
            //     return GetTypeFromDefinition(reader, declaringTypeHandle, 0) + "/" + name;
            // }

            return name;
        }

        public virtual string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind = 0)
        {
            TypeReference reference = reader.GetTypeReference(handle);
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
        {
            return reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
        }

        public virtual string GetSZArrayType(string elementType)
        {
            return elementType + "[]";
        }

        public virtual string GetPointerType(string elementType)
        {
            return elementType + "*";
        }

        public virtual string GetByReferenceType(string elementType)
        {
            return elementType + "&";
        }

        public virtual string GetGenericMethodParameter(DisassemblingGenericContext genericContext, int index)
        {
            return "!!" + genericContext.MethodParameters[index];
        }

        public virtual string GetGenericTypeParameter(DisassemblingGenericContext genericContext, int index)
        {
            return "!" + genericContext.TypeParameters[index];
        }

        public virtual string GetPinnedType(string elementType)
        {
            return elementType + " pinned";
        }

        public virtual string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
        {
            return genericType + "<" + string.Join(",", typeArguments) + ">";
        }

        public virtual string GetArrayType(string elementType, ArrayShape shape)
        {
            var builder = new StringBuilder();

            builder.Append(elementType);
            builder.Append('[');

            for (int i = 0; i < shape.Rank; i++)
            {
                int lowerBound = 0;

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

        public virtual string GetTypeFromHandle(MetadataReader reader, DisassemblingGenericContext genericContext, EntityHandle handle)
        {
            switch (handle.Kind)
            {
                case HandleKind.TypeDefinition:
                    return GetTypeFromDefinition(reader, (TypeDefinitionHandle)handle);

                case HandleKind.TypeReference:
                    return GetTypeFromReference(reader, (TypeReferenceHandle)handle);

                case HandleKind.TypeSpecification:
                    return GetTypeFromSpecification(reader, genericContext, (TypeSpecificationHandle)handle);

                default:
                    throw new ArgumentOutOfRangeException(nameof(handle));
            }
        }

        public virtual string GetModifiedType(string modifierType, string unmodifiedType, bool isRequired)
        {
            return unmodifiedType + (isRequired ? " modreq(" : " modopt(") + modifierType + ")";
        }

        public virtual string GetFunctionPointerType(MethodSignature<string> signature)
        {
            ImmutableArray<string> parameterTypes = signature.ParameterTypes;

            int requiredParameterCount = signature.RequiredParameterCount;

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
    
    internal sealed class StringParameterValueTypeProvider : ISignatureTypeProvider<string, object?>
    {
        private readonly BlobReader valueReader;

        public StringParameterValueTypeProvider(MetadataReader reader, BlobHandle value)
        {
            Reader = reader;
            valueReader = reader.GetBlobReader(value);

            var prolog = valueReader.ReadUInt16();
            if (prolog != 1) throw new BadImageFormatException("Invalid custom attribute prolog.");
        }

        public MetadataReader Reader { get; }

        public string GetArrayType(string elementType, ArrayShape shape) => "";
        public string GetByReferenceType(string elementType) => "";
        public string GetFunctionPointerType(MethodSignature<string> signature) => "";
        public string GetGenericInstance(string genericType, ImmutableArray<string> typestrings) => "";
        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) { throw new NotImplementedException(); }
        public string GetGenericMethodParameter(int index) => "";
        public string GetGenericMethodParameter(object? genericContext, int index) { throw new NotImplementedException(); }
        public string GetGenericTypeParameter(int index) => "";
        public string GetGenericTypeParameter(object? genericContext, int index) { throw new NotImplementedException(); }
        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => "";
        public string GetPinnedType(string elementType) => "";
        public string GetPointerType(string elementType) => "";
        public string GetPrimitiveType(PrimitiveTypeCode typeCode)
        {
            if (typeCode == PrimitiveTypeCode.String) return valueReader.ReadSerializedString() ?? "";
            return "";
        }
        public string GetSZArrayType(string elementType) => "";
        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => "";
        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => "";
        public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind) => "";
    }
    internal static class MetadataHelper
    {
        public static string? ToString(this StringHandle handle, MetadataReader reader)
        {
            return handle.IsNil ? null : reader.GetString(handle);
        }
    }
}