// Copyright, 2021, AggrobirdGK

using System;
using UnityEngine;
using System.Collections.Generic;

namespace AggroBird.DebugConsole
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
        CtrlShift = Ctrl | Shift,
        AltShift = Alt | Shift,
        CtrlAltShift = Ctrl | Alt | Shift,
    }

    [Serializable]
    public struct KeyBind
    {
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

        public override string ToString()
        {
            return $"{mod}+{code}";
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

        internal bool IsPressed(Event evt)
        {
            return bind.IsPressed(evt, state);
        }

        public override string ToString()
        {
            return $"({bind}+{state} => \"{command}\")";
        }
    }

    public static class Macros
    {
#if !NO_DEBUG_CONSOLE
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