// Copyright, AggrobirdGK

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace AggroBird.ReflectionDebugConsole
{
    [Serializable]
    internal sealed class AssemblyTable
    {
        [SerializeField] private bool loadAllAssemblies = true;
        [SerializeField] private string[] assemblies = Array.Empty<string>();

        public Assembly[] GetEnabledAssemblies()
        {
            List<Assembly> result = new();

            HashSet<string> filter = new();
            foreach (var loadAssembly in assemblies)
            {
                filter.Add(loadAssembly);
            }

            if (loadAllAssemblies)
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    // Excluded
                    if (!filter.Contains(assembly.GetName().Name))
                    {
                        result.Add(assembly);
                    }
                }
            }
            else
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    // Included
                    if (filter.Contains(assembly.GetName().Name))
                    {
                        result.Add(assembly);
                    }
                }
            }

            return result.ToArray();
        }
    }

    internal sealed class DebugConsoleSettings : ScriptableObject
    {
        internal const int DefaultFontSize = 13;

        [Header("Keybinds")]
        [Tooltip("Keybinds for opening the console")]
        public KeyBind[] openConsoleKeys = new KeyBind[]
        {
            new(KeyMod.None, KeyCode.BackQuote),
            new(KeyMod.None, KeyCode.Tilde),
        };
        [Tooltip("Keybinds for submitting the input")]
        public KeyBind[] submitInputKeys = new KeyBind[]
        {
            new(KeyMod.None, KeyCode.Return),
            new(KeyMod.None, KeyCode.KeypadEnter),
        };

        [Tooltip("Keybind for closing the console")]
        public KeyBind closeConsoleKey = new(KeyMod.None, KeyCode.Escape);
        [Tooltip("Keybind for autocompleting the most relevant suggestion")]
        public KeyBind autoCompleteKey = new(KeyMod.None, KeyCode.Tab);

        [Tooltip("Keybind for previous command in history")]
        public KeyBind prevHistoryKey = new(KeyMod.None, KeyCode.PageUp);
        [Tooltip("Keybind for next command in history")]
        public KeyBind nextHistoryKey = new(KeyMod.None, KeyCode.PageDown);

        [Tooltip("Keybind for previous suggestion in suggestion list")]
        public KeyBind prevSuggestionKey = new(KeyMod.None, KeyCode.UpArrow);
        [Tooltip("Keybind for next suggestion in suggestion list")]
        public KeyBind nextSuggestionKey = new(KeyMod.None, KeyCode.DownArrow);

        [Header("Console Settings")]
        [Tooltip("Max entry count in command history"), Min(0)]
        public int maxHistoryCount = 100;
        [Tooltip("Max iteration count in loops"), Min(0)]
        public int maxIterationCount = 100000;
        [Tooltip("Prevent access to private members in safe mode")]
        public bool safeMode = true;
        [Tooltip("Allow the console GUI to show up in release builds (!Debug.isDebugBuild)")]
        public bool allowConsoleInRelease = false;

        [Header("Game View")]
        [Tooltip("Constant pixel size of the console font")]
        public int fontSize = DefaultFontSize;
        [Tooltip("Increase the console scale when rendering the game at a greater resolution (editor only)")]
        public bool invertScale = true;

        [Header("Debug Server")]
        [Tooltip("Automatically start a debug server on application startup")]
        public bool startDebugServer = true;
        [Tooltip("Port that the debug server listens on"), Min(0)]
        public int serverPort = 2302;
        [Tooltip("Unique key to prevent random clients from connecting")]
        public string authenticationKey = string.Empty;

        [Header("Macros")]
        [Tooltip("Shared macros (saved to settings)")]
        public List<Macro> sharedMacros = new();

        [Header("Using Namespaces")]
        [Tooltip("Autocomplete namespaces")]
        public List<string> namespaces = new();

        [Header("Assemblies")]
        [Tooltip("Assemblies to include/exclude from the scan")]
        public AssemblyTable assemblies = new();
    }
}