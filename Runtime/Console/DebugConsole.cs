// Copyright, AggrobirdGK

using AggroBird.Reflection;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityObject = UnityEngine.Object;

[assembly: InternalsVisibleTo("AggroBird.DebugConsole.Editor")]

namespace AggroBird.DebugConsole
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
        internal const string LogPrefix = "[DebugConsole]";

        internal static void Log(object msg)
        {
            Debug.Log($"{LogPrefix} {msg}");
        }
        internal static void LogWarning(object msg)
        {
            Debug.LogWarning($"{LogPrefix} {msg}");
        }
        internal static void LogError(object msg)
        {
            Debug.LogError($"{LogPrefix} {msg}");
        }

        public delegate void OnConsoleFocusChange(bool isFocused);
        public static event OnConsoleFocusChange onConsoleFocusChange = default;

#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE
        private static Instance instance;
        private static ConsoleGUI gui = new ConsoleGUI(false);
        private static bool isQuitting = false;
        private static Settings settings = null;
        internal static Settings Settings
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
        internal static void OverrideSettings(Settings settings)
        {
            DebugConsole.settings = settings;
        }

        private static ConsoleGUI GetInstance()
        {
            Initialize();

            return gui;
        }

        private static Settings LoadSettings()
        {
            Settings settings = Resources.Load<Settings>(SettingsFileName);

            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<Settings>();
            }

            return settings;
        }

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

        private static Dictionary<KeyCode, List<Macro>> macroTableInstance = null;
        internal static Dictionary<KeyCode, List<Macro>> MacroTable
        {
            get
            {
                if (macroTableInstance == null)
                {
                    macroTableInstance = new Dictionary<KeyCode, List<Macro>>();
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
                    if (macroTable.TryGetValue(macro.bind.code, out List<Macro> list))
                    {
                        list.Add(macro);
                    }
                    else
                    {
                        list = new List<Macro>();
                        list.Add(macro);
                        macroTable.Add(macro.bind.code, list);
                    }
                }
            }
        }

        private static IReadOnlyList<string>[] validNamespaces = null;
        private static string[] namespaceStrings = null;
        private static void EnsureNamespaces()
        {
            if (validNamespaces == null)
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
                validNamespaces = result.ToArray();

                // Build namespace strings
                StringBuilder builder = new StringBuilder();
                namespaceStrings = new string[validNamespaces.Length];
                for (int i = 0; i < validNamespaces.Length; i++)
                {
                    IReadOnlyList<string> validNamespace = validNamespaces[i];
                    for (int j = 0; j < validNamespace.Count; j++)
                    {
                        if (builder.Length > 0) builder.Append('.');
                        builder.Append(validNamespace[j]);
                    }
                    namespaceStrings[i] = builder.ToString();
                    builder.Clear();
                }
            }
        }
        private static IReadOnlyList<string>[] UsingNamespaces
        {
            get
            {
                EnsureNamespaces();
                return validNamespaces;
            }
        }
        internal static IReadOnlyList<string> UsingNamespacesString
        {
            get
            {
                EnsureNamespaces();
                return namespaceStrings;
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

        private static Task<Identifier> identifierTableTask = null;
        private static CancellationTokenSource identifierTableTokenSource = null;
        private static void BuildIdentifierTable()
        {
            if (identifierTableTask == null)
            {
                IdentifierTable = null;

#if UNITY_EDITOR
                UnityEditor.EditorApplication.update -= UpdateIdentifierTableTask;
                UnityEditor.EditorApplication.update += UpdateIdentifierTableTask;
#endif

                identifierTableTokenSource = new CancellationTokenSource();
                CancellationToken cancellationToken = identifierTableTokenSource.Token;
                IdentifierTableBuilder builder = new IdentifierTableBuilder(GetEnabledAssemblies(), UsingNamespaces, Settings.safeMode);
                if (!Application.isEditor && Application.platform == RuntimePlatform.WebGLPlayer)
                {
                    // WebGL does not support threading
                    IdentifierTable = builder.Build(cancellationToken);
                }
                else
                {
                    identifierTableTask = Task.Factory.StartNew(() => builder.Build(cancellationToken));
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
                    Exception exception = identifierTableTask.Exception;
                    if (exception != null)
                    {
                        Debug.LogException(exception);
                    }
                    if (taskStatus == TaskStatus.RanToCompletion)
                    {
                        IdentifierTable = identifierTableTask.Result;
                    }

                    identifierTableTokenSource.Dispose();
                    identifierTableTokenSource = null;
                    identifierTableTask = null;

#if UNITY_EDITOR
                    UnityEditor.EditorApplication.update -= UpdateIdentifierTableTask;
#endif
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

            if (IdentifierTable == null)
            {
                BuildIdentifierTable();
                return false;
            }

            return true;
        }
        internal static Identifier IdentifierTable { get; private set; }


        internal class Instance : MonoBehaviour
        {
            private GameViewReference gameViewReference = new GameViewReference();
            private bool hasFocus = false;

#if INCLUDE_DEBUG_SERVER
            private DebugServer server;

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
                    server.Update(out DebugServer.Message[] messages);
                    for (int i = 0; i < messages.Length; i++)
                    {
                        try
                        {
                            if (ExecuteCommand(messages[i].message, out object result, false, true))
                            {
                                // Send the output to the client
                                if (!(result != null && result.GetType() == typeof(VoidResult)))
                                {
                                    messages[i].sender.Send(result == null ? "Null" : ClampMaxSize(result.ToString(), DebugClient.MaxPackageSize), 0);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // Send the exception to the client
                            messages[i].sender.Send(ClampMaxSize(ex.ToString(), DebugClient.MaxPackageSize), 2);
                        }
                    }
                }
#endif
            }
            private static string ClampMaxSize(string msg, int len)
            {
                if (msg.Length > len)
                {
                    string append = " ...";
                    return msg.Substring(0, len - append.Length) + append;
                }
                return msg;
            }

            private void OnGUI()
            {
                if (instance == this)
                {
                    // Update UI
                    float gameViewScale = Settings.invertScale ? gameViewReference.GetGameViewScale() : 1;
                    gui.UpdateGUI(new Vector2(Screen.width, Screen.height), Settings.fontSize, scale * gameViewScale);

                    // Update focus callback
                    bool focus = gui.IsOpen && gui.HasFocus;
                    if (focus != hasFocus)
                    {
                        hasFocus = focus;
                        onConsoleFocusChange?.Invoke(hasFocus);
                    }
                }
            }

            private void OnDisable()
            {
                if (isQuitting) return;

                // Force reenable if disabled
                enabled = true;
            }
            private void OnDestroy()
            {
#if INCLUDE_DEBUG_SERVER
                StopDebugServer();
#endif

                // Clear instance reference
                if (instance == this) instance = null;

                // Create a new instance if destroyed
                if (!isQuitting) Initialize();
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

#if UNITY_EDITOR
        private static DebugClient client;

        private static void OpenConnection(string address, int port)
        {
            CloseConnection();

            client = new DebugClient(address, port, Settings.authenticationKey);

            UnityEditor.EditorApplication.update -= UpdateConnection;
            UnityEditor.EditorApplication.update += UpdateConnection;
        }
        private static void CloseConnection()
        {
            if (client != null)
            {
                client.Close();
                client = null;

                Log($"Connection closed");
            }

            UnityEditor.EditorApplication.update -= UpdateConnection;
        }

        private static void UpdateConnection()
        {
            if (client != null)
            {
                if (client.state == DebugClient.State.Disconnected)
                {
                    CloseConnection();
                    return;
                }

                if (client.Poll(out string message, out byte flags))
                {
                    switch (flags)
                    {
                        default: Debug.Log(message); break;
                        case 1: Debug.LogWarning(message); break;
                        case 2: Debug.LogError(message); break;
                    }
                }
            }
        }
#endif


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        internal static void Initialize()
        {
            if (!instance)
            {
                // Make sure quitting is bind
                Application.quitting -= Quitting;
                Application.quitting += Quitting;

#if UNITY_EDITOR
                UnityEditor.EditorApplication.quitting -= Quitting;
                UnityEditor.EditorApplication.quitting += Quitting;
#endif

                instance = UnityObject.FindObjectOfType<Instance>();
                if (!instance)
                {
                    // Create a new instance if we lost our previous
                    GameObject gameObject = GameObject.Find(GameObjectName);
                    if (!gameObject) gameObject = new GameObject(GameObjectName);
                    instance = gameObject.AddComponent<Instance>();
                    gameObject.hideFlags |= HideFlags.HideInHierarchy;
                    UnityObject.DontDestroyOnLoad(gameObject);
                }
                else
                {
                    // Attempt to reactivate existing instance
                    instance.enabled = true;
                }
            }
        }

        private static void Quitting()
        {
            isQuitting = true;
        }


        private static bool ExecuteCommand(string cmd, out object result, bool catchExceptions, bool isRemoteCommand = false)
        {
            result = VoidResult.Empty;

            if (cmd != null)
            {
                cmd = cmd.Trim();
                if (string.IsNullOrEmpty(cmd)) return false;
            }
            else
            {
                return false;
            }

#if UNITY_EDITOR
            // Handle console commands
            if (cmd.Length > 1 && cmd[0] == '/' && cmd[1] != ' ')
            {
                string[] args = cmd.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                switch (args[0])
                {
                    case "/open":
                    {
                        if (args.Length != 2)
                        {
                            LogError($"Invalid amount of arguments provided for command '{args[0]}'");
                            return false;
                        }

                        int portIdx = args[1].LastIndexOf(':');
                        if (portIdx == -1)
                        {
                            OpenConnection(args[1], Settings.serverPort);
                        }
                        else
                        {
                            if (portIdx == 0 || !int.TryParse(cmd.Substring(portIdx + 1), out int port))
                            {
                                LogError($"Invalid ip address format: '{args[1]}'");
                                return false;
                            }

                            OpenConnection(cmd, port);
                        }
                    }
                    break;

                    case "/close":
                    {
                        if (args.Length > 1)
                        {
                            LogError($"Invalid amount of arguments provided for command '{args[0]}'");
                            return false;
                        }

                        CloseConnection();
                    }
                    break;

                    case "/reload":
                    {
                        if (args.Length > 1)
                        {
                            LogError($"Invalid amount of arguments provided for command '{args[0]}'");
                            return false;
                        }

                        Reload();
                    }
                    break;

                    default:
                        LogError($"Unknown console command: '{args[0]}'");
                        break;
                }

                return true;
            }

            // Forward commands to the client if there is one
            if (!isRemoteCommand && !Application.isPlaying)
            {
                if (client != null)
                {
                    client.Send(cmd);
                    return true;
                }
            }
#endif

            try
            {
                Token[] tokens = new Lexer(cmd).ToArray();
                CommandParser commandParser = new CommandParser(tokens, IdentifierTable, Settings.safeMode, Settings.maxIterationCount);
                Command command = commandParser.Parse();
                result = command.Execute();
                return true;
            }
            catch (Exception exception)
            {
                if (catchExceptions)
                {
                    HandleException(exception);
                }
                else
                {
                    throw exception;
                }
            }

            return false;
        }
        private static void HandleException(Exception ex)
        {
            if (ex is TargetInvocationException targetInvocationException)
            {
                ex = targetInvocationException.InnerException;
            }

            if (ex is DebugConsoleException)
            {
                Debug.LogError(ex.Message);
            }
            else if (ex != null)
            {
                Debug.LogException(ex);
            }
        }


        public static void Open() => GetInstance().Open();
        public static void Close() => GetInstance().Close();

        public static bool Execute(string cmd)
        {
            return ExecuteCommand(cmd, out _, true);
        }
        public static bool Execute(string cmd, out object result)
        {
            return ExecuteCommand(cmd, out result, true);
        }

        public static bool IsOpen => GetInstance().IsOpen;
        public static bool HasFocus => GetInstance().HasFocus;

        public static void Reload()
        {
            macroTableInstance = null;
            validNamespaces = null;
            assemblies = null;

            CancelIdentifierTableTask();
            IdentifierTable = null;

            settings = LoadSettings();
        }
        internal static void ReloadMacroTable()
        {
            macroTableInstance = null;
        }

#else
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
