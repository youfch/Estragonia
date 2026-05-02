# Estragonia: Avalonia in Godot

[![Avalonia + Godot](https://github.com/MrJul/Estragonia/blob/main/docs/av_plus_gd.png)](#)

[![NuGet](https://img.shields.io/nuget/v/JLeb.Estragonia)](https://www.nuget.org/packages/JLeb.Estragonia)

Estragonia is a bridge allowing the use of the powerful [Avalonia UI](https://github.com/AvaloniaUI/Avalonia) framework in the no less powerful [Godot](https://github.com/godotengine/godot) game engine!

It's GPU accelerated using Vulkan, which is the main renderer used in Godot 4.

## Quick Start

1. Have Godot 4.3.0 with .NET support installed.
2. Install the `JLeb.Estragonia` NuGet package inside your Godot C# project.
3. Initialize the Avalonia application using `UseGodot()` followed by either `SetupWithoutStarting()` (default, for embedded UI) or `SetupWithGodot()` (for `ShowDialog()` support). See the [step by step instructions](https://github.com/MrJul/Estragonia/blob/main/docs/setup.md) for details.
4. Add a Godot `Control` node to your scene, assign it a script inheriting from `JLeb.Estragonia.AvaloniaControl` and populate its `Control` property with any valid Avalonia view.

For a more detailed guide, see the [step by step instructions](https://github.com/MrJul/Estragonia/blob/main/docs/setup.md).

## Resources

For various things to know regarding compatibility, rendering and input handling, see [this document](https://github.com/MrJul/Estragonia/blob/main/docs/toknow.md).

Samples:
- [HelloWorld](https://github.com/MrJul/Estragonia/tree/main/samples/HelloWorld): a basic Avalonia-into-Godot setup.
- [GameMenu](https://github.com/MrJul/Estragonia/tree/main/samples/GameMenu): a functional game menu UI using the MVVM pattern, with controller support, UI animations and scaling.

## NativeAOT Export (Experimental)

Estragonia supports NativeAOT compilation for Godot desktop exports, enabling fully native binaries without a managed runtime. This is experimental and requires manual configuration.

### Prerequisites

- Godot 4.6+ with .NET support
- .NET 10 SDK (for `PublishAot` support)
- A project using Estragonia with the `Godot.NET.Sdk`

### Project Configuration

Add the following to your `.csproj`:

```xml
<!-- Enable NativeAOT -->
<PropertyGroup>
    <PublishAot>true</PublishAot>
</PropertyGroup>

<!-- Prevent the trimmer from removing script classes and Estragonia types.
     Godot discovers C# scripts by class name at runtime via reflection,
     which the ILC trimmer cannot trace statically. -->
<ItemGroup>
    <TrimmerRootAssembly Include="$(TargetName)" />
    <TrimmerRootAssembly Include="JLeb.Estragonia" />
    <TrimmerRootDescriptor Include="TrimmerRoots.xml" />
</ItemGroup>
```

Create a `TrimmerRoots.xml` file alongside your `.csproj`:

```xml
<linker>
  <assembly fullname="YourProjectAssembly" preserve="all" />
  <assembly fullname="JLeb.Estragonia" preserve="all" />
</linker>
```

Replace `YourProjectAssembly` with your project's assembly name (from `[dotnet] project/assembly_name` in `project.godot`).

### Fix: PrivateAssets Conflict

If your `.csproj` contains an `<ItemDefinitionGroup>` that sets `<PackageReference PrivateAssets="all" />`, you must override this for assemblies that the ILC compiler needs. Add explicit `PrivateAssets="none"` overrides for GodotSharp and any directly-referenced Avalonia packages:

```xml
<ItemGroup>
    <PackageReference Include="GodotSharp" Version="4.6.1" PrivateAssets="none" />
</ItemGroup>

<ItemDefinitionGroup>
    <PackageReference PrivateAssets="all" />
</ItemDefinitionGroup>

<ItemGroup>
    <PackageReference Include="Avalonia.Themes.Fluent" Version="12.*" PrivateAssets="none" />
    <PackageReference Include="Avalonia.HarfBuzz" Version="12.*" PrivateAssets="none" />
</ItemGroup>
```

Without these overrides, `PrivateAssets="all"` prevents the assemblies from reaching the ILC compiler, causing `Failed to load assembly 'GodotSharp'` errors during publish.

### Post-Export Step: Copy Native Libraries

Godot's desktop export places .NET outputs in a `data_<project>_<platform>_<arch>` subdirectory. However, NativeAOT-compiled code resolves native DLLs (like `libSkiaSharp.dll`, `libHarfBuzzSharp.dll`) relative to the **host executable**, not the subdirectory. After exporting, copy these files from the `data_*` folder to the executable's directory:

```powershell
# Example: copy native SkiaSharp dependencies alongside the exported exe
Copy-Item "export_dir/data_MyProject_windows_x86_64/libSkiaSharp.dll" "export_dir/"
Copy-Item "export_dir/data_MyProject_windows_x86_64/libHarfBuzzSharp.dll" "export_dir/"
```

This step is required because Godot's export pipeline does not account for NativeAOT's DLL resolution behavior.

### Known Limitations

- **GodotSharp trim warnings**: `GodotSharp` is not annotated for trimming/AOT compatibility, so expect `IL2104`/`IL3053` warnings during publish. These are benign.
- **Binary size**: With `preserve="all"` on both the project and Estragonia assemblies, trimming is effectively disabled for those assemblies, resulting in larger output.
- **Desktop only tested**: This configuration has been tested on Windows desktop exports. Other platforms may require additional adjustments.

## License

The whole Estragonia project source code is under the [MIT License](https://github.com/MrJul/Estragonia/blob/main/license.txt).  
Some specific licenses may apply to some assets used in the samples. See each sample for more information.

## Video

https://github.com/MrJul/Estragonia/assets/1623034/7bcb12e4-0f0a-41c4-8dd8-71d8c80ede0b

From the [GameMenu sample](https://github.com/MrJul/Estragonia/tree/main/samples/GameMenu)
