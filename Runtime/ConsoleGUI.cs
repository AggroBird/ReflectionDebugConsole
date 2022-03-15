// Copyright, 2021, AggrobirdGK

#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace AggroBird.DebugConsole
{
    internal sealed class ConsoleGUI
    {
        public ConsoleGUI(bool isDocked)
        {
            this.isDocked = isDocked;

            if (isDocked) OpenConsole();
        }

        public bool isDocked { get; private set; }

        public bool isConsoleOpen { get; private set; }
        public bool hasConsoleFocus { get; private set; }

        private int captureConsole = 0;
        private int capturePos = -1;
        private bool clearConsole = false;
        private string consoleInput = string.Empty;
        private SuggestionTable suggestionResult = new SuggestionTable();
        private int maxSuggestionCount = 1;
        private int selectionPos = 0;
        private bool inputChanged = false;
        private int historyIndex = -1;
        private int highlightIndex = -1;
        private int highlightOffset = -1;
        private float previousWindowHeight = 0;
        private HashSet<KeyCode> currentKeyPresses = new HashSet<KeyCode>();

        private const int CaptureFrameCount = 2;

        private GUIStyle boxStyle = default;
        private GUIStyle buttonStyle = default;
        private Texture2D whiteTexture = default;
        private Texture2D blackTexture = default;

        private Task<SuggestionTable> updateSuggestionsTask = null;
        private StringBuilder suggestionStringBuilder = new StringBuilder();
        private StringBuilder updateStringBuilder = new StringBuilder();


        private class SuggestionTableBuilder
        {
            public SuggestionTableBuilder(string input, int selection, Assembly[] assemblies, string[] usingNamespaces, bool safeMode, StringBuilder stringBuilder)
            {
                this.input = input;
                this.selection = selection;
                this.assemblies = assemblies;
                this.usingNamespaces = usingNamespaces;
                this.safeMode = safeMode;
                this.stringBuilder = stringBuilder;
            }

            private string input;
            private int selection;
            private Assembly[] assemblies;
            private string[] usingNamespaces;
            private bool safeMode;
            private StringBuilder stringBuilder;

            public SuggestionTable Build() => new SuggestionTable(input, selection, assemblies, usingNamespaces, safeMode, stringBuilder);
        }


        public void UpdateGUI(Vector2 dimensions, int fontSize, float scaleModifier = 1)
        {
            bool isLayout = Event.current != null && Event.current.type == EventType.Layout;

            if (isLayout)
            {
                if (updateSuggestionsTask != null)
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
                            OnSuggestionResult(updateSuggestionsTask.Result);
                        }
                        updateSuggestionsTask = null;
                    }
                }
            }

            UpdateKeyboardEvents();

            hasConsoleFocus = false;

            if (isConsoleOpen)
            {
                GUI.depth = int.MinValue;

                // Initialize guistyles if null
                if (boxStyle == null || !whiteTexture)
                {
                    SetupGUIStyle();
                }

                // Calculate sizes
                int scaledFontSize = Mathf.FloorToInt(fontSize * scaleModifier);
                if (scaledFontSize < 1) scaledFontSize = 1;
                buttonStyle.fontSize = boxStyle.fontSize = scaledFontSize;
                float uiScale = (float)scaledFontSize / DebugSettings.DefaultFontSize;
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
                string newInput = GUI.TextField(DrawBackground(new Rect(0, y, width + scaleModifier, boxHeight), borderThickness), consoleInput, boxStyle);
                if (newInput != consoleInput || dimensions.y != previousWindowHeight)
                {
                    consoleInput = newInput;
                    inputChanged = true;
                    highlightIndex = -1;
                    highlightOffset = -1;
                    previousWindowHeight = dimensions.y;
                }
                if (GUI.Button(DrawBackground(new Rect(width, dimensions.y - boxHeight, buttonWidth, boxHeight), borderThickness), ">", buttonStyle))
                {
                    ExecuteInput();
                    CloseConsole();
                }

                // Get selection pos
                hasConsoleFocus = GUI.GetNameOfFocusedControl() == DebugConsole.UniqueKey;
                TextEditor editor = null;
                if (hasConsoleFocus)
                {
                    editor = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
                    selectionPos = (capturePos == -1) ? editor.cursorIndex : capturePos;
                }

                float suggestionSpace = dimensions.y - boxHeight;
                maxSuggestionCount = Mathf.FloorToInt((suggestionSpace - verticalPadding) / boxStyle.lineHeight);
                if (maxSuggestionCount < 1) maxSuggestionCount = 1;

                // Show suggestions
                bool hasSuggestions = (suggestionResult != null && suggestionResult.lineCount > 0);
                if (hasSuggestions || isDocked)
                {
                    if (!isDocked)
                    {
                        boxHeight = boxStyle.lineHeight * suggestionResult.lineCount + verticalPadding;
                        y -= boxHeight;
                    }
                    else
                    {
                        boxHeight = suggestionSpace;
                        y = 0;
                    }

                    boxHeight += scaleModifier;

                    GUI.Box(DrawBackground(new Rect(0, y, dimensions.x, boxHeight), borderThickness), hasSuggestions ? suggestionResult.text : string.Empty, boxStyle);
                }

                // Start a new task if the input has changed and no other tasks are running
                if (isLayout && inputChanged && updateSuggestionsTask == null)
                {
                    inputChanged = false;

                    if (!string.IsNullOrEmpty(consoleInput))
                    {
                        var assemblies = DebugConsole.MakeAssemblyArray();
                        var namespaces = DebugConsole.MakeValidNamespaceArray();
                        bool safeMode = DebugConsole.settings.safeMode;
                        var builder = new SuggestionTableBuilder(consoleInput, selectionPos, assemblies, namespaces, safeMode, suggestionStringBuilder);
                        if (Application.platform == RuntimePlatform.WebGLPlayer)
                        {
                            // WebGL does not support threading
                            OnSuggestionResult(builder.Build());
                        }
                        else
                        {
                            updateSuggestionsTask = Task.Factory.StartNew(() => builder.Build());
                        }
                    }
                    else
                    {
                        suggestionResult = new SuggestionTable();
                    }
                }

                // Capture console
                if (captureConsole > 0)
                {
                    if (clearConsole) consoleInput = string.Empty;

                    GUI.FocusControl(DebugConsole.UniqueKey);
                    if (hasConsoleFocus)
                    {
                        captureConsole--;

                        if (capturePos == -1)
                            editor.MoveTextEnd();
                        else
                            editor.selectIndex = editor.cursorIndex = capturePos;

                        if (captureConsole <= 0)
                        {
                            clearConsole = false;
                            capturePos = -1;
                        }
                    }
                }
            }
        }

        private void SetupGUIStyle()
        {
            boxStyle = new GUIStyle();
            boxStyle.richText = true;
            boxStyle.normal.textColor = Color.white;
            boxStyle.border = new RectOffset(4, 4, 4, 4);
            boxStyle.clipping = TextClipping.Clip;
            boxStyle.alignment = TextAnchor.LowerLeft;

            whiteTexture = MakeTexture(8, 8, new Color32(255, 255, 255, 255));
            blackTexture = MakeTexture(8, 8, new Color32(0, 0, 0, 255));

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
        private Rect DrawBackground(Rect rect, float borderThickness)
        {
            {
                Rect copy = rect;
                GUI.DrawTexture(copy, whiteTexture);
                copy.x += borderThickness;
                copy.y += borderThickness;
                borderThickness *= 2;
                copy.width -= borderThickness;
                copy.height -= borderThickness;
                GUI.DrawTexture(copy, blackTexture);
            }
            return rect;
        }

        private void OnSuggestionResult(SuggestionTable result)
        {
            suggestionResult = result;
            highlightIndex = -1;
            highlightOffset = -1;
            suggestionResult.UpdateSuggestionIndex(ref highlightOffset, ref highlightIndex, 0, maxSuggestionCount, updateStringBuilder);
        }

        private void UpdateKeyboardEvents()
        {
            Event current = Event.current;
            if (current.type == EventType.KeyDown && current.keyCode != KeyCode.None)
            {
                if (!isConsoleOpen)
                {
                    foreach (KeyBind bind in DebugConsole.settings.openConsoleKeys)
                    {
                        if (bind.IsPressed(current))
                        {
                            OpenConsole();
                            current.Use();
                            return;
                        }
                    }
                }

                if (hasConsoleFocus)
                {
                    var history = DebugConsole.history;
                    var settings = DebugConsole.settings;

                    foreach (KeyBind bind in DebugConsole.settings.submitInputKeys)
                    {
                        if (bind.IsPressed(current))
                        {
                            ExecuteInput();
                            CloseConsole();
                            current.Use();
                            return;
                        }
                    }

                    if (settings.closeConsoleKey.IsPressed(current))
                    {
                        CloseConsole();
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
                        if (!suggestionResult.isOverloadSuggestion)
                        {
                            ApplySuggestion(1);
                        }
                        current.Use();
                        return;
                    }
                    else if (settings.nextSuggestionKey.IsPressed(current))
                    {
                        if (!suggestionResult.isOverloadSuggestion)
                        {
                            ApplySuggestion(-1);
                        }
                        current.Use();
                        return;
                    }
                    else if (settings.autoCompleteKey.IsPressed(current))
                    {
                        if (suggestionResult.visibleSuggestions.Count > 0)
                        {
                            // Find shortest matching suggestion
                            int shortestLength = int.MaxValue;
                            int shortestIndex = -1;
                            for (int i = 0; i < suggestionResult.visibleSuggestions.Count; i++)
                            {
                                var suggestion = suggestionResult.visibleSuggestions[i];
                                int len = suggestion.offsetText.Length;
                                if (len < shortestLength)
                                {
                                    shortestLength = len;
                                    shortestIndex = i;
                                }
                            }

                            Suggestion shortest = suggestionResult.visibleSuggestions[shortestIndex];
                            string main = shortest.offsetText.Substring(0, shortestLength);
                            foreach (var suggestion in suggestionResult.visibleSuggestions)
                            {
                                if (!suggestion.offsetText.StartsWith(main, true, null))
                                    goto SkipSuggestion;
                            }
                            
                            // Apply suggestion
                            InsertSuggestion(shortest);
                            inputChanged = true;
                        }

                    SkipSuggestion:
                        current.Use();
                        return;
                    }
                }
            }

            if (!isDocked)
            {
                var macroTable = DebugConsole.macroTable;

                if ((current.type == EventType.KeyDown && !hasConsoleFocus) || current.type == EventType.KeyUp)
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
        private void ApplySuggestion(int direction)
        {
            if (suggestionResult.allSuggestions.Count > 0)
            {
                if (suggestionResult.UpdateSuggestionIndex(ref highlightOffset, ref highlightIndex, direction, maxSuggestionCount, updateStringBuilder))
                {
                    InsertSuggestion(suggestionResult.allSuggestions[highlightIndex + highlightOffset]);
                }
            }
        }
        private void InsertSuggestion(Suggestion suggestion)
        {
            string suggestionText = suggestion.offsetText;
            int push = suggestionResult.position - suggestion.len;
            updateStringBuilder.Clear();
            if (push > 0)
            {
                updateStringBuilder.Append(suggestionResult.input.Substring(0, push) + suggestionText);
                selectionPos = capturePos = updateStringBuilder.Length;
                updateStringBuilder.Append(suggestionResult.input.Substring(push + suggestion.len));
            }
            else
            {
                updateStringBuilder.Append(suggestionText + suggestionResult.input.Substring(push + suggestion.len));
                selectionPos = updateStringBuilder.Length;
                capturePos = -1;
            }
            consoleInput = updateStringBuilder.ToString();
            captureConsole = CaptureFrameCount;
        }

        internal void OpenConsole()
        {
            if (!isConsoleOpen)
            {
                isConsoleOpen = true;

                ResetState();
            }
        }
        internal void CloseConsole()
        {
            if (isConsoleOpen)
            {
                if (!isDocked)
                {
                    isConsoleOpen = false;

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
                selectionPos = 0;
                suggestionResult = null;
                historyIndex = -1;
                clearConsole = true;
            }
            else
            {
                selectionPos = consoleInput.Length;
            }

            captureConsole = CaptureFrameCount;
            capturePos = -1;
            inputChanged = true;
            highlightIndex = -1;
            highlightOffset = -1;
        }

        private void ExecuteInput()
        {
            string cmd = consoleInput.Trim();
            if (DebugConsole.Execute(cmd, out object result))
            {
                if (!(result is Type asType && asType == typeof(void)))
                {
                    Debug.Log(result);
                }
            }

            if (!string.IsNullOrEmpty(cmd))
            {
                SaveToHistory(cmd);
            }
        }
        private void SaveToHistory(string cmd)
        {
            var settings = DebugConsole.settings;
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
    }
}
#endif