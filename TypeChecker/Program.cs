using System;
using System.Reflection;
using System.Linq;

class Program {
    static void Main() {
        try {
            var refAsm = Assembly.LoadFrom(@"C:\Users\29189\.nuget\packages\avalonia\12.0.0\ref\net8.0\Avalonia.dll");
            var allTypes = refAsm.GetTypes();
            
            Console.WriteLine("=== Types with WindowDecoration ===");
            foreach (var t in allTypes.Where(t => t.Name.Contains("WindowDecoration") || t.Name.Contains("PlatformRequested"))) {
                Console.WriteLine(t.FullName);
                if (t.IsEnum) foreach (var n in Enum.GetNames(t)) Console.WriteLine("  " + n);
                else foreach (var m in t.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)) Console.WriteLine("  " + m.ToString());
            }
            
            Console.WriteLine("\n=== ITopLevelImpl ===");
            var itli = allTypes.FirstOrDefault(t => t.Name == "ITopLevelImpl");
            if (itli != null) foreach (var m in itli.GetMembers()) Console.WriteLine("  " + m.ToString());
            
            Console.WriteLine("\n=== IPlatformRenderSurface ===");
            var iprs = allTypes.FirstOrDefault(t => t.Name == "IPlatformRenderSurface");
            if (iprs != null) { Console.WriteLine(iprs.FullName); foreach (var m in iprs.GetMembers()) Console.WriteLine("  " + m.ToString()); }
            
            Console.WriteLine("\n=== IRenderTimer ===");
            var irt = allTypes.FirstOrDefault(t => t.Name == "IRenderTimer");
            if (irt != null) foreach (var m in irt.GetMembers()) Console.WriteLine("  " + m.ToString());
            
            Console.WriteLine("\n=== IClipboard ===");
            var ic = allTypes.FirstOrDefault(t => t.Name == "IClipboard");
            if (ic != null) foreach (var m in ic.GetMembers()) Console.WriteLine("  " + m.ToString());
            
            Console.WriteLine("\n=== ICursorFactory ===");
            var icf = allTypes.FirstOrDefault(t => t.Name == "ICursorFactory");
            if (icf != null) { Console.WriteLine(icf.FullName); foreach (var m in icf.GetMembers()) Console.WriteLine("  " + m.ToString()); }
            
            Console.WriteLine("\n=== IWindowImpl ===");
            var iwi = allTypes.FirstOrDefault(t => t.Name == "IWindowImpl");
            if (iwi != null) foreach (var m in iwi.GetMembers()) Console.WriteLine("  " + m.ToString());
            
            Console.WriteLine("\n=== IWindowingPlatform ===");
            var iwp = allTypes.FirstOrDefault(t => t.Name == "IWindowingPlatform");
            if (iwp != null) foreach (var m in iwp.GetMembers()) Console.WriteLine("  " + m.ToString());
            
            Console.WriteLine("\n=== RenderTargetSceneInfo ===");
            var rtsi = allTypes.FirstOrDefault(t => t.Name == "RenderTargetSceneInfo");
            if (rtsi != null) { Console.WriteLine(rtsi.FullName); foreach (var m in rtsi.GetMembers()) Console.WriteLine("  " + m.ToString()); }
            
            Console.WriteLine("\n=== Bitmap in Avalonia ===");
            foreach (var t in allTypes.Where(t => t.Name == "Bitmap")) Console.WriteLine("  " + t.FullName);
            
            Console.WriteLine("\n=== IDataObject ===");
            foreach (var t in allTypes.Where(t => t.Name == "IDataObject")) Console.WriteLine("  " + t.FullName);
            
            Console.WriteLine("\n=== ExtendClientArea ===");
            foreach (var t in allTypes.Where(t => t.Name.Contains("ExtendClient"))) Console.WriteLine("  " + t.FullName);
            
            Console.WriteLine("\n=== SystemDecorations ===");
            foreach (var t in allTypes.Where(t => t.Name.Contains("SystemDecor"))) Console.WriteLine("  " + t.FullName);
        } catch (Exception ex) {
            Console.WriteLine("Error: " + ex.Message);
            Console.WriteLine(ex.StackTrace);
        }
    }
}
