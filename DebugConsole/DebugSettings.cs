// Copyright, 2021, AggrobirdGK

using System.Collections.Generic;
using UnityEngine;

namespace AggroBird.DebugConsole
{
    [System.Serializable]
    internal class LoadAssembly
    {
        public LoadAssembly(string name)
        {
            this.name = name;
            enabled = false;
        }

        public string name;
        public bool enabled;
    }

    internal sealed class DebugSettings : ScriptableObject
    {
        internal const int DefaultFontSize = 13;

        [Tooltip("Keybinds for opening the console")]
        public KeyBind[] openConsoleKeys = new KeyBind[]
        {
            new KeyBind(KeyMod.None, KeyCode.BackQuote),
            new KeyBind(KeyMod.None, KeyCode.Tilde),
        };
        [Tooltip("Keybinds for submitting the input")]
        public KeyBind[] submitInputKeys = new KeyBind[]
        {
            new KeyBind(KeyMod.None, KeyCode.Return),
            new KeyBind(KeyMod.None, KeyCode.KeypadEnter),
        };

        [Tooltip("Keybind for closing the console")]
        public KeyBind closeConsoleKey = new KeyBind(KeyMod.None, KeyCode.Escape);
        [Tooltip("Keybind for autocompleting the most relevant suggestion")]
        public KeyBind autoCompleteKey = new KeyBind(KeyMod.None, KeyCode.Tab);

        [Tooltip("Keybind for previous command in history")]
        public KeyBind prevHistoryKey = new KeyBind(KeyMod.None, KeyCode.PageUp);
        [Tooltip("Keybind for next command in history")]
        public KeyBind nextHistoryKey = new KeyBind(KeyMod.None, KeyCode.PageDown);

        [Tooltip("Keybind for previous suggestion in suggestion list")]
        public KeyBind prevSuggestionKey = new KeyBind(KeyMod.None, KeyCode.UpArrow);
        [Tooltip("Keybind for next suggestion in suggestion list")]
        public KeyBind nextSuggestionKey = new KeyBind(KeyMod.None, KeyCode.DownArrow);


        [Tooltip("Constant pixel size of the console font")]
        public int fontSize = DefaultFontSize;
        [Tooltip("Increase the console scale when rendering the game at a greater resolution (editor only)")]
        public bool invertScale = true;

        [Tooltip("Max entry count in command history")]
        public int maxHistoryCount = 100;
        [Tooltip("Prevent access to private members in safe mode")]
        public bool safeMode = true;

        [Tooltip("Shared macros (saved to settings)")]
        public List<Macro> sharedMacros = new List<Macro>();
        [Tooltip("Autocomplete namespaces")]
        public List<string> namespaces = new List<string>();

        public bool loadAllAssemblies = true;
        public List<LoadAssembly> assemblies = new List<LoadAssembly>();
    }
}