using System;
using System.IO;
using System.Linq;
using Mono.Cecil;

if (args.Length < 1)
{
    Console.WriteLine("Usage: PatchAvaloniaSkia <path-to-Avalonia.Skia.dll> [assembly-name-to-allow]");
    return 1;
}

var dllPath = Path.GetFullPath(args[0]);
var targetAssembly = args.Length > 1 ? args[1] : "JLeb.Estragonia";

if (!File.Exists(dllPath))
{
    Console.WriteLine("DLL not found: " + dllPath);
    return 1;
}

using (var checkAsm = AssemblyDefinition.ReadAssembly(dllPath))
{
    var alreadyPatched = checkAsm.CustomAttributes.Any(a =>
        a.AttributeType.FullName == "System.Runtime.CompilerServices.InternalsVisibleToAttribute"
        && a.ConstructorArguments.Count == 1
        && a.ConstructorArguments[0].Value is string s
        && s == targetAssembly);

    if (alreadyPatched)
    {
        Console.WriteLine("Already patched with InternalsVisibleTo(\"" + targetAssembly + "\"). Skipping.");
        return 0;
    }
}

var nugetRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
var searchDirs = new System.Collections.Generic.List<string>();
searchDirs.Add(Path.GetDirectoryName(dllPath)!);

foreach (var dir in Directory.GetDirectories(nugetRoot, "avalonia*", SearchOption.TopDirectoryOnly))
{
    var libDir = Path.Combine(dir, "12.0.0", "lib", "net8.0");
    if (Directory.Exists(libDir))
        searchDirs.Add(libDir);
}

var resolver = new DefaultAssemblyResolver();
foreach (var dir in searchDirs)
    resolver.AddSearchDirectory(dir);

var readerParams = new ReaderParameters { ReadWrite = true, AssemblyResolver = resolver };
using var asm = AssemblyDefinition.ReadAssembly(dllPath, readerParams);

var ivtCtor = typeof(System.Runtime.CompilerServices.InternalsVisibleToAttribute).GetConstructor(new[] { typeof(string) });
var customAttr = new CustomAttribute(asm.MainModule.ImportReference(ivtCtor));
customAttr.ConstructorArguments.Add(
    new CustomAttributeArgument(asm.MainModule.TypeSystem.String, targetAssembly)
);

asm.CustomAttributes.Add(customAttr);

var tmpPath = dllPath + ".tmp";
asm.Write(tmpPath);
asm.Dispose();

File.Copy(tmpPath, dllPath, overwrite: true);
File.Delete(tmpPath);

Console.WriteLine("Patched: Added InternalsVisibleTo(\"" + targetAssembly + "\") to " + Path.GetFileName(dllPath));
return 0;
