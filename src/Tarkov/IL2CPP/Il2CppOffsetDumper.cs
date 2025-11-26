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
        private ulong _metadataRegistration;
        private ulong _codeRegistration;

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

                // Try to find metadata registration (optional, for advanced features)
                _metadataRegistration = FindMetadataRegistration();
                if (_metadataRegistration != 0)
                {
                    DebugLogger.LogInfo($"IL2CPP MetadataRegistration: 0x{_metadataRegistration:X}");
                }
                else
                {
                    DebugLogger.LogWarning("MetadataRegistration not found - class dumping will be limited");
                }

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

                if (_il2cppDomain == 0)
                {
                    DebugLogger.LogError("IL2CPP domain not initialized");
                    return;
                }

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
                    DebugLogger.LogInfo($"Image Address: 0x{assembly.Image:X}");
                    DebugLogger.LogInfo($"Type Count: {image.TypeCount}");
                    DebugLogger.LogInfo($"Type Start Index: {image.TypeStart}");
                    DebugLogger.LogInfo($"Assembly Index: {image.AssemblyIndex}");

                    // Read assembly name info if available
                    if (assembly.AName != 0)
                    {
                        var asmName = _memory.ReadValue<Il2CppAssemblyName>(assembly.AName);
                        string name = _memory.ReadUtf8String(asmName.Name, 128);
                        DebugLogger.LogInfo($"Assembly Full Name: {name}");
                        DebugLogger.LogInfo($"Version: {asmName.Major}.{asmName.Minor}.{asmName.Build}.{asmName.Revision}");
                    }

                    DebugLogger.LogInfo($"\nNote: Full class enumeration requires Il2CppMetadataRegistration");
                    DebugLogger.LogInfo($"Use DumpClass() if you know the specific class name.");

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
        /// Dump all assemblies loaded in the domain
        /// </summary>
        public void DumpAllAssemblies()
        {
            try
            {
                DebugLogger.LogInfo("\n=== Dumping All Assemblies ===");

                if (_il2cppDomain == 0)
                {
                    DebugLogger.LogError("IL2CPP domain not initialized");
                    return;
                }

                var domain = _memory.ReadValue<Il2CppDomain>(_il2cppDomain);
                DebugLogger.LogInfo($"Total Assemblies: {domain.AssemblyCount}\n");

                // Iterate through all assemblies
                for (uint i = 0; i < domain.AssemblyCount; i++)
                {
                    try
                    {
                        var assemblyPtr = _memory.ReadPtr(domain.Assemblies + (i * 8));
                        if (assemblyPtr == 0) continue;

                        var assembly = _memory.ReadValue<Il2CppAssembly>(assemblyPtr);
                        if (assembly.Image == 0) continue;

                        var image = _memory.ReadValue<Il2CppImage>(assembly.Image);
                        string imageName = _memory.ReadUtf8String(image.Name, 128);

                        DebugLogger.LogInfo($"[{i}] {imageName}");
                        DebugLogger.LogInfo($"    Type Count: {image.TypeCount}");
                        DebugLogger.LogInfo($"    Image: 0x{assembly.Image:X}");
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.LogDebug($"Error reading assembly {i}: {ex.Message}");
                    }
                }

                DebugLogger.LogInfo("\n=== Assembly Dump Complete ===");
            }
            catch (Exception ex)
            {
                DebugLogger.LogException(ex, "DumpAllAssemblies");
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
                DebugLogger.LogDebug("Searching for GameAssembly.dll...");

                // Use MemDMA to get module base
                var gameAssemblyBase = _memory.GetModuleBase("GameAssembly.dll");

                if (gameAssemblyBase == 0)
                {
                    DebugLogger.LogError("GameAssembly.dll not found in process modules");
                    return 0;
                }

                DebugLogger.LogDebug($"Found GameAssembly.dll at 0x{gameAssemblyBase:X}");
                return gameAssemblyBase;
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
                DebugLogger.LogDebug("Searching for IL2CPP domain...");

                // Try multiple signature patterns for IL2CPP domain
                string[] patterns = new[]
                {
                    "48 8B 05 ?? ?? ?? ?? 48 85 C0 74 ?? 48 8B 40 ??", // Common pattern
                    "48 8B 0D ?? ?? ?? ?? 48 85 C9 74 ?? 48 8B 41 ??", // Alternative pattern
                    "48 89 5C 24 ?? 57 48 83 EC 20 48 8B 05 ?? ?? ?? ??", // Domain get function start
                };

                foreach (var pattern in patterns)
                {
                    try
                    {
                        DebugLogger.LogDebug($"Trying pattern: {pattern}");
                        var signatureAddr = _memory.FindSignatureInModule(pattern, "GameAssembly.dll");

                        if (signatureAddr != 0)
                        {
                            DebugLogger.LogDebug($"Found signature at 0x{signatureAddr:X}");

                            // Read RVA from the instruction
                            int rva = _memory.ReadValue<int>(signatureAddr + 3);
                            var domainPtr = signatureAddr + 7 + (ulong)rva;

                            DebugLogger.LogDebug($"Domain pointer at 0x{domainPtr:X}");

                            var domain = _memory.ReadPtr(domainPtr);

                            if (domain != 0)
                            {
                                DebugLogger.LogInfo($"Found IL2CPP domain at 0x{domain:X}");
                                return domain;
                            }
                            else
                            {
                                DebugLogger.LogDebug("Domain pointer was null, trying next pattern");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.LogDebug($"Pattern failed: {ex.Message}");
                    }
                }

                DebugLogger.LogWarning("Could not locate IL2CPP domain with any known pattern");
                DebugLogger.LogWarning("You may need to update the signature patterns for this game version");
                return 0;
            }
            catch (Exception ex)
            {
                DebugLogger.LogException(ex, "FindIl2CppDomain");
                return 0;
            }
        }

        private ulong FindMetadataRegistration()
        {
            try
            {
                DebugLogger.LogDebug("Searching for IL2CPP MetadataRegistration...");

                // MetadataRegistration is typically referenced in il2cpp_init or il2cpp_codegen_register
                string[] patterns = new[]
                {
                    // Pattern for MetadataRegistration access in il2cpp_codegen_register
                    "48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ??",
                    // Alternative pattern
                    "48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 48 8B 0D ?? ?? ?? ??",
                    // Another common pattern
                    "48 8B 0D ?? ?? ?? ?? 48 85 C9 74 ?? 48 8B 01",
                };

                foreach (var pattern in patterns)
                {
                    try
                    {
                        DebugLogger.LogDebug($"Trying MetadataRegistration pattern: {pattern.Substring(0, Math.Min(30, pattern.Length))}...");
                        var signatureAddr = _memory.FindSignatureInModule(pattern, "GameAssembly.dll");

                        if (signatureAddr != 0)
                        {
                            DebugLogger.LogDebug($"Found potential MetadataRegistration reference at 0x{signatureAddr:X}");

                            // Try to resolve the RVA
                            int rva = _memory.ReadValue<int>(signatureAddr + 3);
                            var metadataPtr = signatureAddr + 7 + (ulong)rva;

                            // MetadataRegistration is a structure, not a pointer
                            // So we return the address directly
                            DebugLogger.LogDebug($"MetadataRegistration structure at 0x{metadataPtr:X}");
                            return metadataPtr;
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.LogDebug($"MetadataRegistration pattern failed: {ex.Message}");
                    }
                }

                DebugLogger.LogDebug("Could not locate MetadataRegistration automatically");
                return 0;
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"FindMetadataRegistration error: {ex.Message}");
                return 0;
            }
        }

        private ulong FindClass(string className, string namespaceName)
        {
            try
            {
                if (_il2cppDomain == 0)
                    return 0;

                DebugLogger.LogDebug($"Searching for class '{namespaceName}.{className}'...");

                var domain = _memory.ReadValue<Il2CppDomain>(_il2cppDomain);

                // Iterate through all assemblies
                for (uint i = 0; i < domain.AssemblyCount; i++)
                {
                    var assemblyPtr = _memory.ReadPtr(domain.Assemblies + (i * 8));
                    if (assemblyPtr == 0) continue;

                    var assembly = _memory.ReadValue<Il2CppAssembly>(assemblyPtr);
                    if (assembly.Image == 0) continue;

                    var image = _memory.ReadValue<Il2CppImage>(assembly.Image);
                    string imageName = _memory.ReadUtf8String(image.Name, 128);

                    // For now, linear search through types
                    // TODO: Use hash table for better performance
                    var classPtr = SearchClassesInImage(image, className, namespaceName);
                    if (classPtr != 0)
                    {
                        DebugLogger.LogDebug($"Found class in assembly '{imageName}'");
                        return classPtr;
                    }
                }

                DebugLogger.LogDebug($"Class not found in any assembly");
                return 0;
            }
            catch (Exception ex)
            {
                DebugLogger.LogException(ex, "FindClass");
                return 0;
            }
        }

        /// <summary>
        /// Search for a class within an image by iterating through all type definitions
        /// This is a simplified linear search - production code would use Il2Cpp's hash tables
        /// </summary>
        private ulong SearchClassesInImage(Il2CppImage image, string className, string namespaceName)
        {
            try
            {
                // In IL2CPP, each image has a TypeStart index and TypeCount
                // The actual type definitions are stored in a global array
                // For now, this is a stub - full implementation requires reading the global metadata

                DebugLogger.LogDebug($"Searching {image.TypeCount} types in image...");

                // Note: This requires access to Il2CppMetadataRegistration which contains
                // the types array. This is more complex and requires additional signature scanning.

                return 0;
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"Error searching classes: {ex.Message}");
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
