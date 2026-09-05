using System;
using System.Linq;

namespace Check {
    class Program {
        static void Main() {
            var libs = System.IO.Directory.GetFiles("libs", "*.dll");
            foreach (var lib in libs) {
                try {
                    var asm = System.Reflection.Assembly.LoadFrom(System.IO.Path.GetFullPath(lib));
                    var type = asm.GetTypes().FirstOrDefault(t => t.Name == "PlayerBody");
                    if (type != null) {
                        Console.WriteLine("Found PlayerBody in " + lib);
                        var prop = type.GetProperty("HasFallen", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (prop != null) Console.WriteLine("HasFallen is Property: " + prop.PropertyType.Name);
                        var field = type.GetField("HasFallen", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (field != null) Console.WriteLine("HasFallen is Field: " + field.FieldType.Name);
                    }
                } catch {}
            }
        }
    }
}
