// Copyright, 2022, AggrobirdGK

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace AggroBird.DebugConsole.Editor
{
    internal sealed class DebugConsoleInstance : EditorWindow
    {
        [MenuItem("Window/Analysis/Debug Console Instance", priority = 998)]
        public static void ShowWindow()
        {
            DebugConsoleInstance window = CreateInstance<DebugConsoleInstance>();
            window.titleContent = new GUIContent("Debug Console Instance");
            window.Show();
        }

#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE
        private ConsoleGUI gui = new ConsoleGUI(true);
#endif

        private void Awake()
        {
            minSize = new Vector2(300, 100);
        }

        private void OnInspectorUpdate()
        {
            Repaint();
        }

        private void OnGUI()
        {
#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE
            gui.UpdateGUI(position.size, DebugSettings.DefaultFontSize);
#else
            GUI.Label(new Rect(0, 0, position.width, 20), "Debug Console is disabled");
#endif
        }
    }
}
#endif