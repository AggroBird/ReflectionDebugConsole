// Copyright, AggrobirdGK

#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE
using AggroBird.Reflection;
using System.Collections.Generic;
using System.Text;
#endif

using System;
using UnityEngine;

namespace AggroBird.ReflectionDebugConsole
{
    public sealed class DebugConsoleGUI : IDisposable
    {
#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE
        public DebugConsoleGUI(bool isDocked = true)
        {
            this.isDocked = isDocked;

            if (isDocked) Open();
        }

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

        public bool IsOpen { get; private set; }
        public bool HasFocus { get; private set; }


        private readonly bool isDocked;

        // Capture counters
        private int consoleFocusFrameCount = 0;
        private int consoleCaptureFrameCount = 0;
        private int capturePosition = -1;

        // Console state
        private bool inputChanged = false;
        private string consoleInput = string.Empty;
        private string styledInput = null;
        private int cursorPosition = -1;
        private bool clearConsole = false;
        private float previousWindowHeight = 0;
        private int historyIndex = -1;
        private int highlightIndex = -1;
        private int highlightOffset = -1;
        private TouchScreenKeyboard touchScreenKeyboard = null;
        private bool IsTSKOpen => touchScreenKeyboard != null && touchScreenKeyboard.status == TouchScreenKeyboard.Status.Visible;
        private string consoleOutputText = string.Empty;
        private struct OutputLine
        {
            public OutputLine(string text, bool isError)
            {
                this.text = text;
                this.isError = isError;
            }
            public readonly string text;
            public readonly bool isError;
        }
        private readonly List<OutputLine> consoleOutputLines = new List<OutputLine>();

        // Suggestions
        private SuggestionResult suggestionResult = SuggestionResult.Empty;
        private readonly SuggestionProvider suggestionProvider = new SuggestionProvider();
        private readonly StringBuilder stringBuilder = new StringBuilder(1024);
        private int maxSuggestionCount = 0;

        // GUI style
        private GUIStyle boxStyle = default;
        private GUIStyle buttonStyle = default;
        private Texture2D whiteTexture = default;
        private Texture2D blackTexture = default;

        private const int CaptureFrameCount = 2;

        private static readonly Color32 foregroundColor = new Color32(255, 255, 255, 255);
        private static readonly Color32 backgroundColor = new Color32(30, 30, 30, 255);

        private HashSet<KeyCode> currentKeyPresses = new HashSet<KeyCode>();


        private void ResetState(bool clearInput = true)
        {
            if (clearInput)
            {
                consoleInput = string.Empty;
                cursorPosition = 0;
                clearConsole = true;
                historyIndex = -1;
                suggestionResult = SuggestionResult.Empty;
                capturePosition = -1;
            }
            else
            {
                capturePosition = cursorPosition = consoleInput.Length;
            }

            consoleCaptureFrameCount = CaptureFrameCount;
            highlightIndex = -1;
            highlightOffset = -1;

            inputChanged = true;
            styledInput = null;
        }


        public void DrawGUI(Rect position, int fontSize, float scaleFactor = 1, bool useTouchScreenKeyboard = false)
        {
            if (Event.current == null) throw new DebugConsoleException("DebugConsoleGUI.DrawGUI can only be called from OnGUI");

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

            if (isLayout)
            {
                suggestionProvider.Update();
            }

            if (!IsOpen) return;

            GUI.BeginGroup(position);
            {
                GUI.depth = int.MinValue;
                GUI.color = Color.white;

                // Initialize guistyles if null
                if (boxStyle == null || !whiteTexture)
                {
                    SetupGUIStyle();
                }

                // Calculate sizes
                int scaledFontSize = Mathf.FloorToInt(fontSize * scaleFactor);
                if (scaledFontSize < 1) scaledFontSize = 1;
                buttonStyle.fontSize = boxStyle.fontSize = scaledFontSize;
                float uiScale = (float)scaledFontSize / DebugConsoleSettings.DefaultFontSize;
                int padding = Mathf.FloorToInt(2 * uiScale);
                if (padding < 1) padding = 1;
                boxStyle.padding = new RectOffset(padding, padding, padding, padding);

                int verticalPadding = padding * 2;
                float boxHeight = boxStyle.lineHeight + verticalPadding;
                float buttonWidth = 30 * uiScale;
                float borderThickness = Mathf.Max(Mathf.Floor(scaleFactor), 1);
                Vector2 dimension = position.size;
                float y = dimension.y - boxHeight;
                float width = dimension.x - buttonWidth;

                // Update the input text
                bool guiEnabledState = GUI.enabled;
                GUI.enabled = isReady;
                TextEditor editor = null;
                {
                    Rect inputArea = new Rect(0, y, width + scaleFactor, boxHeight);

                    boxStyle.normal.textColor = backgroundColor;
                    boxStyle.richText = false;
                    string newInput = consoleInput;
                    if (!useTouchScreenKeyboard)
                    {
                        GUI.SetNextControlName(DebugConsole.UniqueKey);
                        newInput = GUI.TextField(DrawBackground(inputArea, borderThickness), consoleInput, boxStyle);
                    }
                    else
                    {
                        if (GUI.Button(DrawBackground(inputArea, borderThickness), string.Empty, boxStyle) && !IsTSKOpen)
                        {
                            TouchScreenKeyboard.hideInput = true;
                            touchScreenKeyboard = TouchScreenKeyboard.Open(consoleInput, TouchScreenKeyboardType.Default);
                        }

                        if (IsTSKOpen)
                        {
                            newInput = touchScreenKeyboard.text;
                        }
                    }

                    if (isReady && (newInput != consoleInput || dimension.y != previousWindowHeight))
                    {
                        consoleInput = newInput;
                        previousWindowHeight = dimension.y;
                        highlightIndex = -1;
                        highlightOffset = -1;
                        inputChanged = true;
                        styledInput = null;
                    }

                    // Get selection position and update focus
                    HasFocus = useTouchScreenKeyboard ? IsTSKOpen : GUI.GetNameOfFocusedControl() == DebugConsole.UniqueKey;
                    Vector2 scrollOffset = Vector2.zero;
                    if (HasFocus)
                    {
                        consoleFocusFrameCount = CaptureFrameCount;

                        if (useTouchScreenKeyboard)
                        {
                            cursorPosition = touchScreenKeyboard.selection.start;
                        }
                        else
                        {
                            editor = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
                            cursorPosition = (capturePosition == -1) ? editor.cursorIndex : capturePosition;
                            scrollOffset = editor.scrollOffset;
                        }
                    }

                    if (styledInput == null)
                    {
                        RebuildStyledInput();
                    }

                    // Draw styled text
                    GUI.BeginClip(Inset(inputArea, borderThickness));
                    boxStyle.normal.textColor = foregroundColor;
                    boxStyle.richText = true;
                    inputArea.position = new Vector2(-borderThickness, -borderThickness) - scrollOffset;
                    inputArea.width += scrollOffset.x;
                    GUI.Label(inputArea, isReady ? styledInput : "Scanning assemblies...", boxStyle);
                    GUI.EndClip();

                    if (GUI.Button(DrawBackground(new Rect(width, dimension.y - boxHeight, buttonWidth, boxHeight), borderThickness), ">", buttonStyle))
                    {
                        ExecuteInput();
                        Close();
                    }
                }
                GUI.enabled = guiEnabledState;

                float suggestionSpace = dimension.y - boxHeight;
                maxSuggestionCount = Mathf.FloorToInt((suggestionSpace - verticalPadding) / boxStyle.lineHeight);
                if (maxSuggestionCount < 1) maxSuggestionCount = 1;

                // Show suggestions
                if (!string.IsNullOrEmpty(suggestionResult.suggestionText) || isDocked)
                {
                    if (!isDocked)
                    {
                        boxHeight = boxStyle.lineHeight * suggestionResult.visibleLineCount + verticalPadding;
                        y -= boxHeight;
                    }
                    else
                    {
                        boxHeight = suggestionSpace;
                        y = 0;
                    }

                    boxHeight += scaleFactor;

                    boxStyle.richText = true;
                    string showText = consoleInput.Length > 0 ? suggestionResult.suggestionText : consoleOutputText;
                    GUI.Box(DrawBackground(new Rect(0, y, dimension.x, boxHeight), borderThickness), showText, boxStyle);
                }

                // Start a new task if the input has changed and no other tasks are running
                if (isLayout && isReady && inputChanged && !suggestionProvider.OperationInProgress)
                {
                    inputChanged = false;

                    if (!string.IsNullOrEmpty(consoleInput) && !DebugConsole.IsConsoleCommand(consoleInput))
                    {
                        suggestionProvider.BuildSuggestions(consoleInput, cursorPosition, maxSuggestionCount, OnSuggestionsBuild);
                    }
                    else
                    {
                        suggestionResult = SuggestionResult.Empty;
                    }
                }

                // Capture console
                if (isLayout && !useTouchScreenKeyboard && consoleCaptureFrameCount > 0)
                {
                    if (clearConsole)
                    {
                        consoleInput = string.Empty;
                        styledInput = null;
                    }

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
            GUI.EndGroup();
        }
        private Rect DrawBackground(Rect rect, float borderThickness)
        {
            GUI.DrawTexture(rect, whiteTexture);
            GUI.DrawTexture(Inset(rect, borderThickness), blackTexture);
            return rect;
        }
        private Rect Inset(Rect rect, float inset)
        {
            Rect copy = rect;
            copy.x += inset;
            copy.y += inset;
            inset *= 2;
            copy.width -= inset;
            copy.height -= inset;
            return copy;
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
                        CycleSuggestion(1);
                        current.Use();
                        return;
                    }
                    else if (settings.nextSuggestionKey.IsPressed(current))
                    {
                        CycleSuggestion(-1);
                        current.Use();
                        return;
                    }
                    else if (settings.autoCompleteKey.IsPressed(current))
                    {
                        AutocompleteSuggestion();
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
                if (DebugConsole.Execute(consoleInput, out object result, out Exception exception))
                {
                    if (result == null || !result.GetType().Equals(typeof(VoidResult)))
                    {
                        string asString = DebugConsole.FormatResult(result);
                        if (isDocked) AppendOutputLine(asString, false);
                        Debug.Log(asString);
                    }
                }
                else if (exception != null)
                {
                    if (isDocked) AppendOutputLine(exception.Message, true);
                    DebugConsole.HandleException(exception);
                }
                SaveToHistory(consoleInput.Trim());
            }
        }
        private void AppendOutputLine(string output, bool isError)
        {
            consoleOutputLines.Add(new OutputLine(output, isError));
            int maxLineCount = maxSuggestionCount * 2;
            if (consoleOutputLines.Count > maxLineCount) consoleOutputLines.RemoveRange(0, consoleOutputLines.Count - maxLineCount);
            stringBuilder.Clear();
            foreach (var line in consoleOutputLines)
            {
                if (stringBuilder.Length > 0) stringBuilder.Append('\n');
                if (line.isError)
                {
                    stringBuilder.Append(Styles.Open(Style.Error));
                    stringBuilder.AppendRTF(line.text);
                    stringBuilder.Append(Styles.Close);
                }
                else
                {
                    stringBuilder.AppendRTF(line.text);
                }
            }
            consoleOutputText = stringBuilder.ToString();
        }

        private void CycleSuggestion(int direction)
        {
            if (!suggestionProvider.OperationInProgress && suggestionResult.suggestions.Length > 0)
            {
                suggestionProvider.UpdateSuggestions(ref highlightOffset, ref highlightIndex, direction, OnSuggestionsUpdated);
            }
        }
        private void AutocompleteSuggestion()
        {
            if (!suggestionProvider.OperationInProgress && suggestionResult.suggestions.Length > 0 && suggestionResult.insertLength > 0)
            {
                // Find shortest matching suggestion
                string shortestSuggestion = suggestionResult.suggestions[0];
                int shortestLength = shortestSuggestion.Length;
                for (int i = 1; i < suggestionResult.suggestions.Length; i++)
                {
                    string suggestion = suggestionResult.suggestions[i];
                    int length = suggestion.Length;
                    int compareLen = Math.Min(length, shortestLength);
                    for (int j = 0; j < compareLen; j++)
                    {
                        if (char.ToLower(suggestion[j]) != char.ToLower(shortestSuggestion[j]))
                        {
                            shortestSuggestion = suggestion;
                            shortestLength = j;
                            goto Next;
                        }
                    }
                    if (length < shortestLength)
                    {
                        shortestSuggestion = suggestion;
                        shortestLength = length;
                    }
                Next: continue;
                }

                // Apply suggestion
                InsertSuggestion(shortestLength == shortestSuggestion.Length ? shortestSuggestion : shortestSuggestion.Substring(0, shortestLength));
                inputChanged = true;
            }
            else
            {
                capturePosition = cursorPosition;
                consoleCaptureFrameCount = CaptureFrameCount;
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


        private void OnSuggestionsBuild(SuggestionResult result)
        {
            highlightIndex = -1;
            highlightOffset = -1;

            suggestionResult = result;

            RebuildStyledInput();
        }
        private void OnSuggestionsUpdated(SuggestionResult result)
        {
            suggestionResult = result;

            if (!suggestionResult.isOverloadList)
            {
                InsertSuggestion(suggestionResult.suggestions[highlightIndex]);
            }
        }

        private void InsertSuggestion(string suggestion)
        {
            stringBuilder.Clear();

            // Insert before suggestion
            if (suggestionResult.insertOffset > 0)
            {
                stringBuilder.Append(suggestionResult.commandText.Substring(0, suggestionResult.insertOffset));
            }

            // Insert suggestion
            stringBuilder.Append(suggestion);

            // Recapture console at end of insert
            cursorPosition = capturePosition = stringBuilder.Length;

            // Insert remainder after suggestion (if any)
            int end = suggestionResult.insertOffset + suggestionResult.insertLength;
            if (end < suggestionResult.commandText.Length)
            {
                stringBuilder.Append(suggestionResult.commandText.Substring(end));
            }

            // Update text
            consoleInput = stringBuilder.ToString();
            consoleCaptureFrameCount = CaptureFrameCount;
            styledInput = null;
        }


        private void RebuildStyledInput()
        {
            if (suggestionResult.commandStyle != null && suggestionResult.commandStyle.Length > 0)
            {
                stringBuilder.Clear();
                StyledToken[] styledOutput = suggestionResult.commandStyle;
                int outputLength = 0;
                int maxLength = 0;
                int shortest = Mathf.Min(suggestionResult.commandText.Length, consoleInput.Length);
                for (; maxLength < shortest; maxLength++)
                {
                    if (suggestionResult.commandText[maxLength] != consoleInput[maxLength])
                    {
                        break;
                    }
                }
                foreach (StyledToken token in styledOutput)
                {
                    if (token.offset >= maxLength)
                    {
                        break;
                    }
                    if (token.offset > outputLength)
                    {
                        int appendLen = token.offset - outputLength;
                        stringBuilder.AppendRTF(suggestionResult.commandText, outputLength, appendLen);
                        outputLength += appendLen;
                    }
                    stringBuilder.Append(Styles.Open(token.style));
                    int newLength = token.offset + token.length;
                    if (newLength > maxLength)
                    {
                        int subLength = token.length - (newLength - maxLength);
                        stringBuilder.AppendRTF(suggestionResult.commandText.SubView(token.offset, subLength));
                        outputLength += subLength;
                    }
                    else
                    {
                        stringBuilder.AppendRTF(suggestionResult.commandText.SubView(token.offset, token.length));
                        outputLength += token.length;
                    }
                    stringBuilder.Append(Styles.Close);
                }
                if (consoleInput.Length > outputLength)
                {
                    stringBuilder.AppendRTF(consoleInput, outputLength, consoleInput.Length - outputLength);
                }
                styledInput = stringBuilder.ToString();
            }
            else
            {
                styledInput = consoleInput;
            }
        }

        private void SetupGUIStyle()
        {
            boxStyle = new GUIStyle();
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

        public void Dispose()
        {
            suggestionProvider.Dispose();
        }
#else
        public DebugConsoleGUI(bool isDocked = true) { }

        public void Open() { }
        public void Close() { }

        public bool IsOpen => false;
        public bool HasFocus => false;

        public void DrawGUI(Rect position, int fontSize, float scaleFactor = 1, bool useTouchScreenKeyboard = false)
        {
            GUI.Label(new Rect(0, 0, position.width, 20), "Debug Console is disabled");
        }

        public void Dispose() { }
#endif
    }
}