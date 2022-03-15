// Copyright, 2021, AggrobirdGK

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System;
using UnityObject = UnityEngine.Object;

namespace AggroBird.DebugConsole.Editor
{
    internal sealed class DebugSettingsWindow : EditorWindow
    {
        [MenuItem("Window/Analysis/Debug Console Settings", priority = 999)]
        public static void ShowWindow()
        {
            GetWindow<DebugSettingsWindow>("Debug Console Settings");
        }


        private bool attemptedLoad = false;
        private DebugSettings settings = null;
        [SerializeField, Tooltip("Local macros (saved to player prefs)")]
        private List<Macro> localMacros = default;
        private Vector2 scrollPos = Vector2.zero;

        internal static DebugSettingsWindow window = default;
        internal static bool isEditorWindow = false;
        internal static Rect windowPosition = default;

        internal static UnityObject currentObject = default;
        internal static string currentProperty = default;
        internal static int currentPropertyIndex = 0;


        private void Awake()
        {
            minSize = new Vector2(400, minSize.y);
        }
        private void OnDestroy()
        {
            DebugConsole.SavePrefs(DebugConsole.MacrosKey, new ListObject<Macro>(localMacros));

            OnMacrosChanged();
        }

        private void OnValidate()
        {
            Undo.undoRedoPerformed -= OnUndo;
            Undo.undoRedoPerformed += OnUndo;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
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
            AssetDatabase.CreateAsset(CreateInstance<DebugSettings>(), resourcePath);
            AssetDatabase.ImportAsset(resourcePath);
            return resourcePath;
        }

        private void OnGUI()
        {
            if (!settings)
            {
                if (!attemptedLoad)
                {
                    attemptedLoad = true;
                    localMacros = DebugConsole.LoadPrefs<ListObject<Macro>>(DebugConsole.MacrosKey);
                    settings = AssetDatabase.LoadAssetAtPath<DebugSettings>(LocateSettingsFile());
                }

                if (!settings)
                {
                    EditorGUILayout.LabelField("Failed to load debug console settings");
                    return;
                }
            }

            window = this;
            isEditorWindow = true;

            try
            {
                Rect newPosition = position;
                newPosition.position = scrollPos;
                if (newPosition != windowPosition && (Event.current.type == EventType.Repaint || Event.current.type == EventType.Layout))
                {
                    EditorGUI.FocusTextInControl(null);
                    windowPosition = newPosition;
                }

                SerializedObject settingsObj = new SerializedObject(settings);
                SerializedObject thisObj = new SerializedObject(this);
                settingsObj.Update();
                thisObj.Update();
                scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
                {
                    EditorGUILayout.BeginVertical(GUILayout.MaxWidth(400));
                    {
                        SerializedProperty closeConsoleKey = settingsObj.FindProperty("closeConsoleKey");
                        EditorGUILayout.LabelField("Keybinds", EditorStyles.boldLabel);
                        ClampedSizeArray(settingsObj.FindProperty("openConsoleKeys"), 4);
                        EditorGUILayout.Space();
                        ClampedSizeArray(settingsObj.FindProperty("submitInputKeys"), 4);
                        EditorGUILayout.Space();
                        EditorGUILayout.PropertyField(settingsObj.FindProperty("closeConsoleKey"));
                        EditorGUILayout.PropertyField(settingsObj.FindProperty("autoCompleteKey"));
                        EditorGUILayout.Space();
                        EditorGUILayout.PropertyField(settingsObj.FindProperty("prevHistoryKey"));
                        EditorGUILayout.PropertyField(settingsObj.FindProperty("nextHistoryKey"));
                        EditorGUILayout.Space();
                        EditorGUILayout.PropertyField(settingsObj.FindProperty("prevSuggestionKey"));
                        EditorGUILayout.PropertyField(settingsObj.FindProperty("nextSuggestionKey"));

                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
                        SerializedProperty maxHistoryCount = settingsObj.FindProperty("maxHistoryCount");
                        EditorGUILayout.PropertyField(maxHistoryCount);
                        maxHistoryCount.intValue = Mathf.Clamp(maxHistoryCount.intValue, 0, 100);
                        EditorGUILayout.PropertyField(settingsObj.FindProperty("safeMode"));

                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("Game View", EditorStyles.boldLabel);
                        SerializedProperty fontSize = settingsObj.FindProperty("fontSize");
                        EditorGUILayout.PropertyField(fontSize);
                        if (fontSize.intValue < 1) fontSize.intValue = 1;
                        EditorGUILayout.PropertyField(settingsObj.FindProperty("invertScale"));
                    }
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Local Macros", EditorStyles.boldLabel);
                    EditorGUI.BeginChangeCheck();
                    currentObject = this;
                    currentProperty = "localMacros";
                    currentPropertyIndex = 0;
                    EditorGUILayout.PropertyField(thisObj.FindProperty(currentProperty));
                    if (EditorGUI.EndChangeCheck()) OnMacrosChanged();

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Shared Macros", EditorStyles.boldLabel);
                    currentObject = settings;
                    currentProperty = "sharedMacros";
                    currentPropertyIndex = 0;
                    EditorGUILayout.PropertyField(settingsObj.FindProperty(currentProperty));

                    EditorGUILayout.BeginVertical(GUILayout.MaxWidth(400));
                    {
                        EditorGUI.BeginChangeCheck();
                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("Using Namespaces", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(settingsObj.FindProperty("namespaces"));
                        if (EditorGUI.EndChangeCheck()) OnNamespacesChanged();
                    }
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.Space();
                    DrawAssemblyList(settingsObj);
                }
                EditorGUILayout.EndScrollView();
                settingsObj.ApplyModifiedProperties();
                thisObj.ApplyModifiedProperties();
            }
            catch (Exception)
            {

            }

            isEditorWindow = false;
        }
        private static void ClampedSizeArray(SerializedProperty property, int max)
        {
            EditorGUILayout.PropertyField(property, true);
            property.arraySize = Mathf.Clamp(property.arraySize, 0, max);
        }

        private void DrawAssemblyList(SerializedObject settingsObj)
        {
            EditorGUILayout.LabelField("Load Assemblies", EditorStyles.boldLabel);
            SerializedProperty assemblies = settingsObj.FindProperty("assemblies");
            assemblies.isExpanded = EditorGUILayout.Foldout(assemblies.isExpanded, "Assemblies");
            if (!assemblies.isExpanded) return;

            bool guiEnabled = GUI.enabled;
            bool isCompiling = EditorApplication.isCompiling;
            GUI.enabled = !isCompiling;

            EditorGUI.indentLevel++;
            EditorGUI.BeginChangeCheck();
            SerializedProperty loadAllAssemblies = settingsObj.FindProperty("loadAllAssemblies");
            bool loadAll = loadAllAssemblies.boolValue;
            EditorGUILayout.PropertyField(loadAllAssemblies);
            bool loadAllChanged = loadAllAssemblies.boolValue != loadAll;

            List<LoadAssembly> options = new List<LoadAssembly>();
            Dictionary<string, LoadAssembly> uniqueAssemblies = new Dictionary<string, LoadAssembly>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name;
                if (string.IsNullOrEmpty(name)) continue;
                if (uniqueAssemblies.ContainsKey(name)) continue;
                LoadAssembly option = new LoadAssembly(name);
                uniqueAssemblies.Add(name, option);
                options.Add(option);
            }

            for (int i = 0; i < assemblies.arraySize; i++)
            {
                SerializedProperty assembly = assemblies.GetArrayElementAtIndex(i);
                string name = assembly.FindPropertyRelative("name").stringValue;
                bool enabled = assembly.FindPropertyRelative("enabled").boolValue;
                if (uniqueAssemblies.TryGetValue(name, out LoadAssembly option))
                {
                    option.enabled = enabled;
                }
                else
                {
                    LoadAssembly newOption = new LoadAssembly(name);
                    newOption.enabled = enabled;
                    uniqueAssemblies.Add(name, newOption);
                    options.Add(newOption);
                }
            }

            if (loadAllChanged)
            {
                foreach (var option in options)
                    option.enabled = !option.enabled;
            }

            options.Sort((lhs, rhs) =>
            {
                return lhs.name.CompareTo(rhs.name);
            });

            EditorGUILayout.LabelField(loadAll ? "Exclude Assemblies" : "Include Assemblies", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(20);
            bool changed = false;
            if (GUILayout.Button("Select All", GUILayout.Width(80)))
            {
                for (int i = 0; i < options.Count; i++)
                    options[i].enabled = true;
                changed = true;
            }
            if (GUILayout.Button("Deselect All", GUILayout.Width(80)))
            {
                for (int i = 0; i < options.Count; i++)
                    options[i].enabled = false;
                changed = true;
            }

            EditorGUILayout.EndHorizontal();
            for (int i = 0; i < options.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                {
                    options[i].enabled = EditorGUILayout.Toggle(options[i].enabled, GUILayout.Width(30));
                    EditorGUILayout.LabelField(options[i].name);
                }
                EditorGUILayout.EndHorizontal();
            }

            changed |= EditorGUI.EndChangeCheck();
            if (changed && !isCompiling)
            {
                assemblies.arraySize = 0;
                for (int i = 0, j = 0; i < options.Count; i++)
                {
                    var assembly = options[i];
                    if (assembly.enabled)
                    {
                        int idx = j++;
                        assemblies.InsertArrayElementAtIndex(idx);
                        var listItem = assemblies.GetArrayElementAtIndex(idx);
                        listItem.FindPropertyRelative("name").stringValue = assembly.name;
                        listItem.FindPropertyRelative("enabled").boolValue = assembly.enabled;
                    }
                }

                OnAssembliesChanged();
            }

            EditorGUI.indentLevel--;
            GUI.enabled = guiEnabled;
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                OnMacrosChanged();
            }
        }
        private void OnUndo()
        {
            OnMacrosChanged();
            OnAssembliesChanged();
            Repaint();
            ClearSelection();
        }
        private void OnMacrosChanged()
        {
#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE
            Macros.localMacros = localMacros;

            DebugConsole.ReloadMacroTable();
#endif
        }
        private void OnNamespacesChanged()
        {
#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE
            DebugConsole.ReloadNamespaces();
#endif
        }
        private void OnAssembliesChanged()
        {
#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE
            DebugConsole.ReloadAssemblies();
#endif
        }

        internal void ClearSelection()
        {
            windowPosition = new Rect(0, 0, 0, 0);
        }
    }
}
#endif