using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;

namespace UnoptimizedAssemblyDetector
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class AssemblyExtensions
    {
        /// <summary>
        /// Whether the assembly was compiled with the optimize+ flag
        /// </summary>
        /// <param name="asm">The assembly to verify the optimization flag</param>
        /// <returns>
        /// true if no <see cref="DebuggableAttribute"/> exists or
        /// <see cref="DebuggableAttribute.IsJITOptimizerDisabled"/> is false,
        /// otherwise, false.
        /// </returns>
        public static bool IsOptimized(this Assembly asm) 
            => asm.GetCustomAttribute<DebuggableAttribute>()?.IsJITOptimizerDisabled != true;
    }
}
