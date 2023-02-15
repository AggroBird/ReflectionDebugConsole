// Copyright, AggrobirdGK

#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE

using AggroBird.Reflection;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace AggroBird.ReflectionDebugConsole
{
    internal class SuggestionProvider : IDisposable
    {
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

        internal static uint lastId = 0;
        internal readonly uint id = lastId++;
        public uint ID => id;

        private readonly StringBuilder stringBuilder = new StringBuilder();
        private SuggestionTable suggestionTable = default;
        private Task<SuggestionTable> updateSuggestionsTask = null;
        public bool OperationInProgress { get; private set; }
        private SuggestionResult cachedResult = default;

        private const string OperationInProgressException = "Another operation already in progress";

        private Action<SuggestionResult> onComplete;
        private int maxSuggestionCount;

        public void Update()
        {
            if (OperationInProgress)
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
                    else
                    {
                        if (taskStatus == TaskStatus.RanToCompletion)
                        {
                            suggestionTable = updateSuggestionsTask.Result;
                            cachedResult = SuggestionResult.Empty;
                            updateSuggestionsTask = null;
                            BuildResult();
                            onComplete?.Invoke(cachedResult);
                        }
                    }

                    onComplete = null;
                    updateSuggestionsTask = null;
                    OperationInProgress = false;
                }
            }
        }

        public void BuildSuggestions(string input, int cursorPosition, int maxSuggestionCount, Action<SuggestionResult> onComplete)
        {
            if (OperationInProgress) throw new DebugConsoleException(OperationInProgressException);

#if UNITY_EDITOR
            if (DebugConsole.HasRemoteConnection)
            {
                return;
            }
#endif

            SuggestionTableBuilder builder = new SuggestionTableBuilder(input, cursorPosition, DebugConsole.IdentifierTable, DebugConsole.UsingNamespacesString, DebugConsole.Settings.safeMode);

            this.maxSuggestionCount = maxSuggestionCount;

            if (DebugConsole.PlatformSupportsThreading())
            {
                OperationInProgress = true;
                this.onComplete = onComplete;
                updateSuggestionsTask = Task.Run(() => builder.Build());
            }
            else
            {
                suggestionTable = builder.Build();
                BuildResult();
                onComplete?.Invoke(cachedResult);
            }
        }
        public void UpdateSuggestions(int highlightOffset, int highlightIndex, int direction, Action<SuggestionResult> onComplete)
        {
            if (OperationInProgress) throw new DebugConsoleException(OperationInProgressException);

#if UNITY_EDITOR
            if (DebugConsole.HasRemoteConnection)
            {
                return;
            }
#endif

            BuildResult(highlightOffset, highlightIndex, direction);
            onComplete?.Invoke(cachedResult);
        }

        private void BuildResult(int highlightOffset, int highlightIndex, int direction)
        {
            cachedResult.commandText = suggestionTable.commandText;
            cachedResult.commandStyle = suggestionTable.commandStyle;
            cachedResult.insertOffset = suggestionTable.insertOffset;
            cachedResult.insertLength = suggestionTable.insertLength;
            cachedResult.visibleLineCount = suggestionTable.visibleLineCount;
            cachedResult.isOverloadList = suggestionTable.isOverloadList;

            if (suggestionTable.Update(ref highlightOffset, ref highlightIndex, direction, maxSuggestionCount, stringBuilder))
            {
                cachedResult.suggestionText = stringBuilder.ToString();
                stringBuilder.Clear();
                cachedResult.suggestions = new string[suggestionTable.visible.Count];
                int idx = 0;
                foreach (var visible in suggestionTable.visible)
                {
                    cachedResult.suggestions[idx++] = visible.Text;
                }
            }

            cachedResult.insertOffset = highlightOffset;
            cachedResult.highlightIndex = highlightIndex;
        }
        private void BuildResult()
        {
            BuildResult(-1, -1, 0);
        }

        public void Dispose()
        {

        }
    }
}
#endif