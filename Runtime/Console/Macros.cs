// Copyright, AggrobirdGK

using System;
using System.Collections.Generic;
using UnityEngine;

namespace AggroBird.ReflectionDebugConsole
{
    [Serializable]
    internal sealed class ListObject<T>
    {
        public ListObject()
        {
            list = new List<T>();
        }
        public ListObject(List<T> list)
        {
            this.list = list;
        }

        public static implicit operator List<T>(ListObject<T> listObject) => listObject.list;

        public List<T> list;
    }

    public enum KeyState
    {
        KeyDown = 0,
        KeyUp,
    }

    public enum KeyMod
    {
        None = 0,
        Ctrl = 1,
        Alt = 2,
        Shift = 4,
        CtrlAlt = Ctrl | Alt,
        CtrlShift = Ctrl | Shift,
        AltShift = Alt | Shift,
        CtrlAltShift = Ctrl | Alt | Shift,
    }

    [Serializable]
    public struct KeyBind : IEquatable<KeyBind>
    {
        internal KeyBind(Event evt)
        {
            mod = MakeKeyMod(evt);
            code = evt.keyCode;
        }
        public KeyBind(KeyMod mod, KeyCode code)
        {
            this.mod = mod;
            this.code = code;
        }

        public KeyMod mod;
        public KeyCode code;

        internal bool IsPressed(Event evt, KeyState state = KeyState.KeyDown)
        {
            if (evt.keyCode != code) return false;

            KeyState keystate = evt.type == EventType.KeyDown ? KeyState.KeyDown : KeyState.KeyUp;
            if (keystate != state) return false;

            KeyMod keymod = KeyMod.None;
            if (evt.control) keymod |= KeyMod.Ctrl;
            if (evt.alt) keymod |= KeyMod.Alt;
            if (evt.shift) keymod |= KeyMod.Shift;
            if (mod != keymod) return false;

            return true;
        }

        internal static KeyMod MakeKeyMod(Event evt)
        {
            KeyMod keymod = KeyMod.None;
            if (evt.control && evt.keyCode != KeyCode.LeftControl && evt.keyCode != KeyCode.RightControl) keymod |= KeyMod.Ctrl;
            if (evt.alt && evt.keyCode != KeyCode.LeftAlt && evt.keyCode != KeyCode.RightAlt) keymod |= KeyMod.Alt;
            if (evt.shift && evt.keyCode != KeyCode.LeftShift && evt.keyCode != KeyCode.RightShift) keymod |= KeyMod.Shift;
            return keymod;
        }

        public bool Equals(KeyBind other)
        {
            return mod == other.mod && code == other.code;
        }
        public override bool Equals(object obj)
        {
            return obj is KeyBind other && Equals(other);
        }
        public override int GetHashCode()
        {
            return (mod.GetHashCode() << 2) ^ code.GetHashCode();
        }

        public static bool operator ==(KeyBind left, KeyBind right)
        {
            return left.Equals(right);
        }
        public static bool operator !=(KeyBind left, KeyBind right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return $"{mod}+{code}";
        }
    }

    internal struct MacroKeyBind : IEquatable<MacroKeyBind>
    {
        public MacroKeyBind(KeyMod mod, KeyCode code, KeyState state)
        {
            bind = new KeyBind(mod, code);
            this.state = state;
        }
        public MacroKeyBind(KeyBind bind, KeyState state)
        {
            this.bind = bind;
            this.state = state;
        }

        public KeyBind bind;
        public KeyState state;

        public bool Equals(MacroKeyBind other)
        {
            return bind == other.bind && state == other.state;
        }
        public override bool Equals(object obj)
        {
            return obj is MacroKeyBind other && Equals(other);
        }
        public override int GetHashCode()
        {
            return (bind.GetHashCode() << 2) ^ state.GetHashCode();
        }

        public static bool operator ==(MacroKeyBind left, MacroKeyBind right)
        {
            return left.Equals(right);
        }
        public static bool operator !=(MacroKeyBind left, MacroKeyBind right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return $"{bind}+{state}";
        }
    }

    [Serializable]
    public struct Macro
    {
        public Macro(KeyMod mod, KeyCode code, KeyState state, string command)
        {
            bind = new KeyBind(mod, code);
            this.state = state;
            this.command = command;
        }

        public KeyBind bind;
        public KeyState state;
        public string command;

        public override string ToString()
        {
            return $"({bind}+{state} => \"{command}\")";
        }
    }

    public static class Macros
    {
#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE
        internal static List<Macro> localMacros = DebugConsole.LoadPrefs<ListObject<Macro>>(DebugConsole.MacrosKey);

        public static void Add(Macro macro)
        {
            localMacros.Add(macro);

            DebugConsole.ReloadMacroTable();
        }
        public static Macro Get(int index)
        {
            return localMacros[index];
        }
        public static void Set(int index, Macro macro)
        {
            localMacros[index] = macro;

            DebugConsole.ReloadMacroTable();
        }
        public static void Delete(int index)
        {
            localMacros.RemoveAt(index);

            DebugConsole.ReloadMacroTable();
        }
        public static void Save()
        {
            DebugConsole.SavePrefs(DebugConsole.MacrosKey, new ListObject<Macro>(localMacros));
        }
#else
        public static void Add(Macro macro) { }
        public static Macro Get(int index) { return default; }
        public static void Set(int index, Macro macro) { }
        public static void Delete(int index) { }
        public static void Save() { }
#endif
    }
}