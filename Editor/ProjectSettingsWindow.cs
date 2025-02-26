// Copyright, AggrobirdGK

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AggroBird.ReflectionDebugConsole.Editor
{
    internal static class EditorRectUtility
    {
        public static bool IsVisible(Rect position)
        {
            return !isEditorWindow || editorWindowPosition.Overlaps(position);
        }

        public static bool isEditorWindow;
        public static Rect editorWindowPosition;
    }

    internal sealed class LocalMacroSettings : ScriptableObject
    {
        [Tooltip("Local macros (saved to player prefs)")]
        public List<Macro> localMacros = default;
    }

    internal abstract class SettingsWindow : SettingsProvider
    {
        protected SettingsWindow(string path, SettingsScope scopes, IEnumerable<string> keywords = null) : base(path, scopes, keywords)
        {

        }

        private static readonly List<string> keywordBuilder = new();
        protected static string[] GetKeywordsFromTypeFields<T>()
        {
            keywordBuilder.Clear();
            keywordBuilder.Add("Reflection");
            keywordBuilder.Add("Debug");
            keywordBuilder.Add("Console");
            foreach (var field in typeof(T).GetFields())
            {
                keywordBuilder.Add(ObjectNames.NicifyVariableName(field.Name));
            }
            return keywordBuilder.ToArray();
        }


        private PropertyInfo settingsWindowProperty;
        private Vector2 scrollPos = Vector2.zero;

        protected bool TryGetEditorWindow(out EditorWindow editorWindow)
        {
            if (settingsWindowProperty != null)
            {
                editorWindow = settingsWindowProperty.GetValue(this) as EditorWindow;
                return editorWindow != null;
            }
            editorWindow = null;
            return false;
        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            settingsWindowProperty = typeof(SettingsProvider).GetProperty("settingsWindow", BindingFlags.NonPublic | BindingFlags.Instance);
        }
        public override void OnDeactivate()
        {

        }

        protected bool DrawEditorWindow(SerializedObject settingsObject)
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            EditorRectUtility.isEditorWindow = false;

            if (settingsWindowProperty != null)
            {
                EditorWindow editorWindow = settingsWindowProperty.GetValue(this) as EditorWindow;
                if (editorWindow)
                {
                    EditorRectUtility.isEditorWindow = true;
                    Rect newPosition = new(scrollPos, editorWindow.position.size);
                    if (newPosition != EditorRectUtility.editorWindowPosition)
                    {
                        EditorRectUtility.editorWindowPosition = newPosition;
                        EditorGUI.FocusTextInControl(null);
                    }
                }
            }

            settingsObject.Update();
            SerializedProperty iterator = settingsObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                if (iterator.propertyPath != "m_Script")
                {
                    EditorGUILayout.PropertyField(iterator);
                }
                enterChildren = false;
            }
            bool changed = settingsObject.ApplyModifiedProperties();

            EditorRectUtility.isEditorWindow = false;
            EditorGUILayout.EndScrollView();

            return changed;
        }
        protected void ValidateFocus()
        {
            if (settingsWindowProperty != null)
            {
                EditorWindow editorWindow = settingsWindowProperty.GetValue(this) as EditorWindow;
                if (editorWindow)
                {
                    if (!ReferenceEquals(editorWindow, EditorWindow.focusedWindow))
                    {
                        OnFocusLost();
                    }
                }
            }
        }

        protected abstract void OnFocusLost();
    }

    internal sealed class UserPrefSettingsWindow : SettingsWindow
    {
        private const string Path = "Preferences/Reflection Debug Console";

        [MenuItem("Window/Analysis/Debug Console User Preferences", priority = 51)]
        public static void Open()
        {
            SettingsService.OpenUserPreferences(Path);
        }

        private UserPrefSettingsWindow(string path, SettingsScope scopes, IEnumerable<string> keywords = null) : base(path, scopes, keywords)
        {

        }

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            UserPrefSettingsWindow provider = new(Path, SettingsScope.User)
            {
                keywords = GetKeywordsFromTypeFields<LocalMacroSettings>()
            };
            return provider;
        }


        private LocalMacroSettings settings = null;
        private SerializedObject settingsObject = null;

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            base.OnActivate(searchContext, rootElement);
        }
        public override void OnDeactivate()
        {
            base.OnDeactivate();

            ApplyChanges();

            settingsObject = null;

            if (settings)
            {
                DebugConsole.SavePrefs(DebugConsole.MacrosKey, new ListObject<Macro>(settings.localMacros));

                Object.DestroyImmediate(settings);
                settings = null;
            }
        }

        public override void OnGUI(string searchContext)
        {
            ValidateSettings();

            if (DrawEditorWindow(settingsObject))
            {
                ApplyChanges();
            }
            ValidateFocus();
        }

        protected override void OnFocusLost()
        {
            ApplyChanges();
        }

        private void ApplyChanges()
        {
#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE
            if (settings)
            {
                Macros.localMacros = settings.localMacros;

                DebugConsole.ReloadMacroTable();
            }
#endif
        }

        private void ValidateSettings()
        {
            if (!settings)
            {
                settings = ScriptableObject.CreateInstance<LocalMacroSettings>();
                settings.localMacros = DebugConsole.LoadPrefs<ListObject<Macro>>(DebugConsole.MacrosKey);
                settingsObject = new SerializedObject(settings);
            }
        }
    }

    internal sealed class ProjectSettingsWindow : SettingsWindow
    {
        private const string Path = "Project/Reflection Debug Console";

        [MenuItem("Window/Analysis/Debug Console Project Settings", priority = 50)]
        public static void Open()
        {
            SettingsService.OpenProjectSettings(Path);
        }

        private ProjectSettingsWindow(string path, SettingsScope scopes, IEnumerable<string> keywords = null) : base(path, scopes, keywords)
        {

        }

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            ProjectSettingsWindow provider = new(Path, SettingsScope.Project)
            {
                keywords = GetKeywordsFromTypeFields<DebugConsoleSettings>()
            };
            return provider;
        }


        private DebugConsoleSettings settings = null;
        private SerializedObject settingsObject = null;
        private bool hasPendingChanges = false;

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            base.OnActivate(searchContext, rootElement);

            Undo.undoRedoPerformed += OnUndo;
        }
        public override void OnDeactivate()
        {
            base.OnDeactivate();

            ApplyChanges();

            Undo.undoRedoPerformed -= OnUndo;
        }

        public override void OnGUI(string searchContext)
        {
            if (!ValidateSettings())
            {
                EditorGUILayout.HelpBox("Failed to open console settings asset", MessageType.Warning);
                return;
            }

            hasPendingChanges |= DrawEditorWindow(settingsObject);
            ValidateFocus();
        }

        private void OnUndo()
        {
            hasPendingChanges = true;
        }

        protected override void OnFocusLost()
        {
            ApplyChanges();
        }

        private void ApplyChanges()
        {
            if (hasPendingChanges)
            {
                hasPendingChanges = false;
                DebugConsole.Reload();
            }
        }

        private string LocateSettingsFile()
        {
            string[] assets = AssetDatabase.FindAssets(DebugConsole.SettingsFileName);
            if (assets != null && assets.Length > 0)
            {
                return AssetDatabase.GUIDToAssetPath(assets[0]);
            }

            string resourceFolder = $"Assets/Resources";
            if (!Directory.Exists(resourceFolder)) Directory.CreateDirectory(resourceFolder);
            string resourcePath = $"{resourceFolder}/{DebugConsole.SettingsFileName}.asset";
            DebugConsoleSettings newInstance = ScriptableObject.CreateInstance<DebugConsoleSettings>();
            newInstance.authenticationKey = CreateAuthenticationKey(32);
            AssetDatabase.CreateAsset(newInstance, resourcePath);
            AssetDatabase.ImportAsset(resourcePath);
            return resourcePath;
        }
        private static string CreateAuthenticationKey(int length)
        {
            System.Text.StringBuilder result = new();
            byte[] bytes = new byte[length];
            using (RandomNumberGenerator random = RandomNumberGenerator.Create())
            {
                random.GetBytes(bytes);
                int totalCount = (26 + 26 + 10);
                for (int i = 0; i < bytes.Length; i++)
                {
                    int idx = bytes[i] % totalCount;
                    if (idx < 26)
                    {
                        result.Append((char)('a' + idx));
                        continue;
                    }
                    idx -= 26;
                    if (idx < 26)
                    {
                        result.Append((char)('A' + idx));
                        continue;
                    }
                    idx -= 26;
                    result.Append((char)('0' + idx));
                }
            }
            return result.ToString();
        }

        private bool ValidateSettings()
        {
            if (!settings)
            {
                settingsObject = null;
                settings = AssetDatabase.LoadAssetAtPath<DebugConsoleSettings>(LocateSettingsFile());
                if (!settings)
                {
                    return false;
                }
                settingsObject = new SerializedObject(settings);
            }

            return true;
        }
    }
}