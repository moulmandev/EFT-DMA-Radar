using System.Runtime.InteropServices;

namespace LoneEftDmaRadar.Tarkov.IL2CPP
{
    /// <summary>
    /// IL2CPP metadata structures for runtime dumping
    /// </summary>
    public static class Il2CppStructures
    {
        [StructLayout(LayoutKind.Explicit)]
        public struct Il2CppImage
        {
            [FieldOffset(0x0)]
            public ulong Name; // const char*

            [FieldOffset(0x8)]
            public int AssemblyIndex;

            [FieldOffset(0xC)]
            public int TypeStart;

            [FieldOffset(0x10)]
            public uint TypeCount;

            [FieldOffset(0x18)]
            public int NameToClassHashTable; // Il2CppNameToTypeDefinitionIndexHashTable*
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct Il2CppClass
        {
            [FieldOffset(0x0)]
            public ulong Image; // Il2CppImage*

            [FieldOffset(0x8)]
            public ulong GCDesc; // void*

            [FieldOffset(0x10)]
            public ulong Name; // const char*

            [FieldOffset(0x18)]
            public ulong Namespace; // const char*

            [FieldOffset(0x48)]
            public ulong Fields; // FieldInfo*

            [FieldOffset(0x80)]
            public ulong Parent; // Il2CppClass*

            [FieldOffset(0xC8)]
            public uint InstanceSize;

            [FieldOffset(0x11C)]
            public ushort FieldCount;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct FieldInfo
        {
            [FieldOffset(0x0)]
            public ulong Name; // const char*

            [FieldOffset(0x8)]
            public ulong Type; // Il2CppType*

            [FieldOffset(0x10)]
            public ulong Parent; // Il2CppClass*

            [FieldOffset(0x18)]
            public int Offset; // Memory offset of the field
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct Il2CppType
        {
            [FieldOffset(0x0)]
            public ulong Data; // void*

            [FieldOffset(0x8)]
            public ushort Attrs; // unsigned int : 16

            [FieldOffset(0xA)]
            public byte Type; // Il2CppTypeEnum

            [FieldOffset(0xB)]
            public byte Byref; // unsigned int : 1
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct Il2CppAssembly
        {
            [FieldOffset(0x0)]
            public ulong Image; // Il2CppImage*

            [FieldOffset(0x8)]
            public uint Token;

            [FieldOffset(0xC)]
            public int ReferencedAssemblyStart;

            [FieldOffset(0x10)]
            public int ReferencedAssemblyCount;

            [FieldOffset(0x14)]
            public ulong AName; // Il2CppAssemblyName*
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct Il2CppAssemblyName
        {
            [FieldOffset(0x0)]
            public ulong Name; // const char*

            [FieldOffset(0x8)]
            public ulong Culture; // const char*

            [FieldOffset(0x10)]
            public ulong PublicKey; // const char*

            [FieldOffset(0x18)]
            public uint Hash_alg;

            [FieldOffset(0x1C)]
            public int Hash_len;

            [FieldOffset(0x20)]
            public uint Flags;

            [FieldOffset(0x24)]
            public int Major;

            [FieldOffset(0x28)]
            public int Minor;

            [FieldOffset(0x2C)]
            public int Build;

            [FieldOffset(0x30)]
            public int Revision;

            [FieldOffset(0x34)]
            public ulong PublicKeyToken; // 8-byte token
        }

        /// <summary>
        /// IL2CPP Domain structure
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        public struct Il2CppDomain
        {
            [FieldOffset(0x0)]
            public ulong Assemblies; // Il2CppAssembly**

            [FieldOffset(0x8)]
            public uint AssemblyCount;
        }

        /// <summary>
        /// Il2CppMetadataRegistration - contains global metadata arrays
        /// This structure varies between Unity/IL2CPP versions
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        public struct Il2CppMetadataRegistration
        {
            [FieldOffset(0x0)]
            public ulong GenericClasses; // Il2CppGenericClass**

            [FieldOffset(0x8)]
            public int GenericClassesCount;

            [FieldOffset(0x10)]
            public ulong GenericInsts; // Il2CppGenericInst**

            [FieldOffset(0x18)]
            public int GenericInstsCount;

            [FieldOffset(0x20)]
            public ulong GenericMethodTable; // Il2CppGenericMethodFunctionsDefinitions*

            [FieldOffset(0x28)]
            public int GenericMethodTableCount;

            [FieldOffset(0x30)]
            public ulong Types; // Il2CppType**

            [FieldOffset(0x38)]
            public int TypesCount;

            [FieldOffset(0x40)]
            public ulong MethodSpecs; // Il2CppMethodSpec*

            [FieldOffset(0x48)]
            public int MethodSpecsCount;
        }

        /// <summary>
        /// Il2CppCodeRegistration - contains code-related metadata
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        public struct Il2CppCodeRegistration
        {
            [FieldOffset(0x0)]
            public ulong MethodPointers; // void**

            [FieldOffset(0x8)]
            public int MethodPointersCount;

            [FieldOffset(0x10)]
            public ulong ReversePInvokeWrappers; // Il2CppMethodPointer*

            [FieldOffset(0x18)]
            public int ReversePInvokeWrappersCount;

            [FieldOffset(0x20)]
            public ulong GenericMethodPointers; // Il2CppMethodPointer*

            [FieldOffset(0x28)]
            public int GenericMethodPointersCount;

            [FieldOffset(0x30)]
            public ulong InvokerPointers; // InvokerMethod*

            [FieldOffset(0x38)]
            public int InvokerPointersCount;
        }
    }
}
