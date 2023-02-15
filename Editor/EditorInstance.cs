// Copyright, AggrobirdGK

using UnityEditor;
using UnityEngine;

namespace AggroBird.ReflectionDebugConsole.Editor
{
    internal sealed class EditorInstance : EditorWindow
    {
        [MenuItem("Window/Analysis/Debug Console Instance ^#d", priority = 52)]
        public static void ShowWindow()
        {
            EditorInstance window = CreateInstance<EditorInstance>();
            window.titleContent = new GUIContent("Debug Console Instance");
            window.Show();
        }

        private readonly DebugConsoleGUI gui = new DebugConsoleGUI(true);

        private void Awake()
        {
            minSize = new Vector2(300, 100);
        }

        private void OnDestroy()
        {
            gui.Dispose();
        }

        private void OnInspectorUpdate()
        {
            Repaint();
        }

        private void OnGUI()
        {
            gui.DrawGUI(new Rect(Vector2.zero, position.size), DebugConsoleSettings.DefaultFontSize);
        }
    }
}