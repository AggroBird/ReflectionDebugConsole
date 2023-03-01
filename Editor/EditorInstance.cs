// Copyright, AggrobirdGK

using UnityEditor;
using UnityEngine;

namespace AggroBird.ReflectionDebugConsole.Editor
{
    internal sealed class EditorInstance : EditorWindow
    {
        private static EditorInstance FindCurrentWindow()
        {
            EditorInstance[] windows = (EditorInstance[])Resources.FindObjectsOfTypeAll(typeof(EditorInstance));
            return windows.Length > 0 ? windows[0] : null;
        }

        [MenuItem("Window/Analysis/Debug Console Instance %#d", priority = 52)]
        public static void ShowWindow()
        {
            EditorInstance window = FindCurrentWindow();
            if (!window)
            {
                window = CreateInstance<EditorInstance>();
                window.titleContent = new GUIContent("Debug Console Instance");
                window.Show();
            }
            else
            {
                window.Focus();
            }
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