// Copyright, 2021, AggrobirdGK

#if !NO_DEBUG_CONSOLE
using System;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace AggroBird.DebugConsole
{
    internal static class Environment
    {
        // Buildin types
        public static Type @bool => typeof(bool);
        public static Type @char => typeof(char);
        public static Type @void => typeof(void);
        public static Type @byte => typeof(byte);
        public static Type @sbyte => typeof(sbyte);
        public static Type @short => typeof(short);
        public static Type @ushort => typeof(ushort);
        public static Type @int => typeof(int);
        public static Type @uint => typeof(uint);
        public static Type @long => typeof(long);
        public static Type @ulong => typeof(ulong);
        public static Type @float => typeof(float);
        public static Type @double => typeof(double);
        public static Type @decimal => typeof(decimal);
        public static Type @string => typeof(string);
        public static Type @object => typeof(object);
        // Buildin keywords
        public static bool @true => true;
        public static bool @false => false;
        public static object @null => null;


        // Typecasting
        // Type casting utility

        // Returns the type of a specified object (as type object)
        public static Type @typeof(Type type)
        {
            return type;
        }
        // Cast specified object to type, return null on failure
        [GenericTypeMethod(1)]
        public static object @cast(object obj, Type type)
        {
            if (type == null) throw new NullReferenceException();
            if (obj == null && !type.IsValueType) return null;

            if (ReflectionUtility.TryConvert(type, obj, out object converted))
            {
                return converted;
            }

            return null;
        }


        // Macro functions
        // Utility for saving and modifying macros in standalone builds for testing

        // Add a macro to the local macro set
        public static void @addmacro(KeyMod mod, KeyCode code, KeyState state, string command)
        {
            Macros.Add(new Macro(mod, code, state, command));
        }
        // Get a macro from the local macro set
        public static Macro @getmacro(int index)
        {
            return Macros.Get(index);
        }
        // Overwrite a macro in the local macro set
        public static void @setmacro(int index, Macro macro)
        {
            Macros.Set(index, macro);
        }
        // Remove a macro from the local macro set
        public static void @deletemacro(int index)
        {
            Macros.Delete(index);
        }
        // Save macros to prefs
        public static void @savemacros()
        {
            Macros.Save();
        }


        // Unity Utility
        // Unity helper functions

        // Find unity object of type (type must inherit from UnityEngine.Object)
        [GenericTypeMethod(0)]
        public static UnityObject @find(Type type)
        {
            return UnityObject.FindObjectOfType(type);
        }
        // Get component on game object (type must inherit from UnityEngine.Component)
        [GenericTypeMethod(1)]
        public static Component @getcomponent(GameObject obj, Type type)
        {
            return obj.GetComponent(type);
        }
        // Get component on component (type must inherit from UnityEngine.Component)
        [GenericTypeMethod(1)]
        public static Component @getcomponent(Component comp, Type type)
        {
            return comp.GetComponent(type);
        }


        // Debugging utility
        // Printing/logging helper functions

        // Print to console
        public static void @print(object msg)
        {
            Debug.Log(msg);
        }
        // Dump all declared variables in a type (excluding properties)
        public static string @dump(object obj)
        {
            if (obj == null)
            {
                return "Null";
            }

            StringBuilder result = new StringBuilder();
            string mod;
            BindingFlags flags;
            if (obj is Type type)
            {
                flags = ReflectionUtility.PrivateStaticFlags;
                mod = "static ";
            }
            else
            {
                type = obj.GetType();
                flags = ReflectionUtility.PrivateInstanceFlags;
                mod = string.Empty;
            }

            string typeName = type.IsEnum ? "enum" : type.IsClass ? "class" : "struct";
            result.Append($"{typeName} {type}\n{{\n");
            foreach (FieldInfo field in ReflectionUtility.GetFieldsRecursive(type, flags))
            {
                result.Append($"    {mod}{ReflectionUtility.GetTypeName(field.FieldType)} {field.Name} = {field.GetValue(obj)};\n");
            }
            result.Append("}");
            return result.ToString();
        }


        // Console utility
        // Console helper functions

        // Scale console text (default is 1)
        public static void @scale(float scale)
        {
            DebugConsole.scale = scale;
        }
        // Reload the console
        public static void @reload()
        {
            DebugConsole.Reload();
        }
    }
}
#endif