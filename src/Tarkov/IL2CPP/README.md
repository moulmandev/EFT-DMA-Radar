# IL2CPP Runtime Offset Dumper - POC

This is a proof-of-concept implementation for dynamically dumping IL2CPP offsets at runtime from Escape from Tarkov's memory. This tool helps generate and update the SDK.cs file when game updates change memory offsets.

## Overview

The dumper reads IL2CPP metadata structures directly from the game's memory (GameAssembly.dll) and extracts:
- Class definitions
- Field offsets
- Type information
- Assembly structure

All output is logged to the DebugLogger console for easy viewing and copying.

## Components

### 1. Il2CppStructures.cs
Defines the IL2CPP metadata structures:
- `Il2CppClass` - Class metadata including fields and size
- `FieldInfo` - Field name, type, and memory offset
- `Il2CppImage` - Assembly/module information
- `Il2CppDomain` - Root structure containing all assemblies
- `Il2CppType` - Type information
- `Il2CppAssembly` - Assembly metadata

### 2. Il2CppOffsetDumper.cs
The main dumper class that:
- Locates GameAssembly.dll and IL2CPP domain in memory
- Provides methods to dump individual classes or entire assemblies
- Reads class fields and their offsets
- Logs all information using DebugLogger

Key methods:
- `Initialize()` - Finds GameAssembly and IL2CPP domain
- `DumpClass(className, namespace)` - Dumps a specific class
- `DumpAssembly(assemblyName)` - Dumps assembly info
- `DumpMultipleClasses(dict)` - Batch dumps multiple classes

### 3. DumperExample.cs
Example usage demonstrating how to:
- Run a full dump of common EFT classes
- Dump only critical classes for SDK generation
- Quick test the dumper functionality

## Usage

### Basic Usage

```csharp
using LoneEftDmaRadar.Tarkov.IL2CPP;

// Make sure DebugLogger is enabled first
DebugLogger.Toggle();

// Run the example dumper
DumperExample.RunDumperExample();
```

### Dump Specific Class

```csharp
var dumper = new Il2CppOffsetDumper();

if (dumper.Initialize())
{
    dumper.DumpClass("Player", "EFT");
    dumper.DumpClass("GameWorld", "EFT");
}
```

### Batch Dump for SDK

```csharp
var dumper = new Il2CppOffsetDumper();

if (dumper.Initialize())
{
    var classes = new Dictionary<string, string>
    {
        { "Player", "EFT" },
        { "GameWorld", "EFT" },
        { "MovementContext", "EFT" }
    };

    dumper.DumpMultipleClasses(classes);
}
```

### Quick Test

```csharp
// Quick test to verify everything works
DumperExample.QuickTest();
```

## Integration with Existing Code

To integrate the dumper into your radar application:

1. **Enable Debug Console:**
   ```csharp
   DebugLogger.Toggle();
   ```

2. **Run After Game Loads:**
   The dumper should be called after the game has fully initialized and GameAssembly.dll is loaded. A good place might be in your radar initialization code:

   ```csharp
   // In your main radar initialization
   public async Task StartRadar()
   {
       // ... existing initialization code ...

       // Optionally dump offsets for verification
       #if DEBUG
       DumperExample.QuickTest();
       #endif
   }
   ```

3. **Trigger On-Demand:**
   Add a hotkey or UI button to trigger dumping:
   ```csharp
   if (userPressedDumpKey)
   {
       DumperExample.DumpCriticalClasses();
   }
   ```

## Output Format

The dumper logs information in this format:

```
=== Dumping Class: EFT.Player ===
Class: EFT.Player
Class Address: 0x7FF8A2C40000
Instance Size: 0x960
Field Count: 25

--- Fields ---
  [0] MovementContext (Offset: 0x60) - Type: class
  [1] _playerBody (Offset: 0x190) - Type: class
  [2] Physical (Offset: 0x8F0) - Type: class
  [3] Profile (Offset: 0x8D8) - Type: class
  ...
```

## Updating SDK.cs

After running the dumper:

1. Review the DebugLogger output
2. Copy the offset values (0x60, 0x190, etc.)
3. Update the corresponding constants in [SDK.cs](../SDK.cs):

```csharp
public readonly partial struct Player
{
    public const uint MovementContext = 0x60; // EFT.MovementContext
    public const uint _playerBody = 0x190; // EFT.PlayerBody
    public const uint Physical = 0x8F0; // Physical
    // ...
}
```

## Important Notes

### Signature Scanning
The current POC uses a generic signature pattern to find the IL2CPP domain. This may need to be updated for different game versions. If `Initialize()` fails, you'll need to:

1. Use a tool like x64dbg or Cheat Engine to find the current pattern
2. Update the `domainPattern` in `FindIl2CppDomain()` method
3. Or use IL2CPP export functions if they're available

### Structure Offsets
The IL2CPP structure offsets (in Il2CppStructures.cs) are based on standard IL2CPP Unity builds. If EFT uses a modified IL2CPP runtime, these offsets may need adjustment.

### Performance
The dumper reads memory synchronously and can take a moment for large assemblies. Use it only when needed (game updates, verification), not in tight game loops.

## Troubleshooting

**"Failed to find GameAssembly.dll"**
- Ensure the game is running
- Check that you're attached to the correct process

**"Failed to find IL2CPP domain"**
- The signature pattern needs updating for the current game version
- Try using IL2CPP export functions as an alternative

**"Class not found"**
- Verify the class name and namespace are correct
- The class might be in a different assembly
- Check if the class name has been obfuscated

## Future Enhancements

Possible improvements for the full version:
- Automatic SDK.cs file generation
- GUI interface for selecting classes to dump
- Export to JSON/XML format
- Differential comparison with existing SDK
- Support for obfuscated class names
- Full assembly traversal using hash tables
- Method signature dumping
- Property dumping

## References

This POC is inspired by IL2CPP dumping tools:
- Perfare/Il2CppDumper
- fedes1to/Il2CppDumperR
- AndnixSH/Auto-Il2cppDumper

## License

Same license as the parent project (MIT).
