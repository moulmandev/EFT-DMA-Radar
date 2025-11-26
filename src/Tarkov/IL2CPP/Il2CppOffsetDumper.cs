using LoneEftDmaRadar.DMA;
using LoneEftDmaRadar.UI.Misc;
using System;
using System.Collections.Generic;
using static LoneEftDmaRadar.Tarkov.IL2CPP.Il2CppStructures;

namespace LoneEftDmaRadar.Tarkov.IL2CPP
{
    /// <summary>
    /// Runtime IL2CPP offset dumper for EFT
    /// Dumps class and field offsets from memory for SDK generation
    /// </summary>
    public class Il2CppOffsetDumper
    {
        private readonly MemDMA _memory;
        private ulong _gameAssemblyBase;
        private ulong _il2cppDomain;

        public Il2CppOffsetDumper()
        {
            _memory = Memory;
        }

        /// <summary>
        /// Initialize the dumper by locating IL2CPP domain and game assembly
        /// </summary>
        public bool Initialize()
        {
            try
            {
                DebugLogger.LogInfo("=== IL2CPP Offset Dumper Initialization ===");

                // Find GameAssembly.dll module
                _gameAssemblyBase = FindGameAssembly();
                if (_gameAssemblyBase == 0)
                {
                    DebugLogger.LogError("Failed to find GameAssembly.dll");
                    return false;
                }

                DebugLogger.LogInfo($"GameAssembly.dll base: 0x{_gameAssemblyBase:X}");

                // Locate IL2CPP domain
                _il2cppDomain = FindIl2CppDomain();
                if (_il2cppDomain == 0)
                {
                    DebugLogger.LogError("Failed to find IL2CPP domain");
                    DebugLogger.LogWarning("Note: IL2CPP domain signature may need updating for current game version");
                    return false;
                }

                DebugLogger.LogInfo($"IL2CPP Domain: 0x{_il2cppDomain:X}");
                DebugLogger.LogInfo("Initialization successful!");

                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.LogException(ex, "Il2CppOffsetDumper.Initialize");
                return false;
            }
        }

        /// <summary>
        /// Dump offsets for a specific class by name
        /// </summary>
        public void DumpClass(string className, string namespaceName = "")
        {
            try
            {
                DebugLogger.LogInfo($"\n=== Dumping Class: {namespaceName}.{className} ===");

                var classPtr = FindClass(className, namespaceName);
                if (classPtr == 0)
                {
                    DebugLogger.LogWarning($"Class '{className}' not found");
                    return;
                }

                var classData = _memory.ReadValue<Il2CppClass>(classPtr);

                // Read class name and namespace
                string actualName = _memory.ReadUtf8String(classData.Name, 128);
                string actualNamespace = _memory.ReadUtf8String(classData.Namespace, 128);

                DebugLogger.LogInfo($"Class: {actualNamespace}.{actualName}");
                DebugLogger.LogInfo($"Class Address: 0x{classPtr:X}");
                DebugLogger.LogInfo($"Instance Size: 0x{classData.InstanceSize:X}");
                DebugLogger.LogInfo($"Field Count: {classData.FieldCount}");

                // Dump all fields
                if (classData.FieldCount > 0 && classData.Fields != 0)
                {
                    DebugLogger.LogInfo("\n--- Fields ---");
                    DumpClassFields(classData);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogException(ex, $"DumpClass({className})");
            }
        }

        /// <summary>
        /// Dump all classes in a specific assembly (e.g., "Assembly-CSharp")
        /// </summary>
        public void DumpAssembly(string assemblyName)
        {
            try
            {
                DebugLogger.LogInfo($"\n=== Dumping Assembly: {assemblyName} ===");

                var domain = _memory.ReadValue<Il2CppDomain>(_il2cppDomain);
                DebugLogger.LogDebug($"Assembly Count: {domain.AssemblyCount}");

                // Iterate through all assemblies
                for (uint i = 0; i < domain.AssemblyCount; i++)
                {
                    var assemblyPtr = _memory.ReadPtr(domain.Assemblies + (i * 8));
                    if (assemblyPtr == 0) continue;

                    var assembly = _memory.ReadValue<Il2CppAssembly>(assemblyPtr);
                    if (assembly.Image == 0) continue;

                    var image = _memory.ReadValue<Il2CppImage>(assembly.Image);
                    string imageName = _memory.ReadUtf8String(image.Name, 128);

                    if (!imageName.Equals(assemblyName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    DebugLogger.LogInfo($"Found assembly: {imageName}");
                    DebugLogger.LogInfo($"Type Count: {image.TypeCount}");

                    // Note: Full assembly dump would iterate through all types
                    // For POC, we'll just log the assembly info
                    DebugLogger.LogInfo($"Assembly dump complete. Use DumpClass() for specific classes.");

                    return;
                }

                DebugLogger.LogWarning($"Assembly '{assemblyName}' not found");
            }
            catch (Exception ex)
            {
                DebugLogger.LogException(ex, $"DumpAssembly({assemblyName})");
            }
        }

        /// <summary>
        /// Dump offsets for multiple classes - useful for SDK generation
        /// </summary>
        public void DumpMultipleClasses(Dictionary<string, string> classes)
        {
            try
            {
                DebugLogger.LogInfo("\n=== Batch Class Dump ===");

                foreach (var kvp in classes)
                {
                    DumpClass(kvp.Key, kvp.Value);
                }

                DebugLogger.LogInfo("\n=== Batch Dump Complete ===");
            }
            catch (Exception ex)
            {
                DebugLogger.LogException(ex, "DumpMultipleClasses");
            }
        }

        #region Private Helper Methods

        private ulong FindGameAssembly()
        {
            try
            {
                // Try to find GameAssembly.dll using signature scanning
                // This is a simplified POC - actual implementation may vary
                DebugLogger.LogDebug("Searching for GameAssembly.dll...");

                // For POC, we return 0 to demonstrate the structure
                // Actual implementation would scan memory or use process module list
                DebugLogger.LogWarning("GameAssembly.dll lookup not fully implemented in POC");
                return 0;
            }
            catch (Exception ex)
            {
                DebugLogger.LogException(ex, "FindGameAssembly");
                return 0;
            }
        }

        private ulong FindIl2CppDomain()
        {
            try
            {
                // Fallback: Use signature scanning for il2cpp_domain_get pattern
                // This is a simplified approach - actual implementation would need game-specific patterns
                DebugLogger.LogDebug("Using signature-based domain lookup...");

                // Pattern for il2cpp domain access (game-specific, needs updating)
                const string domainPattern = "48 8B 05 ?? ?? ?? ?? 48 85 C0 74 ?? 48 8B 40 ??";

                try
                {
                    var signatureAddr = _memory.FindSignature(domainPattern);
                    if (signatureAddr != 0)
                    {
                        int rva = _memory.ReadValue<int>(signatureAddr + 3);
                        var domainPtr = signatureAddr + 7 + (ulong)rva;
                        var domain = _memory.ReadPtr(domainPtr);

                        if (domain != 0)
                        {
                            DebugLogger.LogDebug($"Found domain via signature: 0x{domain:X}");
                            return domain;
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.LogDebug($"Signature scan failed: {ex.Message}");
                }

                // If signature fails, return 0 - this indicates the pattern needs to be updated
                DebugLogger.LogWarning("Could not locate IL2CPP domain - signatures may need updating");
                return 0;
            }
            catch (Exception ex)
            {
                DebugLogger.LogException(ex, "FindIl2CppDomain");
                return 0;
            }
        }

        private ulong FindClass(string className, string namespaceName)
        {
            try
            {
                if (_il2cppDomain == 0)
                    return 0;

                var domain = _memory.ReadValue<Il2CppDomain>(_il2cppDomain);

                // Iterate through all assemblies
                for (uint i = 0; i < domain.AssemblyCount; i++)
                {
                    var assemblyPtr = _memory.ReadPtr(domain.Assemblies + (i * 8));
                    if (assemblyPtr == 0) continue;

                    var assembly = _memory.ReadValue<Il2CppAssembly>(assemblyPtr);
                    if (assembly.Image == 0) continue;

                    var image = _memory.ReadValue<Il2CppImage>(assembly.Image);

                    // Search for class in this image
                    // Note: Actual implementation would use the image's class hash table
                    // For POC, we demonstrate the structure
                }

                return 0;
            }
            catch (Exception ex)
            {
                DebugLogger.LogException(ex, "FindClass");
                return 0;
            }
        }

        private void DumpClassFields(Il2CppClass classData)
        {
            try
            {
                ulong fieldPtr = classData.Fields;

                for (int i = 0; i < classData.FieldCount; i++)
                {
                    var field = _memory.ReadValue<Il2CppStructures.FieldInfo>(fieldPtr);

                    string fieldName = _memory.ReadUtf8String(field.Name, 128);
                    string typeName = GetTypeName(field.Type);

                    DebugLogger.LogInfo($"  [{i}] {fieldName} (Offset: 0x{field.Offset:X}) - Type: {typeName}");

                    // Move to next field
                    fieldPtr += (ulong)System.Runtime.InteropServices.Marshal.SizeOf<Il2CppStructures.FieldInfo>();
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogException(ex, "DumpClassFields");
            }
        }

        private string GetTypeName(ulong typePtr)
        {
            try
            {
                if (typePtr == 0) return "Unknown";

                var type = _memory.ReadValue<Il2CppType>(typePtr);

                // Map IL2CPP type enum to readable names
                // This is simplified - full implementation would handle all types
                return type.Type switch
                {
                    0x01 => "void",
                    0x02 => "bool",
                    0x03 => "char",
                    0x04 => "sbyte",
                    0x05 => "byte",
                    0x06 => "short",
                    0x07 => "ushort",
                    0x08 => "int",
                    0x09 => "uint",
                    0x0A => "long",
                    0x0B => "ulong",
                    0x0C => "float",
                    0x0D => "double",
                    0x0E => "string",
                    0x15 => "class",
                    0x1C => "genericinst",
                    _ => $"Type_{type.Type:X2}"
                };
            }
            catch
            {
                return "Unknown";
            }
        }

        #endregion
    }
}
