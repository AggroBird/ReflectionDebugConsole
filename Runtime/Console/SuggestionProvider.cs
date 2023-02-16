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

        internal static int lastId = 0;
        internal readonly int id = lastId++;
        public int ID => id;

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
                    else if (taskStatus == TaskStatus.RanToCompletion && OperationInProgress)
                    {
                        suggestionTable = updateSuggestionsTask.Result;
                        cachedResult = SuggestionResult.Empty;
                        updateSuggestionsTask = null;
                        BuildResult();
                        onComplete?.Invoke(cachedResult);
                    }

                    onComplete = null;
                    OperationInProgress = false;
                    updateSuggestionsTask = null;
                }
            }
        }

        public void BuildSuggestions(string input, int cursorPosition, int maxSuggestionCount, Action<SuggestionResult> onComplete)
        {
            if (OperationInProgress) throw new DebugConsoleException(OperationInProgressException);

#if UNITY_EDITOR
            // Forward request to remote server
            if (DebugConsole.HasRemoteConnection && !Application.isPlaying)
            {
                DebugConsole.SendSuggestionBuildRequest(this, input, cursorPosition, maxSuggestionCount);
                OperationInProgress = true;
                this.onComplete = onComplete;
                return;
            }
#endif

            SuggestionTableBuilder builder = new SuggestionTableBuilder(input, cursorPosition, DebugConsole.IdentifierTable, DebugConsole.UsingNamespaces, DebugConsole.Settings.safeMode);

            this.maxSuggestionCount = maxSuggestionCount;

            if (DebugConsole.PlatformSupportsThreading())
            {
                updateSuggestionsTask = Task.Run(() => builder.Build());
                OperationInProgress = true;
                this.onComplete = onComplete;
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
            // Forward request to remote server
            if (DebugConsole.HasRemoteConnection && !Application.isPlaying)
            {
                DebugConsole.SendSuggestionUpdateRequest(this, highlightOffset, highlightIndex, direction);
                OperationInProgress = true;
                this.onComplete = onComplete;
                return;
            }
#endif

            // No need to async this, we can use the cached result
            BuildResult(highlightOffset, highlightIndex, direction);
            onComplete?.Invoke(cachedResult);
        }

        public void OnRemoteSuggestionsReceived(SuggestionResult result)
        {
            if (OperationInProgress)
            {
                onComplete?.Invoke(result);
                onComplete = null;
                OperationInProgress = false;
            }
        }
        public void OnRemoteRequestCancelled()
        {
            onComplete = null;
            OperationInProgress = false;
        }

        private void BuildResult(int highlightOffset, int highlightIndex, int direction)
        {
            if (suggestionTable.Update(ref highlightOffset, ref highlightIndex, direction, maxSuggestionCount, stringBuilder))
            {
                // Update text if changed
                cachedResult.suggestionText = stringBuilder.ToString();
                if (cachedResult.suggestions == null || cachedResult.suggestions.Length != suggestionTable.visible.Count)
                {
                    cachedResult.suggestions = new string[suggestionTable.visible.Count];
                }
                for (int i = 0; i < suggestionTable.visible.Count; i++)
                {
                    cachedResult.suggestions[i] = suggestionTable.visible[i].Text;
                }
            }

            cachedResult.id = id;
            cachedResult.commandText = suggestionTable.commandText;
            cachedResult.commandStyle = suggestionTable.commandStyle;
            cachedResult.insertOffset = suggestionTable.insertOffset;
            cachedResult.insertLength = suggestionTable.insertLength;
            cachedResult.visibleLineCount = suggestionTable.visibleLineCount;
            cachedResult.isOverloadList = suggestionTable.isOverloadList;
            cachedResult.highlightOffset = highlightOffset;
            cachedResult.highlightIndex = highlightIndex;
        }
        private void BuildResult()
        {
            BuildResult(-1, -1, 0);
        }

        public void Dispose()
        {
            OperationInProgress = false;
            onComplete = null;
        }
    }
}
#endif