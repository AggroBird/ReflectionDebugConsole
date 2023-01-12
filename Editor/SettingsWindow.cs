// Copyright, AggrobirdGK

using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace AggroBird.DebugConsole.Editor
{
    internal sealed class SettingsWindow : EditorWindow
    {
        [MenuItem("Window/Analysis/Debug Console Settings", priority = 50)]
        public static void ShowWindow()
        {
            GetWindow<SettingsWindow>("Debug Console Settings");
        }

        private bool attemptedLoad = false;
        private Settings settings = null;
        private Vector2 scrollPos = Vector2.zero;

        [SerializeField, Tooltip("Local macros (saved to player prefs)")]
        private List<Macro> localMacros = default;


        private void OnEnable()
        {
            minSize = new Vector2(400, minSize.y);

            Undo.undoRedoPerformed += OnUndo;
        }
        private void OnDisable()
        {
            DebugConsole.SavePrefs(DebugConsole.MacrosKey, new ListObject<Macro>(localMacros));

            ReloadMacros();

            Undo.undoRedoPerformed -= OnUndo;
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
            Settings newInstance = CreateInstance<Settings>();
            newInstance.authenticationKey = CreateAuthenticationKey(32);
            AssetDatabase.CreateAsset(newInstance, resourcePath);
            AssetDatabase.ImportAsset(resourcePath);
            return resourcePath;
        }
        private static string CreateAuthenticationKey(int length)
        {
            System.Text.StringBuilder result = new System.Text.StringBuilder();
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


        private void OnGUI()
        {
            if (!settings)
            {
                if (!attemptedLoad)
                {
                    attemptedLoad = true;
                    localMacros = DebugConsole.LoadPrefs<ListObject<Macro>>(DebugConsole.MacrosKey);
                    settings = AssetDatabase.LoadAssetAtPath<Settings>(LocateSettingsFile());
                }

                if (!settings)
                {
                    EditorGUILayout.LabelField("Failed to load debug console settings");
                    return;
                }
            }

            SerializedObject settingsObj = new SerializedObject(settings);
            SerializedObject thisObj = new SerializedObject(this);

            settingsObj.UpdateIfRequiredOrScript();
            thisObj.UpdateIfRequiredOrScript();

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            SerializedProperty iterator = settingsObj.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                if (iterator.propertyPath != "m_Script")
                {
                    EditorGUILayout.PropertyField(iterator);
                }

                // Show local macros after shared macros
                if (iterator.name == "sharedMacros")
                {
                    EditorGUILayout.PropertyField(thisObj.FindProperty("localMacros"));
                }

                enterChildren = false;
            }
            EditorGUILayout.EndScrollView();

            settingsObj.ApplyModifiedProperties();
            thisObj.ApplyModifiedProperties();
        }

        private void OnValidate()
        {
            ReloadMacros();
        }
        private void OnUndo()
        {
            ReloadMacros();
            Repaint();
        }


        private void ReloadMacros()
        {
#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE
            Macros.localMacros = localMacros;

            DebugConsole.ReloadMacroTable();
#endif
        }
    }
}