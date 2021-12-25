// Copyright, 2021, AggrobirdGK

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace AggroBird.DebugConsole.Editor
{
    [CustomPropertyDrawer(typeof(KeyBind))]
    internal sealed class KeyBindPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            position = EditorGUI.PrefixLabel(position, label);

            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            float width = position.width;
            float size = 70;
            position.width = size - 1;

            EditorGUI.PropertyField(position, property.FindPropertyRelative("mod"), GUIContent.none);

            position.x += position.width + 2;
            position.width = width - (size + 1);

            EditorGUI.PropertyField(position, property.FindPropertyRelative("code"), GUIContent.none);

            EditorGUI.indentLevel = indent;
        }
    }

    [CustomPropertyDrawer(typeof(Macro))]
    internal sealed class MacroPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            bool isEditorWindow = DebugSettingsWindow.isEditorWindow;
            if (!isEditorWindow || DebugSettingsWindow.windowPosition.Overlaps(position))
            {
                if (!isEditorWindow)
                {
                    position = EditorGUI.PrefixLabel(position, label);
                }

                Event evt = Event.current;
                Rect fullPosition = position;
                float width = position.width;

                bool contains = position.Contains(evt.mousePosition);

                position.y += 1;

                SerializedProperty bind = property.FindPropertyRelative("bind");

                position.width = Mathf.Min(70, position.width / 4);
                if (isEditorWindow) position.width += 20;
                PropertyField(bind.FindPropertyRelative("mod"), position);
                position.x += position.width;
                width -= position.width;

                int indent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;

                PropertyField(bind.FindPropertyRelative("code"), position);
                position.x += position.width;
                width -= position.width;

                PropertyField(property.FindPropertyRelative("state"), position);
                position.x += position.width;
                width -= position.width;

                position.y -= 1;
                position.height -= 1;

                position.width = width;
                Rect textArea = position;
                PropertyField(property.FindPropertyRelative("command"), position, false);

                if (isEditorWindow)
                {
                    fullPosition.x -= 50;
                    fullPosition.width += 100;
                    if (evt.type == EventType.MouseDown && evt.button == 1 && fullPosition.Contains(evt.mousePosition) && !textArea.Contains(evt.mousePosition))
                    {
                        GenericMenu context = new GenericMenu();
                        ContextData data = new ContextData
                        {
                            obj = DebugSettingsWindow.currentObject,
                            prop = DebugSettingsWindow.currentProperty,
                            idx = DebugSettingsWindow.currentPropertyIndex,
                        };
                        context.AddItem(new GUIContent("Duplicate Macro"), false, Duplicate, data);
                        context.AddItem(new GUIContent("Delete Macro"), false, Delete, data);
                        context.ShowAsContext();
                    }
                }

                EditorGUI.indentLevel = indent;
            }

            DebugSettingsWindow.currentPropertyIndex++;
        }

        private class ContextData
        {
            public Object obj;
            public string prop;
            public int idx;
        }

        private void Duplicate(object data)
        {
            ContextData contextData = (ContextData)data;
            if (contextData.obj)
            {
                SerializedObject obj = new SerializedObject(contextData.obj);
                obj.Update();
                obj.FindProperty(contextData.prop).InsertArrayElementAtIndex(contextData.idx);
                obj.ApplyModifiedProperties();

                if (DebugSettingsWindow.window)
                {
                    DebugSettingsWindow.window.ClearSelection();
                }
            }
        }
        private void Delete(object data)
        {
            ContextData contextData = (ContextData)data;
            if (contextData.obj)
            {
                SerializedObject obj = new SerializedObject(contextData.obj);
                obj.Update();
                obj.FindProperty(contextData.prop).DeleteArrayElementAtIndex(contextData.idx);
                obj.ApplyModifiedProperties();

                if (DebugSettingsWindow.window)
                {
                    DebugSettingsWindow.window.ClearSelection();
                }
            }
        }

        private void PropertyField(SerializedProperty property, Rect position, bool addMargin = true)
        {
            if (addMargin) position.width -= 2;
            EditorGUI.PropertyField(position, property, GUIContent.none);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return base.GetPropertyHeight(property, label) + 1;
        }
    }

    [CustomEditor(typeof(DebugSettings))]
    internal sealed class DebugSettingsInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("Open Debug Console Settings"))
            {
                DebugSettingsWindow.ShowWindow();
            }
        }
    }
}
#endif