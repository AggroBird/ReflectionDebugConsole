// Copyright, AggrobirdGK

#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE

using AggroBird.Reflection;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace AggroBird.DebugConsole
{
    internal sealed class ConsoleGUI
    {
        private const int CaptureFrameCount = 2;
        private const string ScanningAssembliesText = "Scanning assemblies...";

        private static readonly Color32 foregroundColor = new Color32(255, 255, 255, 255);
        private static readonly Color32 backgroundColor = new Color32(30, 30, 30, 255);


        public ConsoleGUI(bool isDocked)
        {
            this.isDocked = isDocked;

            if (isDocked) Open();
        }

        public readonly bool isDocked;

        public bool IsOpen { get; private set; }
        public bool HasFocus { get; private set; }

        // Capture counters
        private int consoleFocusFrameCount = 0;
        private int consoleCaptureFrameCount = 0;
        private int capturePosition = -1;

        // Console state
        private bool inputChanged = false;
        private string consoleInput = string.Empty;
        private int cursorPosition = -1;
        private bool clearConsole = false;
        private float previousWindowHeight = 0;
        private int historyIndex = -1;
        private int highlightIndex = -1;
        private int highlightOffset = -1;

        // Suggestions
        private Task<SuggestionTable> updateSuggestionsTask = null;
        private SuggestionTable suggestionResult = SuggestionTable.Empty;
        private readonly StringBuilder stringBuilder = new StringBuilder(1024);
        private int maxSuggestionCount = 0;

        private class SuggestionTableBuilder
        {
            public SuggestionTableBuilder(string input, int cursorPosition, Identifier identifierTable, IReadOnlyList<string> usingNamespaces, bool safeMode)
            {
                this.input = input;
                this.cursorPosition = cursorPosition;
                this.identifierTable = identifierTable;
                this.usingNamespaces = usingNamespaces;
                this.safeMode = safeMode;
            }

            private readonly string input;
            private readonly int cursorPosition;
            private readonly Identifier identifierTable;
            private readonly IReadOnlyList<string> usingNamespaces;
            private readonly bool safeMode;

            public SuggestionTable Build() => new SuggestionTable(input, cursorPosition, identifierTable, usingNamespaces, safeMode);
        }

        // GUI style
        private GUIStyle boxStyle = default;
        private GUIStyle buttonStyle = default;
        private Texture2D whiteTexture = default;
        private Texture2D blackTexture = default;

        private HashSet<KeyCode> currentKeyPresses = new HashSet<KeyCode>();


        public void Open()
        {
            if (!IsOpen)
            {
                IsOpen = true;

                ResetState();
            }
        }
        public void Close()
        {
            if (IsOpen)
            {
                if (!isDocked)
                {
                    IsOpen = false;

                    GUI.FocusControl(null);
                }

                ResetState();
            }
        }

        private void ResetState(bool clearInput = true)
        {
            if (clearInput)
            {
                consoleInput = string.Empty;
                cursorPosition = 0;
                clearConsole = true;
                historyIndex = -1;
                suggestionResult = SuggestionTable.Empty;
            }
            else
            {
                cursorPosition = consoleInput.Length;
            }

            OnInputChanged();
            capturePosition = -1;
            consoleCaptureFrameCount = CaptureFrameCount;
            highlightIndex = -1;
            highlightOffset = -1;
        }
        private void OnInputChanged()
        {
            inputChanged = true;
        }


        public void UpdateGUI(Vector2 dimensions, int fontSize, float scaleModifier = 1)
        {
            bool isLayout = Event.current != null && Event.current.type == EventType.Layout;

            bool isReady = DebugConsole.EnsureIdentifierTable();

            UpdateKeyboardEvents();

            // Keep console focus for some extra frames to catch any return
            // input presses going directly to the original application
            if (isLayout && consoleFocusFrameCount > 0)
            {
                consoleFocusFrameCount--;
            }
            HasFocus = consoleFocusFrameCount > 0;

            if (isLayout && updateSuggestionsTask != null)
            {
                // Update the task status and retrieve the result upon completion
                TaskStatus taskStatus = updateSuggestionsTask.Status;
                if (taskStatus >= TaskStatus.RanToCompletion)
                {
                    Exception exception = updateSuggestionsTask.Exception;
                    if (exception != null)
                    {
                        Debug.LogException(exception);
                    }
                    if (taskStatus == TaskStatus.RanToCompletion)
                    {
                        OnSuggestionResultCompleted(updateSuggestionsTask.Result);
                    }
                    updateSuggestionsTask = null;
                }
            }

            if (IsOpen)
            {
                GUI.depth = int.MinValue;
                GUI.color = Color.white;

                // Initialize guistyles if null
                if (boxStyle == null || !whiteTexture)
                {
                    SetupGUIStyle();
                }

                // Calculate sizes
                int scaledFontSize = Mathf.FloorToInt(fontSize * scaleModifier);
                if (scaledFontSize < 1) scaledFontSize = 1;
                buttonStyle.fontSize = boxStyle.fontSize = scaledFontSize;
                float uiScale = (float)scaledFontSize / Settings.DefaultFontSize;
                int padding = Mathf.FloorToInt(2 * uiScale);
                if (padding < 1) padding = 1;
                boxStyle.padding = new RectOffset(padding, padding, padding, padding);

                int verticalPadding = padding * 2;
                float boxHeight = boxStyle.lineHeight + verticalPadding;
                float buttonWidth = 30 * uiScale;
                float borderThickness = Mathf.Max(Mathf.Floor(scaleModifier), 1);
                float y = dimensions.y - boxHeight;
                float width = dimensions.x - buttonWidth;

                GUI.SetNextControlName(DebugConsole.UniqueKey);

                // Update the input text
                bool guiEnabledState = GUI.enabled;
                GUI.enabled = isReady;
                {
                    Rect inputArea = new Rect(0, y, width + scaleModifier, boxHeight);
                    boxStyle.normal.textColor = backgroundColor;
                    string newInput = GUI.TextField(DrawBackground(inputArea, borderThickness), consoleInput, boxStyle);
                    boxStyle.normal.textColor = foregroundColor;
                    string showText = isReady ? consoleInput : ScanningAssembliesText;
                    GUI.Label(inputArea, showText, boxStyle);
                    if (isReady && (newInput != consoleInput || dimensions.y != previousWindowHeight))
                    {
                        OnInputChanged();
                        consoleInput = newInput;
                        previousWindowHeight = dimensions.y;
                        highlightIndex = -1;
                        highlightOffset = -1;
                    }
                    if (GUI.Button(DrawBackground(new Rect(width, dimensions.y - boxHeight, buttonWidth, boxHeight), borderThickness), ">", buttonStyle))
                    {
                        ExecuteInput();
                        Close();
                    }
                }
                GUI.enabled = guiEnabledState;

                // Get selection position and update focus
                HasFocus = GUI.GetNameOfFocusedControl() == DebugConsole.UniqueKey;
                TextEditor editor = null;
                if (HasFocus)
                {
                    consoleFocusFrameCount = CaptureFrameCount;

                    editor = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
                    cursorPosition = (capturePosition == -1) ? editor.cursorIndex : capturePosition;
                }

                float suggestionSpace = dimensions.y - boxHeight;
                maxSuggestionCount = Mathf.FloorToInt((suggestionSpace - verticalPadding) / boxStyle.lineHeight);
                if (maxSuggestionCount < 1) maxSuggestionCount = 1;

                // Show suggestions
                if (!string.IsNullOrEmpty(suggestionResult.text) || isDocked)
                {
                    if (!isDocked)
                    {
                        boxHeight = boxStyle.lineHeight * suggestionResult.suggestions.Length + verticalPadding;
                        y -= boxHeight;
                    }
                    else
                    {
                        boxHeight = suggestionSpace;
                        y = 0;
                    }

                    boxHeight += scaleModifier;

                    GUI.Box(DrawBackground(new Rect(0, y, dimensions.x, boxHeight), borderThickness), suggestionResult.text, boxStyle);
                }

                // Start a new task if the input has changed and no other tasks are running
                if (isLayout && isReady && inputChanged && updateSuggestionsTask == null)
                {
                    inputChanged = false;

                    if (!string.IsNullOrEmpty(consoleInput))
                    {
                        SuggestionTableBuilder builder = new SuggestionTableBuilder(consoleInput, cursorPosition, DebugConsole.IdentifierTable, DebugConsole.UsingNamespacesString, DebugConsole.Settings.safeMode);
                        if (Application.platform == RuntimePlatform.WebGLPlayer)
                        {
                            // WebGL does not support threading
                            OnSuggestionResultCompleted(builder.Build());
                        }
                        else
                        {
                            updateSuggestionsTask = Task.Factory.StartNew(() => builder.Build());
                        }
                    }
                    else
                    {
                        suggestionResult = SuggestionTable.Empty;
                    }
                }

                // Capture console
                if (isLayout && consoleCaptureFrameCount > 0)
                {
                    if (clearConsole) consoleInput = string.Empty;

                    GUI.FocusControl(DebugConsole.UniqueKey);
                    if (HasFocus)
                    {
                        if (isReady) consoleCaptureFrameCount--;

                        if (capturePosition == -1)
                            editor.MoveTextEnd();
                        else
                            editor.selectIndex = editor.cursorIndex = capturePosition;

                        if (consoleCaptureFrameCount <= 0)
                        {
                            clearConsole = false;
                            capturePosition = -1;
                        }
                    }
                }
            }
        }
        private Rect DrawBackground(Rect rect, float borderThickness)
        {
            Rect copy = rect;
            GUI.DrawTexture(copy, whiteTexture);
            copy.x += borderThickness;
            copy.y += borderThickness;
            borderThickness *= 2;
            copy.width -= borderThickness;
            copy.height -= borderThickness;
            GUI.DrawTexture(copy, blackTexture);
            return rect;
        }


        private void OnSuggestionResultCompleted(SuggestionTable table)
        {
            highlightIndex = -1;
            highlightOffset = -1;

            suggestionResult = table;
            suggestionResult.Update(ref highlightOffset, ref highlightIndex, 0, maxSuggestionCount, stringBuilder);
        }

        private void UpdateKeyboardEvents()
        {
            Event current = Event.current;
            if (current.type == EventType.KeyDown && current.keyCode != KeyCode.None)
            {
                if (!IsOpen)
                {
                    foreach (KeyBind bind in DebugConsole.Settings.openConsoleKeys)
                    {
                        if (bind.IsPressed(current))
                        {
                            Open();
                            current.Use();
                            return;
                        }
                    }
                }

                if (HasFocus)
                {
                    var history = DebugConsole.history;
                    var settings = DebugConsole.Settings;

                    foreach (KeyBind bind in DebugConsole.Settings.submitInputKeys)
                    {
                        if (bind.IsPressed(current))
                        {
                            ExecuteInput();
                            Close();
                            current.Use();
                            return;
                        }
                    }

                    if (settings.closeConsoleKey.IsPressed(current))
                    {
                        Close();
                        current.Use();
                        return;
                    }
                    else if (settings.prevHistoryKey.IsPressed(current))
                    {
                        if (history.Count > 0 && historyIndex < history.Count - 1)
                        {
                            historyIndex++;
                            consoleInput = history[historyIndex];
                            ResetState(false);
                        }
                        current.Use();
                        return;
                    }
                    else if (settings.nextHistoryKey.IsPressed(current))
                    {
                        if (history.Count > 0 && historyIndex > 0)
                        {
                            historyIndex--;
                            consoleInput = history[historyIndex];
                            ResetState(false);
                        }
                        current.Use();
                        return;
                    }
                    else if (settings.prevSuggestionKey.IsPressed(current))
                    {
                        ApplySuggestion(1);
                        current.Use();
                        return;
                    }
                    else if (settings.nextSuggestionKey.IsPressed(current))
                    {
                        ApplySuggestion(-1);
                        current.Use();
                        return;
                    }
                    else if (settings.autoCompleteKey.IsPressed(current))
                    {
                        if (suggestionResult && !suggestionResult.isOverloadList && suggestionResult.insertLength > 0)
                        {
                            // Find shortest matching suggestion
                            int shortestLength = suggestionResult.visible[0].Text.Length;
                            int shortestIndex = 0;
                            for (int i = 1; i < suggestionResult.visible.Count; i++)
                            {
                                var suggestion = suggestionResult.visible[i];
                                int len = suggestion.Text.Length;
                                if (len < shortestLength)
                                {
                                    shortestLength = len;
                                    shortestIndex = i;
                                }
                            }

                            // Apply suggestion
                            InsertSuggestion(shortestIndex);
                            OnInputChanged();
                        }
                        else
                        {
                            capturePosition = cursorPosition;
                            consoleCaptureFrameCount = CaptureFrameCount;
                        }

                        current.Use();
                        return;
                    }
                }
            }

            if (!isDocked)
            {
                var macroTable = DebugConsole.MacroTable;

                if ((current.type == EventType.KeyDown && !HasFocus) || current.type == EventType.KeyUp)
                {
                    if (current.type == EventType.KeyDown)
                    {
                        if (currentKeyPresses.Contains(current.keyCode)) return;
                        currentKeyPresses.Add(current.keyCode);
                    }
                    else
                    {
                        if (!currentKeyPresses.Contains(current.keyCode)) return;
                        currentKeyPresses.Remove(current.keyCode);
                    }

                    if (macroTable.TryGetValue(current.keyCode, out List<Macro> list))
                    {
                        foreach (var macro in list)
                        {
                            if (macro.IsPressed(current))
                            {
                                DebugConsole.Execute(macro.command);
                            }
                        }
                    }
                }
            }
        }

        private void ExecuteInput()
        {
            if (!string.IsNullOrEmpty(consoleInput))
            {
                if (DebugConsole.Execute(consoleInput, out object result))
                {
                    if (!(result != null && result.GetType() == typeof(VoidResult)))
                    {
                        Debug.Log(result);
                    }
                }
                SaveToHistory(consoleInput.Trim());
            }
        }

        private void SaveToHistory(string cmd)
        {
            var settings = DebugConsole.Settings;
            var history = DebugConsole.history;

            if (settings.maxHistoryCount > 0)
            {
                for (int i = 0; i < history.Count; i++)
                {
                    if (history[i] == cmd)
                    {
                        if (i != 0)
                        {
                            history.RemoveAt(i);
                            history.Insert(0, cmd);
                            DebugConsole.SaveHistory();
                        }

                        return;
                    }
                }

                history.Insert(0, cmd);
                if (history.Count > settings.maxHistoryCount)
                {
                    history.RemoveRange(settings.maxHistoryCount, history.Count - settings.maxHistoryCount);
                }
                DebugConsole.SaveHistory();
            }
        }


        private void ApplySuggestion(int direction)
        {
            if (suggestionResult && suggestionResult.Update(ref highlightOffset, ref highlightIndex, direction, maxSuggestionCount, stringBuilder))
            {
                if (!suggestionResult.isOverloadList)
                {
                    InsertSuggestion(highlightIndex + highlightOffset);
                }
            }
        }
        private void InsertSuggestion(int index)
        {
            Suggestion suggestion = suggestionResult.suggestions[index];

            stringBuilder.Clear();

            // Insert prefix (namespace, etc.)
            if (suggestionResult.insertOffset > 0)
            {
                stringBuilder.Append(suggestionResult.input.Substring(0, suggestionResult.insertOffset));
            }

            // Insert suggestion
            stringBuilder.Append(suggestion.Text);

            // Recapture console at end of insert
            cursorPosition = capturePosition = stringBuilder.Length;

            // Insert remainder after suggestion (if any)
            int end = suggestionResult.insertOffset + suggestionResult.insertLength;
            if (end < suggestionResult.input.Length)
            {
                stringBuilder.Append(suggestionResult.input.Substring(end));
            }

            // Update text
            consoleInput = stringBuilder.ToString();
            consoleCaptureFrameCount = CaptureFrameCount;
        }


        private void SetupGUIStyle()
        {
            boxStyle = new GUIStyle();
            boxStyle.richText = true;
            boxStyle.normal.textColor = foregroundColor;
            boxStyle.border = new RectOffset(4, 4, 4, 4);
            boxStyle.clipping = TextClipping.Clip;
            boxStyle.alignment = TextAnchor.LowerLeft;

            whiteTexture = MakeTexture(8, 8, foregroundColor);
            blackTexture = MakeTexture(8, 8, backgroundColor);

            buttonStyle = new GUIStyle(boxStyle);
            buttonStyle.alignment = TextAnchor.MiddleCenter;
        }
        private Texture2D MakeTexture(int width, int height, Color32 color)
        {
            Color32[] pixels = new Color32[width * height];
            Texture2D texture = new Texture2D(width, height);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Point;
            for (int y = 0, idx = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    pixels[idx++] = color;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}
#endif