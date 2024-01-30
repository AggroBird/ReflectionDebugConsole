// Copyright, AggrobirdGK

#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE
using AggroBird.Reflection;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Object = UnityEngine.Object;
#endif

using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using System.Globalization;

[assembly: InternalsVisibleTo("AggroBird.ReflectionDebugConsole.Editor")]

namespace AggroBird.ReflectionDebugConsole
{
    public static class DebugConsole
    {
        internal const string Namespace = "AggroBird";
        internal const string HistoryKey = "history";
        internal const string SettingsKey = "settings";
        internal const string MacrosKey = "macros";
        internal const string InstanceKey = "instance";
        internal static readonly string UniqueKey = $"{Namespace}.ReflectionDebugConsole";
        internal static readonly string SettingsFileName = $"{UniqueKey}.{SettingsKey}";
        internal static readonly string GameObjectName = $"{UniqueKey}.{InstanceKey}";
        internal static readonly string LastAddressName = $"{UniqueKey}.lastAddress";
        internal const string LogPrefix = "[DebugConsole]";

        public delegate void OnConsoleFocusChange(bool isFocused);
        public static event OnConsoleFocusChange onConsoleFocusChange = default;

#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE
        public static void Log(object msg)
        {
#if INCLUDE_DEBUG_SERVER
            if (instance && instance.CurrentExecutingClient != null)
            {
                instance.CurrentExecutingClient.Send(FormatResult(msg), MessageFlags.Log);
            }
#endif
            Debug.Log($"{LogPrefix} {msg}");
        }
        public static void LogWarning(object msg)
        {
#if INCLUDE_DEBUG_SERVER
            if (instance && instance.CurrentExecutingClient != null)
            {
                instance.CurrentExecutingClient.Send(FormatResult(msg), MessageFlags.Warning);
            }
#endif
            Debug.LogWarning($"{LogPrefix} {msg}");
        }
        public static void LogError(object msg)
        {
#if INCLUDE_DEBUG_SERVER
            if (instance && instance.CurrentExecutingClient != null)
            {
                instance.CurrentExecutingClient.Send(FormatResult(msg), MessageFlags.Error);
            }
#endif
            Debug.LogError($"{LogPrefix} {msg}");
        }

        private static Instance instance;
        private static DebugConsoleGUI gui;
        private static DebugConsoleSettings settings = null;
        internal static DebugConsoleSettings Settings
        {
            get
            {
                if (!settings)
                {
                    settings = LoadSettings();
                }

                return settings;
            }
        }
        internal static void OverrideSettings(DebugConsoleSettings settings)
        {
            DebugConsole.settings = settings;
        }

        private static DebugConsoleGUI GetGUI()
        {
            Initialize();

            if (gui == null)
            {
                gui = new DebugConsoleGUI(false);
            }

            return gui;
        }

        private static DebugConsoleSettings LoadSettings()
        {
            DebugConsoleSettings settings = Resources.Load<DebugConsoleSettings>(SettingsFileName);

            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<DebugConsoleSettings>();
            }

            return settings;
        }

        private static bool AllowConsoleGUI => Debug.isDebugBuild || Settings.allowConsoleInRelease;

        private static Assembly[] assemblies = null;
        private static Assembly[] GetEnabledAssemblies()
        {
            if (assemblies == null)
            {
                assemblies = Settings.assemblies.GetEnabledAssemblies();
            }
            return assemblies;
        }

        internal static readonly List<string> history = LoadHistory();
        internal static void SaveHistory()
        {
            SavePrefs(HistoryKey, new ListObject<string>(history));
        }
        private static List<string> LoadHistory()
        {
            return LoadPrefs<ListObject<string>>(HistoryKey);
        }

        private static Dictionary<MacroKeyBind, List<Macro>> macroTableInstance = null;
        internal static Dictionary<MacroKeyBind, List<Macro>> MacroTable
        {
            get
            {
                if (macroTableInstance == null)
                {
                    macroTableInstance = new Dictionary<MacroKeyBind, List<Macro>>();
                    AppendMacroTable(Settings.sharedMacros);
                    AppendMacroTable(Macros.localMacros);
                }
                return macroTableInstance;
            }
        }
        private static void AppendMacroTable(List<Macro> macros)
        {
            if (macros != null)
            {
                var macroTable = MacroTable;
                foreach (var macro in macros)
                {
                    MacroKeyBind keyBind = new MacroKeyBind(macro.bind, macro.state);
                    if (!macroTable.TryGetValue(keyBind, out List<Macro> list))
                    {
                        list = new List<Macro>();
                        macroTable.Add(keyBind, list);
                    }
                    list.Add(macro);
                }
            }
        }

        private static IReadOnlyList<string>[] validNamespacesSplit = null;
        private static string[] validNamespaces = null;
        private static void EnsureNamespaces()
        {
            if (validNamespacesSplit == null)
            {
                List<IReadOnlyList<string>> result = new List<IReadOnlyList<string>>();
                HashSet<string> uniqueNamespaces = new HashSet<string>();
                StringBuilder stringBuilder = new StringBuilder();
                foreach (string ns in Settings.namespaces)
                {
                    stringBuilder.Clear();
                    string[] tokens = ns.Split('.');
                    for (int i = 0; i < tokens.Length; i++)
                    {
                        tokens[i] = tokens[i].Trim();
                        if (!IsValidNamespace(tokens[i]))
                        {
                            goto Skip;
                        }
                        if (stringBuilder.Length > 0) stringBuilder.Append('.');
                        stringBuilder.Append(tokens[i]);
                    }
                    string valid = stringBuilder.ToString();
                    if (uniqueNamespaces.Contains(valid)) continue;
                    uniqueNamespaces.Add(valid);
                    result.Add(tokens);
                Skip:
                    continue;
                }
                validNamespacesSplit = result.ToArray();

                // Build namespace strings
                StringBuilder builder = new StringBuilder();
                validNamespaces = new string[validNamespacesSplit.Length];
                for (int i = 0; i < validNamespacesSplit.Length; i++)
                {
                    IReadOnlyList<string> validNamespace = validNamespacesSplit[i];
                    for (int j = 0; j < validNamespace.Count; j++)
                    {
                        if (builder.Length > 0) builder.Append('.');
                        builder.Append(validNamespace[j]);
                    }
                    validNamespaces[i] = builder.ToString();
                    builder.Clear();
                }
            }
        }
        private static IReadOnlyList<string>[] UsingNamespacesSplit
        {
            get
            {
                EnsureNamespaces();
                return validNamespacesSplit;
            }
        }
        internal static IReadOnlyList<string> UsingNamespaces
        {
            get
            {
                EnsureNamespaces();
                return validNamespaces;
            }
        }
        private static bool IsValidNamespace(string str)
        {
            if (string.IsNullOrEmpty(str)) return false;
            if (char.IsNumber(str[0])) return false;
            for (int i = 0; i < str.Length; i++)
            {
                char c = str[i];
                if (!(c == '_' || char.IsLetter(c) || char.IsNumber(c)))
                {
                    return false;
                }
            }
            return true;
        }

        private class IdentifierTableBuilder
        {
            public IdentifierTableBuilder(Assembly[] assemblies, IReadOnlyList<string>[] usingNamespaces, bool safeMode)
            {
                this.assemblies = assemblies;
                this.usingNamespaces = usingNamespaces;
                this.safeMode = safeMode;
            }

            private readonly Assembly[] assemblies;
            private readonly IReadOnlyList<string>[] usingNamespaces;
            private readonly bool safeMode;

            public Identifier Build(CancellationToken cancellationToken)
            {
                return new Identifier(assemblies, usingNamespaces, safeMode, cancellationToken);
            }
        }

        // WebGL does not support threading
        internal static bool PlatformSupportsThreading() => Application.isEditor || Application.platform != RuntimePlatform.WebGLPlayer;

        private static Task<Identifier> identifierTableTask = null;
        private static CancellationTokenSource identifierTableTokenSource = null;
        private static void BuildIdentifierTable()
        {
            if (identifierTableTask == null)
            {
                identifierTable = null;

#if UNITY_EDITOR
                UnityEditor.EditorApplication.update -= UpdateIdentifierTableTask;
                UnityEditor.EditorApplication.update += UpdateIdentifierTableTask;
#endif

                identifierTableTokenSource = new CancellationTokenSource();
                CancellationToken cancellationToken = identifierTableTokenSource.Token;
                IdentifierTableBuilder builder = new IdentifierTableBuilder(GetEnabledAssemblies(), UsingNamespacesSplit, Settings.safeMode);
                if (PlatformSupportsThreading())
                {
                    identifierTableTask = Task.Run(() => builder.Build(cancellationToken));
                }
                else
                {
                    identifierTable = builder.Build(cancellationToken);
                }
            }
        }
        private static void UpdateIdentifierTableTask()
        {
            if (identifierTableTask != null)
            {
                TaskStatus taskStatus = identifierTableTask.Status;
                if (taskStatus >= TaskStatus.RanToCompletion)
                {
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.update -= UpdateIdentifierTableTask;
#endif

                    Exception exception = identifierTableTask.Exception;
                    if (exception != null)
                    {
                        Debug.LogException(exception);
                    }
                    if (taskStatus == TaskStatus.RanToCompletion)
                    {
                        identifierTable = identifierTableTask.Result;
                    }

                    identifierTableTokenSource.Dispose();
                    identifierTableTokenSource = null;
                    identifierTableTask = null;
                }
            }
        }
        private static void CancelIdentifierTableTask()
        {
            if (identifierTableTask != null)
            {
                identifierTableTokenSource.Cancel();
                try { identifierTableTask.Wait(); } catch { }

                identifierTableTokenSource.Dispose();
                identifierTableTokenSource = null;
                identifierTableTask = null;

#if UNITY_EDITOR
                UnityEditor.EditorApplication.update -= UpdateIdentifierTableTask;
#endif
            }
        }
        internal static bool EnsureIdentifierTable()
        {
            if (identifierTableTask != null)
            {
                return false;
            }

            if (identifierTable == null)
            {
                BuildIdentifierTable();
                return false;
            }

            return true;
        }
        private static Identifier identifierTable;
        internal static Identifier IdentifierTable => identifierTable == null ? Identifier.Empty : identifierTable;


        internal class Instance : MonoBehaviour
        {
            private GameViewReference gameViewReference = new GameViewReference();
            private bool hasFocus = false;

#if INCLUDE_DEBUG_SERVER
            private DebugServer server;
            private readonly SuggestionProvider serverSuggestionProvider = new SuggestionProvider();
            private DebugClient currentExecutingClient;
            public DebugClient CurrentExecutingClient => currentExecutingClient;

            public bool IsRunningServer => server != null;

            public void StartDebugServer()
            {
                StopDebugServer();

                server = new DebugServer(Settings.serverPort, Settings.authenticationKey);
            }
            public void StopDebugServer()
            {
                if (server != null)
                {
                    server.Close();
                    server = null;
                }
            }
#endif

            private void Start()
            {
                BuildIdentifierTable();

                GetGUI();
            }

            private void Update()
            {
                Initialize();

                // Ensure we are still the main instance
                if (instance != this)
                {
                    Destroy(this);
                    return;
                }

                UpdateIdentifierTableTask();

#if INCLUDE_DEBUG_SERVER
                if (Settings.startDebugServer && !Application.isEditor && server == null)
                {
                    StartDebugServer();
                }

                if (server != null)
                {
                    serverSuggestionProvider.Update();

                    server.Update(out DebugServer.Message[] messages);
                    for (int i = 0; i < messages.Length; i++)
                    {
                        switch (messages[i].flags)
                        {
                            case MessageFlags.None:
                                currentExecutingClient = messages[i].sender;
                                if (ExecuteCommand(messages[i].message, out object result, out Exception exception, isRemoteCommand: true))
                                {
                                    // Send the output to the client
                                    if (result == null || !result.GetType().Equals(typeof(VoidResult)))
                                    {
                                        currentExecutingClient.Send(FormatResult(result), MessageFlags.Log);
                                    }
                                }
                                else if (exception != null)
                                {
                                    // Send the exception to the client
                                    currentExecutingClient.Send(exception.ToString(), MessageFlags.Error);
                                }
                                currentExecutingClient = null;
                                break;

                            case MessageFlags.BuildSuggestions:
                            {
                                SuggestionBuildRequest request = JsonUtility.FromJson<SuggestionBuildRequest>(messages[i].message);
                                DebugClient sender = messages[i].sender;
                                serverSuggestionProvider.BuildSuggestions(request.input, request.cursorPosition, request.maxSuggestionCount, (result) =>
                                {
                                    if (sender.State == DebugClient.ConnectionState.Connected)
                                    {
                                        try
                                        {
                                            result.id = request.id;
                                            sender.Send(JsonUtility.ToJson(result), MessageFlags.SuggestionResult);
                                        }
                                        catch (Exception exception)
                                        {
                                            sender.Send(JsonUtility.ToJson(new SuggestionRequestFailed
                                            {
                                                id = request.id,
                                                error = exception.Message,
                                            }), MessageFlags.SuggestionFailed);
                                        }
                                    }
                                });
                            }
                            break;

                            case MessageFlags.UpdateSuggestions:
                            {
                                SuggestionUpdateRequest request = JsonUtility.FromJson<SuggestionUpdateRequest>(messages[i].message);
                                DebugClient sender = messages[i].sender;
                                serverSuggestionProvider.UpdateSuggestions(request.highlightOffset, request.highlightIndex, request.direction, (result) =>
                                {
                                    if (sender.State == DebugClient.ConnectionState.Connected)
                                    {
                                        try
                                        {
                                            result.id = request.id;
                                            sender.Send(JsonUtility.ToJson(result), MessageFlags.SuggestionResult);
                                        }
                                        catch (Exception exception)
                                        {
                                            sender.Send(JsonUtility.ToJson(new SuggestionRequestFailed
                                            {
                                                id = request.id,
                                                error = exception.Message,
                                            }), MessageFlags.SuggestionFailed);
                                        }
                                    }
                                });
                            }
                            break;
                        }
                    }
                }
#endif
            }
            private void OnGUI()
            {
                if (instance == this && gui != null)
                {
                    // Update UI
                    float gameViewScale = Settings.invertScale ? gameViewReference.GetGameViewScale() : 1;
                    gui.DrawGUI(new Rect(0, 0, Screen.width, Screen.height), Settings.fontSize, scale * gameViewScale);

                    // Update focus callback
                    bool focus = gui.IsOpen && gui.HasFocus;
                    if (focus != hasFocus)
                    {
                        hasFocus = focus;
                        onConsoleFocusChange?.Invoke(hasFocus);
                    }
                }
            }

            private void OnDestroy()
            {
                if (gui != null)
                {
                    gui.Close();
                    gui.Dispose();
                    gui = null;
                }

#if INCLUDE_DEBUG_SERVER
                StopDebugServer();
#endif
            }
        }

        public static void StartDebugServer()
        {
#if INCLUDE_DEBUG_SERVER
            if (instance)
            {
                if (instance.IsRunningServer)
                {
                    LogWarning("Debug server is already running");
                    return;
                }
                instance.StartDebugServer();
            }
#else
            LogWarning("Debug server has been excluded from build");
#endif
        }
        public static void StopDebugServer()
        {
#if INCLUDE_DEBUG_SERVER
            if (instance)
            {
                if (!instance.IsRunningServer)
                {
                    LogWarning("Debug server is not running");
                    return;
                }
                instance.StartDebugServer();
            }
#else
            LogWarning("Debug server has been excluded from build");
#endif
        }

        [Serializable]
        private struct SuggestionBuildRequest
        {
            public int id;
            public string input;
            public int cursorPosition;
            public int maxSuggestionCount;
        }

        [Serializable]
        private struct SuggestionUpdateRequest
        {
            public int id;
            public int highlightOffset;
            public int highlightIndex;
            public int direction;
        }

        [Serializable]
        private struct SuggestionRequestFailed
        {
            public int id;
            public string error;
        }

#if UNITY_EDITOR
        private static DebugClient client;

        internal static bool HasRemoteConnection => client != null && client.State == DebugClient.ConnectionState.Connected;

        internal static void SendSuggestionBuildRequest(SuggestionProvider provider, string input, int cursorPosition, int maxSuggestionCount)
        {
            awaitingRequests.Add(provider.id, provider);

            client.Send(JsonUtility.ToJson(new SuggestionBuildRequest
            {
                id = provider.id,
                input = input,
                cursorPosition = cursorPosition,
                maxSuggestionCount = maxSuggestionCount,
            }), MessageFlags.BuildSuggestions);
        }
        internal static void SendSuggestionUpdateRequest(SuggestionProvider provider, int highlightOffset, int highlightIndex, int direction)
        {
            awaitingRequests.Add(provider.id, provider);

            client.Send(JsonUtility.ToJson(new SuggestionUpdateRequest
            {
                id = provider.id,
                highlightOffset = highlightOffset,
                highlightIndex = highlightIndex,
                direction = direction,
            }), MessageFlags.UpdateSuggestions);
        }

        private static readonly Dictionary<int, SuggestionProvider> awaitingRequests = new Dictionary<int, SuggestionProvider>();

        private static void OpenConnection(string address, int port)
        {
            CloseConnection();

            client = new DebugClient(address, port, Settings.authenticationKey);

            UnityEditor.EditorApplication.update -= UpdateConnection;
            UnityEditor.EditorApplication.update += UpdateConnection;
        }
        private static void CloseConnection()
        {
            UnityEditor.EditorApplication.update -= UpdateConnection;

            foreach (SuggestionProvider provider in awaitingRequests.Values)
            {
                provider.OnRemoteRequestCancelled();
            }
            awaitingRequests.Clear();

            if (client != null)
            {
                client.Close();
                client = null;
            }
        }

        private static void UpdateConnection()
        {
            if (client != null)
            {
                if (client.State == DebugClient.ConnectionState.Disconnected)
                {
                    // Clean up
                    CloseConnection();
                }
                else if (client.Poll(out string message, out MessageFlags flags))
                {
                    switch (flags)
                    {
                        default: Debug.Log($"[{client.Endpoint}] {message}"); break;
                        case MessageFlags.Warning: Debug.LogWarning($"[{client.Endpoint}] {message}"); break;
                        case MessageFlags.Error: Debug.LogError($"[{client.Endpoint}] {message}"); break;
                        case MessageFlags.SuggestionResult:
                        {
                            try
                            {
                                SuggestionResult result = JsonUtility.FromJson<SuggestionResult>(message);
                                if (awaitingRequests.TryGetValue(result.id, out SuggestionProvider provider))
                                {
                                    awaitingRequests.Remove(result.id);
                                    provider.OnRemoteSuggestionsReceived(result);
                                }
                            }
                            catch (Exception exception)
                            {
                                Debug.LogException(exception);
                            }
                        }
                        break;
                        case MessageFlags.SuggestionFailed:
                        {
                            try
                            {
                                SuggestionRequestFailed result = JsonUtility.FromJson<SuggestionRequestFailed>(message);
                                if (awaitingRequests.TryGetValue(result.id, out SuggestionProvider provider))
                                {
                                    awaitingRequests.Remove(result.id);
                                    provider.OnRemoteRequestCancelled();
                                    Debug.LogError($"[{client.Endpoint}] {result.error}");
                                }
                            }
                            catch (Exception exception)
                            {
                                Debug.LogException(exception);
                            }
                        }
                        break;
                    }
                }
            }
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        internal static void Initialize()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                LogWarning("Debug console cannot be accessed when editor is not running");
                return;
            }
#endif

            if (!instance && AllowConsoleGUI)
            {
                instance = Object.FindObjectOfType<Instance>();
                if (!instance)
                {
                    // Create a new instance if we lost our previous
                    GameObject gameObject = GameObject.Find(GameObjectName);
                    if (!gameObject) gameObject = new GameObject(GameObjectName);
                    instance = gameObject.AddComponent<Instance>();
                    gameObject.hideFlags |= HideFlags.NotEditable | HideFlags.HideInHierarchy | HideFlags.HideInInspector;
                    Object.DontDestroyOnLoad(gameObject);
                }
                else
                {
                    // Attempt to reactivate existing instance
                    instance.enabled = true;
                }
            }
        }


        internal static bool IsSpace(char c)
        {
            switch (c)
            {
                case ' ':
                case '\t':
                case '\n':
                case '\r':
                    return true;
                default:
                    return false;
            }
        }
        internal static bool IsNullOrSpace(string cmd)
        {
            if (cmd != null)
            {
                for (int i = 0; i < cmd.Length; i++)
                {
                    if (!IsSpace(cmd[i]))
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        internal static bool IsConsoleCommand(string cmd)
        {
            for (int i = 0; i < cmd.Length; i++)
            {
                if (!IsSpace(cmd[i]))
                {
                    switch (cmd[i])
                    {
                        case '/':
                            if (i < cmd.Length - 1)
                            {
                                char next = cmd[i + 1];
                                return next >= 'a' && next <= 'z';
                            }
                            return false;
                        default:
                            return false;
                    }
                }
            }
            return false;
        }
        private static void ValidateConsoleCommandArgCount(string[] args, int requiredCount)
        {
            if (args.Length != requiredCount)
            {
                throw new DebugConsoleException($"Invalid amount of arguments provided for command '{args[0]}'");
            }
        }
        private static bool HandleConsoleCommand(string cmd)
        {
            string[] args = cmd.Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            switch (args[0])
            {
#if UNITY_EDITOR
                case "/open":
                {
                    string address;
                    if (args.Length == 1)
                    {
                        address = PlayerPrefs.GetString(LastAddressName, "127.0.0.1");
                    }
                    else
                    {
                        ValidateConsoleCommandArgCount(args, 2);
                        address = args[1];
                    }

                    int portIdx = address.LastIndexOf(':');
                    if (portIdx == -1)
                    {
                        OpenConnection(address, Settings.serverPort);
                    }
                    else
                    {
                        if (portIdx == 0 || !int.TryParse(cmd.Substring(portIdx + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int port))
                        {
                            LogError($"Invalid ip address format: '{address}'");
                            return false;
                        }

                        OpenConnection(cmd, port);
                    }

                    if (args.Length > 1)
                    {
                        PlayerPrefs.SetString(LastAddressName, address);
                    }
                }
                break;

                case "/close":
                {
                    ValidateConsoleCommandArgCount(args, 1);

                    CloseConnection();
                }
                break;
#endif

                case "/reload":
                {
                    ValidateConsoleCommandArgCount(args, 1);

                    Reload();
                }
                break;

                case "/scale":
                {
                    ValidateConsoleCommandArgCount(args, 2);

                    if (!float.TryParse(args[1], NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float setScale))
                    {
                        LogError($"Failed to parse scale argument: '{args[1]}'");
                    }

                    scale = setScale;
                }
                break;

                default:
                    LogError($"Unknown console command: '{args[0]}'");
                    break;
            }

            return true;
        }

        internal static string FormatResult(object result)
        {
            return result == null ? "null" : result.ToString();
        }

        private static readonly byte[] TruncatedString = Encoding.UTF8.GetBytes("... <message truncated>");
        private static readonly byte[] NullMsg = Encoding.UTF8.GetBytes("null");
        private static byte[] TruncateNetworkMessage(object msg)
        {
            if (msg == null) return NullMsg;

            byte[] bytes = Encoding.UTF8.GetBytes(msg.ToString());
            if (bytes.Length > DebugClient.MaxPackageSize)
            {
                for (int i = DebugClient.MaxPackageSize - 1; i >= 0; i--)
                {
                    if (((bytes[i] & 128) == 0) || ((bytes[i] & 64) != 0))
                    {
                        if (DebugClient.MaxPackageSize - i >= TruncatedString.Length)
                        {
                            byte[] truncated = new byte[i + TruncatedString.Length];
                            Buffer.BlockCopy(bytes, 0, truncated, 0, i);
                            Buffer.BlockCopy(TruncatedString, 0, truncated, i, TruncatedString.Length);
                            return truncated;
                        }
                    }
                }
            }
            return bytes;
        }


        private static bool ExecuteCommand(string cmd, out object result, out Exception exception, bool isRemoteCommand = false)
        {
            result = VoidResult.Empty;
            exception = null;

            if (!IsNullOrSpace(cmd))
            {
                try
                {
                    // Handle console commands
                    if (IsConsoleCommand(cmd))
                    {
                        return HandleConsoleCommand(cmd);
                    }

#if UNITY_EDITOR
                    // Forward commands to the client if there is one
                    if (client != null && !isRemoteCommand && !Application.isPlaying)
                    {
                        client.Send(cmd);
                        return true;
                    }
#endif

                    Token[] tokens = new Lexer(cmd).ToArray();
                    CommandParser commandParser = new CommandParser(tokens, IdentifierTable, Settings.safeMode, Settings.maxIterationCount);
                    Command command = commandParser.Parse();
                    result = command.Execute();
                    return true;
                }
                catch (TargetInvocationException targetInvocationException)
                {
                    exception = targetInvocationException.InnerException;
                }
                catch (Exception regularException)
                {
                    exception = regularException;
                }
            }

            return false;
        }
        internal static void HandleException(Exception exception)
        {
            if (exception != null)
            {
                if (exception is DebugConsoleException)
                {
                    // Treat syntax errors as non-exceptions
                    Debug.LogError(exception.Message);
                }
                else
                {
                    // Forward the exception to the unity console
                    Debug.LogException(exception);
                }
            }
        }


        public static void Open() { if (AllowConsoleGUI) { Initialize(); GetGUI().Open(); } }
        public static void Close() { gui?.Close(); }

        public static bool Execute(string cmd)
        {
            bool success = ExecuteCommand(cmd, out _, out Exception exception);
            HandleException(exception);
            return success;
        }
        public static bool Execute(string cmd, out object result)
        {
            bool success = ExecuteCommand(cmd, out result, out Exception exception);
            HandleException(exception);
            return success;
        }
        public static bool Execute(string cmd, out Exception exception)
        {
            return ExecuteCommand(cmd, out _, out exception);
        }
        public static bool Execute(string cmd, out object result, out Exception exception)
        {
            return ExecuteCommand(cmd, out result, out exception);
        }

        public static bool IsOpen => Application.isPlaying && gui != null && gui.IsOpen;
        public static bool HasFocus => Application.isPlaying && gui != null && gui.HasFocus;

        public static void Reload()
        {
            macroTableInstance = null;
            validNamespacesSplit = null;
            assemblies = null;

            CancelIdentifierTableTask();
            identifierTable = null;

            settings = LoadSettings();
        }
        internal static void ReloadMacroTable()
        {
            macroTableInstance = null;
        }

#else
        public static void Log(object msg)
        {
            Debug.Log($"{LogPrefix} {msg}");
        }
        public static void LogWarning(object msg)
        {
            Debug.LogWarning($"{LogPrefix} {msg}");
        }
        public static void LogError(object msg)
        {
            Debug.LogError($"{LogPrefix} {msg}");
        }

        public static void Open() { }
        public static void Close() { }

        public static void StartDebugServer() { }
        public static void StopDebugServer() { }

        public static bool Execute(string cmd)
        {
            return false;
        }
        public static bool Execute(string cmd, out object result)
        {
            result = null;
            return false;
        }
        public static bool Execute(string cmd, out Exception exception)
        {
            exception = null;
            return false;
        }
        public static bool Execute(string cmd, out object result, out Exception exception)
        {
            result = null;
            exception = null;
            return false;
        }

        public static readonly bool IsOpen = false;
        public static readonly bool HasFocus = false;

        public static void Reload() { }
#endif

        public static float scale = 1;

        internal static T LoadPrefs<T>(string key) where T : new()
        {
            try
            {
                string content = PlayerPrefs.GetString($"{UniqueKey}.{key}", string.Empty);
                if (!string.IsNullOrEmpty(content))
                {
                    return JsonUtility.FromJson<T>(content);
                }
            }
            catch (Exception)
            {

            }

            return new T();
        }
        internal static void SavePrefs(string key, object obj)
        {
            if (obj != null)
            {
                PlayerPrefs.SetString($"{UniqueKey}.{key}", JsonUtility.ToJson(obj));
            }
        }
    }
}
