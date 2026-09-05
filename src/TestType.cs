using System;
using UnityEngine;

namespace SkaterMod {
    public class TestType {
        public static void Print() {
            var type = typeof(PlayerBody);
            var field = type.GetField("HasFallen", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (field != null) Console.WriteLine("Field: " + field.FieldType.Name);
            var prop = type.GetProperty("HasFallen", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (prop != null) Console.WriteLine("Property: " + prop.PropertyType.Name);
        }
    }
}
