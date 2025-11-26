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

                DebugLogger.LogInfo($"IL2CPP Domain/Assemblies: 0x{_il2cppDomain:X}");

                // Try to find TYPE_INFO_TABLE (critical for class enumeration)
                _typeInfoTable = FindTypeInfoTable();
                if (_typeInfoTable != 0)
                {
                    DebugLogger.LogInfo($"TYPE_INFO_TABLE: 0x{_typeInfoTable:X}");
                }
                else
                {
                    DebugLogger.LogWarning("TYPE_INFO_TABLE not found - class dumping will be limited");
                }

                // Try to find metadata registration (optional, for advanced features)
                _metadataRegistration = FindMetadataRegistration();
                if (_metadataRegistration != 0)
                {
                    DebugLogger.LogInfo($"IL2CPP MetadataRegistration: 0x{_metadataRegistration:X}");
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

                var assembliesArray = _il2cppDomain;
                uint assemblyIndex = 0;
                const uint MAX_ASSEMBLIES = 500;

                // Iterate through assemblies array (null-terminated)
                while (assemblyIndex < MAX_ASSEMBLIES)
                {
                    var assemblyPtr = _memory.ReadPtr(assembliesArray + (assemblyIndex * 8));
                    if (assemblyPtr == 0) break; // End of array

                    var assembly = _memory.ReadValue<Il2CppAssembly>(assemblyPtr);
                    if (assembly.Image == 0)
                    {
                        assemblyIndex++;
                        continue;
                    }

                    var image = _memory.ReadValue<Il2CppImage>(assembly.Image);
                    if (image.Name == 0)
                    {
                        assemblyIndex++;
                        continue;
                    }

                    string imageName = _memory.ReadUtf8String(image.Name, 128);

                    if (!imageName.Equals(assemblyName, StringComparison.OrdinalIgnoreCase))
                    {
                        assemblyIndex++;
                        continue;
                    }

                    // Found the target assembly
                    DebugLogger.LogInfo($"Found assembly: {imageName}");
                    DebugLogger.LogInfo($"Assembly Ptr: 0x{assemblyPtr:X}");
                    DebugLogger.LogInfo($"Image Address: 0x{assembly.Image:X}");
                    DebugLogger.LogInfo($"Type Count: {image.TypeCount}");
                    DebugLogger.LogInfo($"Type Start Index: {image.TypeStart}");
                    DebugLogger.LogInfo($"Assembly Index: {image.AssemblyIndex}");

                    // Read assembly name info if available
                    if (assembly.AName != 0)
                    {
                        try
                        {
                            var asmName = _memory.ReadValue<Il2CppAssemblyName>(assembly.AName);
                            if (asmName.Name != 0)
                            {
                                string name = _memory.ReadUtf8String(asmName.Name, 128);
                                DebugLogger.LogInfo($"Assembly Full Name: {name}");
                                DebugLogger.LogInfo($"Version: {asmName.Major}.{asmName.Minor}.{asmName.Build}.{asmName.Revision}");
                            }
                        }
                        catch (Exception ex)
                        {
                            DebugLogger.LogDebug($"Could not read assembly name info: {ex.Message}");
                        }
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

                // _il2cppDomain points to the assemblies array (pointer to Il2CppAssembly**)
                // We iterate until we hit a null pointer (null-terminated array)
                var assembliesArray = _il2cppDomain;

                uint assemblyIndex = 0;
                uint validAssemblies = 0;
                const uint MAX_ASSEMBLIES = 500; // Safety limit

                DebugLogger.LogInfo("Iterating through assemblies array (null-terminated)...\n");

                // Iterate through pointer array until we hit null
                while (assemblyIndex < MAX_ASSEMBLIES)
                {
                    try
                    {
                        // Read pointer to Il2CppAssembly from the array
                        var assemblyPtr = _memory.ReadPtr(assembliesArray + (assemblyIndex * 8));

                        // If we hit a null pointer, we've reached the end
                        if (assemblyPtr == 0)
                        {
                            DebugLogger.LogDebug($"Hit null pointer at index {assemblyIndex}, end of assemblies");
                            break;
                        }

                        // Try to read the assembly structure
                        var assembly = _memory.ReadValue<Il2CppAssembly>(assemblyPtr);

                        // Validate the assembly has an image
                        if (assembly.Image == 0)
                        {
                            assemblyIndex++;
                            continue;
                        }

                        // Read the image data
                        var image = _memory.ReadValue<Il2CppImage>(assembly.Image);

                        // Validate image name pointer
                        if (image.Name == 0)
                        {
                            assemblyIndex++;
                            continue;
                        }

                        // Read the assembly name
                        string imageName = _memory.ReadUtf8String(image.Name, 128);

                        // Filter out invalid/empty names
                        if (string.IsNullOrWhiteSpace(imageName))
                        {
                            assemblyIndex++;
                            continue;
                        }

                        // This is a valid assembly
                        validAssemblies++;
                        DebugLogger.LogInfo($"[{validAssemblies}] {imageName}");
                        DebugLogger.LogInfo($"    Type Count: {image.TypeCount}");
                        DebugLogger.LogInfo($"    Type Start: {image.TypeStart}");
                        DebugLogger.LogInfo($"    Assembly Index: {image.AssemblyIndex}");
                        DebugLogger.LogInfo($"    Image: 0x{assembly.Image:X}");
                        DebugLogger.LogInfo($"    Assembly Ptr: 0x{assemblyPtr:X}\n");
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.LogDebug($"Error reading assembly at index {assemblyIndex}: {ex.Message}");
                    }

                    assemblyIndex++;
                }

                DebugLogger.LogInfo($"\n=== Assembly Dump Complete: Found {validAssemblies} valid assemblies ===");
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

        private ulong _assembliesPtr = 0; // Direct pointer to assemblies array (alternative to domain)
        private ulong _typeInfoTable = 0; // Global type info table

        private ulong FindIl2CppDomain()
        {
            try
            {
                DebugLogger.LogDebug("Searching for IL2CPP assemblies...");

                // Approach 1: Try to find assemblies array directly (like reference implementation)
                // Pattern: il2cpp_domain_get_assemblies function that does "mov rax, [rip+offset]"
                var assembliesPtr = FindAssembliesArray();
                if (assembliesPtr != 0)
                {
                    _assembliesPtr = assembliesPtr;
                    DebugLogger.LogInfo($"✓ Found assemblies array at 0x{assembliesPtr:X}");

                    // Create a fake domain structure for compatibility
                    // We don't actually need the domain, just the assemblies pointer
                    return assembliesPtr; // Return assemblies pointer as "domain"
                }

                // Approach 2: Try to find IL2CPP domain structure (original method)
                DebugLogger.LogDebug("Trying domain-based approach...");
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

                            try
                            {
                                // Calculate RVA offset based on pattern
                                int rvaOffset = 3;
                                int instructionEnd = 7;

                                // Adjust for different patterns
                                if (pattern.Contains("48 89 5C 24"))
                                {
                                    rvaOffset = 13;
                                    instructionEnd = 17;
                                }
                                else if (pattern.Contains("48 8B 0D"))
                                {
                                    rvaOffset = 3;
                                    instructionEnd = 7;
                                }

                                int rva = _memory.ReadValue<int>(signatureAddr + (ulong)rvaOffset);
                                var domainPtr = signatureAddr + (ulong)instructionEnd + (ulong)rva;

                                DebugLogger.LogDebug($"RVA offset: {rvaOffset}, RVA value: 0x{rva:X8}, Instruction end: {instructionEnd}");
                                DebugLogger.LogDebug($"Calculated domain pointer at 0x{domainPtr:X}");

                                // Validate range
                                var minAddr = _gameAssemblyBase > 0x10000000 ? _gameAssemblyBase - 0x10000000 : 0;
                                var maxAddr = _gameAssemblyBase + 0x10000000;

                                if (domainPtr < minAddr || domainPtr > maxAddr)
                                {
                                    DebugLogger.LogDebug($"Domain pointer outside reasonable range, trying next pattern");
                                    continue;
                                }

                                var domain = _memory.ReadPtr(domainPtr);

                                if (domain != 0)
                                {
                                    DebugLogger.LogInfo($"✓ Found IL2CPP domain at 0x{domain:X}");
                                    return domain;
                                }
                            }
                            catch (Exception innerEx)
                            {
                                DebugLogger.LogDebug($"Error processing signature: {innerEx.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.LogDebug($"Pattern failed: {ex.Message}");
                    }
                }

                DebugLogger.LogWarning("Could not locate IL2CPP domain/assemblies with any known pattern");
                DebugLogger.LogWarning("You may need to update the signature patterns for this game version");
                return 0;
            }
            catch (Exception ex)
            {
                DebugLogger.LogException(ex, "FindIl2CppDomain");
                return 0;
            }
        }

        /// <summary>
        /// Find the assemblies array directly (alternative to finding domain)
        /// Reference pattern: "il2cpp_domain_get_assemblies -> mov rax, offset"
        /// </summary>
        private ulong FindAssembliesArray()
        {
            try
            {
                // Pattern for il2cpp_domain_get_assemblies function
                // This function typically does: mov rax, [rip+offset] to load the assemblies array
                string[] patterns = new[]
                {
                    "48 8B 05 ?? ?? ?? ?? C3", // mov rax, [rip+??]; ret
                    "48 8B 05 ?? ?? ?? ?? 48 8B ?? C3", // mov rax, [rip+??]; mov ??, rax; ret
                    "48 8B 05 ?? ?? ?? ?? 48 8B ?? ?? C3", // Similar with extra instructions
                };

                foreach (var pattern in patterns)
                {
                    try
                    {
                        var signatureAddr = _memory.FindSignatureInModule(pattern, "GameAssembly.dll");

                        if (signatureAddr != 0)
                        {
                            DebugLogger.LogDebug($"Found assemblies pattern at 0x{signatureAddr:X}");

                            // Read RVA from mov instruction (offset +3, instruction ends at +7)
                            int rva = _memory.ReadValue<int>(signatureAddr + 3);
                            var assembliesPtr = signatureAddr + 7 + (ulong)rva;

                            DebugLogger.LogDebug($"Assemblies pointer at 0x{assembliesPtr:X}");

                            // Validate by checking if we can read a pointer from this address
                            var firstAssemblyPtr = _memory.ReadPtr(assembliesPtr);
                            if (firstAssemblyPtr != 0)
                            {
                                DebugLogger.LogDebug($"First assembly at 0x{firstAssemblyPtr:X} (validation passed)");
                                return assembliesPtr;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.LogDebug($"Assemblies pattern failed: {ex.Message}");
                    }
                }

                return 0;
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"FindAssembliesArray error: {ex.Message}");
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

        /// <summary>
        /// Find the TYPE_INFO_TABLE (global array of Il2CppClass pointers)
        /// Reference pattern: "48 89 05 ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 8B 48" - mov offset, rax
        /// </summary>
        private ulong FindTypeInfoTable()
        {
            try
            {
                DebugLogger.LogDebug("Searching for TYPE_INFO_TABLE...");

                // Pattern for TYPE_INFO_TABLE assignment
                // The table is typically assigned with: mov [rip+offset], rax
                string[] patterns = new[]
                {
                    "48 89 05 ?? ?? ?? ?? 48 8B 05", // mov [rip+??], rax; mov rax, [rip+...]
                    "48 89 05 ?? ?? ?? ?? 48 8B", // mov [rip+??], rax; mov ...
                    "48 89 0D ?? ?? ?? ?? 48 8B", // mov [rip+??], rcx; mov ...
                };

                foreach (var pattern in patterns)
                {
                    try
                    {
                        var signatureAddr = _memory.FindSignatureInModule(pattern, "GameAssembly.dll");

                        if (signatureAddr != 0)
                        {
                            DebugLogger.LogDebug($"Found TYPE_INFO_TABLE pattern at 0x{signatureAddr:X}");

                            // Read RVA from mov [rip+offset] instruction (offset +3, instruction ends at +7)
                            int rva = _memory.ReadValue<int>(signatureAddr + 3);
                            var typeInfoTablePtr = signatureAddr + 7 + (ulong)rva;

                            DebugLogger.LogDebug($"TYPE_INFO_TABLE pointer at 0x{typeInfoTablePtr:X}");

                            // Validate by trying to read the pointer
                            var tableAddr = _memory.ReadPtr(typeInfoTablePtr);
                            if (tableAddr != 0)
                            {
                                DebugLogger.LogInfo($"✓ Found TYPE_INFO_TABLE at 0x{typeInfoTablePtr:X}");
                                _typeInfoTable = typeInfoTablePtr;
                                return typeInfoTablePtr;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.LogDebug($"TYPE_INFO_TABLE pattern failed: {ex.Message}");
                    }
                }

                DebugLogger.LogDebug("Could not locate TYPE_INFO_TABLE automatically");
                return 0;
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"FindTypeInfoTable error: {ex.Message}");
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

                var assembliesArray = _il2cppDomain;
                uint assemblyIndex = 0;
                const uint MAX_ASSEMBLIES = 500;

                // Iterate through all assemblies (null-terminated array)
                while (assemblyIndex < MAX_ASSEMBLIES)
                {
                    var assemblyPtr = _memory.ReadPtr(assembliesArray + (assemblyIndex * 8));
                    if (assemblyPtr == 0) break; // End of array

                    var assembly = _memory.ReadValue<Il2CppAssembly>(assemblyPtr);
                    if (assembly.Image == 0)
                    {
                        assemblyIndex++;
                        continue;
                    }

                    var image = _memory.ReadValue<Il2CppImage>(assembly.Image);
                    if (image.Name == 0)
                    {
                        assemblyIndex++;
                        continue;
                    }

                    string imageName = _memory.ReadUtf8String(image.Name, 128);

                    // For now, linear search through types
                    // TODO: Use hash table for better performance
                    var classPtr = SearchClassesInImage(image, className, namespaceName);
                    if (classPtr != 0)
                    {
                        DebugLogger.LogDebug($"Found class in assembly '{imageName}'");
                        return classPtr;
                    }

                    assemblyIndex++;
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
