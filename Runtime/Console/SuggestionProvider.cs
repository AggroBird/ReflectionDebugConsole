using AggroBird.Reflection;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace AggroBird.ReflectionDebugConsole
{
    internal class SuggestionProvider
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

        private readonly StringBuilder stringBuilder = new StringBuilder();
        private SuggestionTable suggestionTable = default;
        private Task<SuggestionTable> updateSuggestionsTask = null;
        public bool IsBuildingSuggestions => updateSuggestionsTask != null;
        private Action onComplete;
        private SuggestionResult cachedResult = default;

        public void Update()
        {
            if (IsBuildingSuggestions)
            {
                // Update the task status and retrieve the result upon completion
                TaskStatus taskStatus = updateSuggestionsTask.Status;
                if (taskStatus >= TaskStatus.RanToCompletion)
                {
                    Exception exception = updateSuggestionsTask.Exception;

                    if (exception != null)
                    {
                        updateSuggestionsTask = null;

                        throw exception;
                    }
                    else
                    {
                        if (taskStatus == TaskStatus.RanToCompletion)
                        {
                            suggestionTable = updateSuggestionsTask.Result;
                            cachedResult = SuggestionResult.Empty;
                            updateSuggestionsTask = null;
                            onComplete?.Invoke();
                        }
                        else
                        {
                            updateSuggestionsTask = null;
                        }
                    }
                }
            }
        }

        public void BuildSuggestions(string input, int cursorPosition, Identifier identifierTable, IReadOnlyList<string> usingNamespaces, bool safeMode)
        {
            if (IsBuildingSuggestions) throw new DebugConsoleException("Suggestion building operation already in progress");

            SuggestionTableBuilder builder = new SuggestionTableBuilder(input, cursorPosition, identifierTable, usingNamespaces, safeMode);
            suggestionTable = builder.Build();
        }
        public void BuildSuggestionsAsync(string input, int cursorPosition, Identifier identifierTable, IReadOnlyList<string> usingNamespaces, bool safeMode, Action onComplete)
        {
            if (IsBuildingSuggestions) throw new DebugConsoleException("Suggestion building operation already in progress");

            SuggestionTableBuilder builder = new SuggestionTableBuilder(input, cursorPosition, identifierTable, usingNamespaces, safeMode);
            this.onComplete = onComplete;
            updateSuggestionsTask = Task.Run(() => builder.Build());
        }

        public SuggestionResult GetResult(ref int highlightOffset, ref int highlightIndex, int direction, int maxCount)
        {
            if (IsBuildingSuggestions) throw new DebugConsoleException("Suggestion building operation still in progress");

            cachedResult.commandText = suggestionTable.commandText;
            cachedResult.commandStyle = suggestionTable.commandStyle;
            cachedResult.insertOffset = suggestionTable.insertOffset;
            cachedResult.insertLength = suggestionTable.insertLength;
            cachedResult.visibleLineCount = suggestionTable.visibleLineCount;
            cachedResult.isOverloadList = suggestionTable.isOverloadList;

            if (suggestionTable.Update(ref highlightOffset, ref highlightIndex, direction, maxCount, stringBuilder))
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

            return cachedResult;
        }
    }
}
