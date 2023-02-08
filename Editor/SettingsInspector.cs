// Copyright, AggrobirdGK

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AggroBird.ReflectionDebugConsole.Editor
{
    [CustomPropertyDrawer(typeof(KeyBind))]
    internal sealed class KeyBindPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            position = EditorGUI.PrefixLabel(position, label);

            float width = position.width;
            float size = Mathf.Min(90, position.width / 2);
            position.width = size - 1;

            EditorGUI.PropertyField(position, property.FindPropertyRelative("mod"), GUIContent.none);

            position.x += position.width + 2;
            position.width = width - (size + 1);

            EditorGUI.PropertyField(position, property.FindPropertyRelative("code"), GUIContent.none);

            EditorGUI.EndProperty();
        }
    }

    [CustomPropertyDrawer(typeof(Macro))]
    internal sealed class MacroPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (EditorRectUtility.IsVisible(position))
            {
                EditorGUI.BeginProperty(position, label, property);

                EditorGUI.PrefixLabel(position, label);
                position.x += 90;
                position.width -= 90;

                float textFieldPosition = position.width;

                SerializedProperty bind = property.FindPropertyRelative("bind");

                position.width = Mathf.Min(90, position.width / 4);
                float horizontalStep = position.width + 2;
                EditorGUI.PropertyField(position, bind.FindPropertyRelative("mod"), GUIContent.none);
                position.x += horizontalStep;
                textFieldPosition -= horizontalStep;

                EditorGUI.PropertyField(position, bind.FindPropertyRelative("code"), GUIContent.none);
                position.x += horizontalStep;
                textFieldPosition -= horizontalStep;

                EditorGUI.PropertyField(position, property.FindPropertyRelative("state"), GUIContent.none);
                position.x += horizontalStep;
                textFieldPosition -= horizontalStep;

                position.y -= 1;
                position.height -= 1;

                position.width = textFieldPosition;
                EditorGUI.PropertyField(position, property.FindPropertyRelative("command"), GUIContent.none);

                EditorGUI.EndProperty();
            }
        }
    }

    [CustomPropertyDrawer(typeof(AssemblyTable))]
    internal sealed class AssemblyTableDrawer : PropertyDrawer
    {
        private class ListItem
        {
            public ListItem(Assembly assembly, bool isSelected)
            {
                this.assembly = assembly;
                this.isSelected = isSelected;
            }

            public Assembly assembly;
            public bool isSelected;
        }

        private string filter = string.Empty;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            float verticalStep = base.GetPropertyHeight(property, label);

            position.height = verticalStep;
            if (EditorGUI.PropertyField(position, property, label, false))
            {
                using (new EditorGUI.DisabledGroupScope(EditorApplication.isCompiling || EditorApplication.isUpdating))
                {
                    EditorGUI.indentLevel++;
                    position = EditorGUI.IndentedRect(position);
                    EditorGUI.indentLevel--;

                    position.y += verticalStep;

                    SerializedProperty loadAllAssemblies = property.FindPropertyRelative("loadAllAssemblies");
                    SerializedProperty assemblies = property.FindPropertyRelative("assemblies");

                    bool saveRequired = false;

                    // Show load all toggle
                    EditorGUI.BeginChangeCheck();
                    EditorGUI.PropertyField(position, loadAllAssemblies);
                    bool reverseFilter = EditorGUI.EndChangeCheck();
                    position.y += verticalStep * 1.5f;

                    // Show label
                    EditorGUI.LabelField(position, loadAllAssemblies.boolValue ? "Exclude Assemblies" : "Include Assemblies", EditorStyles.boldLabel);
                    position.y += verticalStep;

                    // Get current selection
                    HashSet<string> currentSelection = new HashSet<string>();
                    for (int i = 0; i < assemblies.arraySize; i++)
                    {
                        currentSelection.Add(assemblies.GetArrayElementAtIndex(i).stringValue);
                    }

                    // Get all currently loaded assemblies
                    SortedDictionary<string, ListItem> uniqueAssemblies = new SortedDictionary<string, ListItem>();
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        string assemblyName = assembly.GetName().Name;
                        if (!uniqueAssemblies.ContainsKey(assemblyName))
                        {
                            bool isSelected = currentSelection.Contains(assemblyName);

                            if (reverseFilter)
                            {
                                if (isSelected)
                                    currentSelection.Remove(assemblyName);
                                else
                                    currentSelection.Add(assemblyName);

                                isSelected = !isSelected;
                                saveRequired = true;
                            }

                            uniqueAssemblies.Add(assemblyName, new ListItem(assembly, isSelected));
                        }
                    }

                    // Show filter
                    filter = EditorGUI.TextField(position, "Filter", filter);
                    string[] filterWords = Array.Empty<string>();
                    if (!string.IsNullOrEmpty(filter))
                    {
                        filterWords = filter.ToLower().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    }
                    bool hasFilter = filterWords.Length > 0;
                    position.y += verticalStep;

                    // Apply filter
                    List<ListItem> filteredSelection = new List<ListItem>();
                    int selectedCount = 0;
                    foreach (var pair in uniqueAssemblies)
                    {
                        string assemblyName = pair.Key;
                        if (hasFilter)
                        {
                            bool filterMatch = true;
                            foreach (var word in filterWords)
                            {
                                if (assemblyName.IndexOf(word, StringComparison.OrdinalIgnoreCase) == -1)
                                {
                                    filterMatch = false;
                                    break;
                                }
                            }
                            if (!filterMatch) continue;
                        }
                        filteredSelection.Add(pair.Value);
                        if (pair.Value.isSelected) selectedCount++;
                    }

                    // Show select/deselect all
                    EditorGUI.showMixedValue = selectedCount > 0 && selectedCount < filteredSelection.Count;
                    bool allSelected = selectedCount == filteredSelection.Count && filteredSelection.Count > 0;
                    Rect horizontal = position;
                    EditorGUI.BeginChangeCheck();
                    EditorGUI.Toggle(horizontal, allSelected);
                    bool reverseSelection = EditorGUI.EndChangeCheck();
                    EditorGUI.showMixedValue = false;
                    horizontal.x += 20;
                    GUI.Label(horizontal, "All Assemblies", EditorStyles.boldLabel);
                    position.y += position.height;

                    // Show list of assemblies
                    foreach (var item in filteredSelection)
                    {
                        string assemblyName = item.assembly.GetName().Name;

                        if (reverseSelection)
                        {
                            if (!allSelected && !item.isSelected)
                            {
                                currentSelection.Add(assemblyName);
                                item.isSelected = true;
                                saveRequired = true;
                            }
                            if (allSelected && item.isSelected)
                            {
                                currentSelection.Remove(assemblyName);
                                item.isSelected = false;
                                saveRequired = true;
                            }
                        }

                        if (EditorRectUtility.IsVisible(position))
                        {
                            horizontal = position;
                            EditorGUI.BeginChangeCheck();
                            EditorGUI.Toggle(horizontal, GUIContent.none, item.isSelected);
                            if (EditorGUI.EndChangeCheck())
                            {
                                if (item.isSelected)
                                    currentSelection.Remove(assemblyName);
                                else
                                    currentSelection.Add(assemblyName);

                                saveRequired = true;
                                item.isSelected = !item.isSelected;
                            }
                            horizontal.x += 20;
                            GUI.Label(horizontal, assemblyName);
                        }
                        position.y += verticalStep;
                    }

                    // Save changes
                    if (saveRequired)
                    {
                        assemblies.arraySize = currentSelection.Count;
                        int idx = 0;
                        foreach (var assembly in currentSelection)
                        {
                            assemblies.GetArrayElementAtIndex(idx++).stringValue = assembly;
                        }
                    }
                }
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = base.GetPropertyHeight(property, label);
            if (property.isExpanded)
            {
                return height * (AppDomain.CurrentDomain.GetAssemblies().Length + 4.5f);
            }
            return height;
        }
    }

    [CustomEditor(typeof(DebugConsoleSettings))]
    internal sealed class DebugSettingsInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("Open Project Settings"))
            {
                ProjectSettingsWindow.Open();
            }
            if (GUILayout.Button("Open User Preferences"))
            {
                UserPrefSettingsWindow.Open();
            }
        }
    }
}