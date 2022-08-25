// Copyright, 2022, AggrobirdGK

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
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

        public delegate void OnConsoleFocusChange(bool isFocused);
        public static OnConsoleFocusChange onConsoleFocusChange = default;

#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE
        public static void Open() => GetInstance().OpenConsole();
        public static void Close() => GetInstance().CloseConsole();

        public static bool Execute(string cmd)
        {
            return ExecuteCommand(cmd, out object _);
        }
        public static bool Execute(string cmd, out object result)
        {
            return ExecuteCommand(cmd, out result);
        }

        public static bool isOpen => GetInstance().isConsoleOpen;
        public static bool hasFocus => GetInstance().hasConsoleFocus;

        public static void Reload()
        {
            settings = LoadSettings();

            ClearCache();
        }
        internal static void ClearCache()
        {
            ReloadAssemblies();
            ReloadMacroTable();
            ReloadNamespaces();
        }


        internal static Instance instance { get; private set; }
        private static ConsoleGUI GetInstance()
        {
            Initialize();

            return gui;
        }

        private static ConsoleGUI gui = new ConsoleGUI(false);


        internal class Instance : MonoBehaviour
        {
            private bool hasFocus = false;

            private void OnGUI()
            {
                Initialize();

                if (instance != this)
                {
                    Destroy(this);
                    return;
                }

                gui.UpdateGUI(new Vector2(Screen.width, Screen.height), settings.fontSize, scale * gameViewReference.GetGameViewScale());

                bool focus = gui.isConsoleOpen && gui.hasConsoleFocus;
                if (focus != hasFocus)
                {
                    hasFocus = focus;
                    onConsoleFocusChange?.Invoke(hasFocus);
                }
            }

            private GameViewReference gameViewReference = new GameViewReference();


            private class GameViewReference
            {
                public GameViewReference()
                {
                    try
                    {
                        gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor", false);
                        if (gameViewType != null)
                        {
                            positionInfo = gameViewType.GetProperty("position", ReflectionUtility.PrivateInstanceFlags);
                        }
                    }
                    catch (Exception)
                    {

                    }
                }

                public float GetGameViewScale()
                {
                    if (positionInfo != null && settings.invertScale)
                    {
                        try
                        {
                            bool hasGameView = gameViewWindow;

                            if (!hasGameView)
                            {
                                foreach (var window in Resources.FindObjectsOfTypeAll(gameViewType))
                                {
                                    if (window is ScriptableObject scriptableObject)
                                    {
                                        gameViewWindow = scriptableObject;
                                        hasGameView = true;
                                    }
                                }
                            }

                            if (hasGameView)
                            {
                                Rect gameViewRect = (Rect)positionInfo.GetValue(gameViewWindow);
                                float vw = gameViewRect.width;
                                float vh = gameViewRect.height - 17;
                                float sw = Screen.width;
                                float sh = Screen.height;
                                float va = Mathf.Max(vh, 1) / Mathf.Max(vw, 1);
                                float sa = Mathf.Max(sh, 1) / Mathf.Max(sw, 1);
                                return (va > sa) ? (sw / vw) : (sh / vh);
                            }
                        }
                        catch (Exception)
                        {

                        }
                    }

                    return 1;
                }

                private Type gameViewType;
                private PropertyInfo positionInfo;
                private ScriptableObject gameViewWindow;
            }

            private void OnDisable()
            {
                if (isQuitting) return;

                enabled = true;
            }
            private void OnDestroy()
            {
                if (isQuitting) return;

                if (instance == this)
                    instance = null;

                Initialize();
            }
        }

        private static bool isQuitting = false;

        private static void Quitting()
        {
            isQuitting = true;
        }


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        internal static void Initialize()
        {
            if (!instance)
            {
                Application.quitting -= Quitting;
                Application.quitting += Quitting;

                instance = UnityObject.FindObjectOfType<Instance>();
                if (!instance)
                {
                    string instanceKey = $"{UniqueKey}.{InstanceKey}";
                    GameObject gameObject = GameObject.Find(instanceKey);
                    if (!gameObject) gameObject = new GameObject(instanceKey);
                    instance = gameObject.AddComponent<Instance>();
                    gameObject.hideFlags |= HideFlags.HideInHierarchy;
                    UnityObject.DontDestroyOnLoad(gameObject);
                }
                else
                {
                    instance.enabled = true;
                }
            }
        }

        internal static void SaveHistory()
        {
            SavePrefs(HistoryKey, new ListObject<string>(history));
        }
        private static List<string> LoadHistory()
        {
            return LoadPrefs<ListObject<string>>(HistoryKey);
        }


        internal static DebugSettings settings = LoadSettings();
        private static DebugSettings LoadSettings()
        {
            DebugSettings settings = Resources.Load<DebugSettings>(SettingsFileName);

            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<DebugSettings>();
            }

            return settings;
        }

        private static List<Assembly> assemblies = null;
        internal static Assembly[] MakeAssemblyArray()
        {
            if (assemblies == null)
            {
                assemblies = new List<Assembly>();
                Dictionary<string, LoadAssembly> loadAssemblies = new Dictionary<string, LoadAssembly>();
                foreach (var loadAssembly in settings.assemblies)
                {
                    loadAssemblies.Add(loadAssembly.name, loadAssembly);
                }

                if (settings.loadAllAssemblies)
                {
                    foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        // Excluded
                        if (loadAssemblies.TryGetValue(assembly.GetName().Name, out LoadAssembly loadAssembly) && loadAssembly.enabled)
                        {
                            continue;
                        }

                        assemblies.Add(assembly);
                    }
                }
                else
                {
                    foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        // Included
                        if (loadAssemblies.TryGetValue(assembly.GetName().Name, out LoadAssembly loadAssembly) && loadAssembly.enabled)
                        {
                            assemblies.Add(assembly);
                        }
                    }
                }
            }
            return assemblies.ToArray();
        }
        internal static void ReloadAssemblies()
        {
            assemblies = null;
        }

        private static void AppendMacroTable(List<Macro> macros)
        {
            if (macros != null)
            {
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

        internal static Dictionary<KeyCode, List<Macro>> macroTable
        {
            get
            {
                if (macroTableInstance == null)
                {
                    macroTableInstance = new Dictionary<KeyCode, List<Macro>>();
                    AppendMacroTable(settings.sharedMacros);
                    AppendMacroTable(Macros.localMacros);
                }
                return macroTableInstance;
            }
        }
        private static Dictionary<KeyCode, List<Macro>> macroTableInstance = null;
        internal static void ReloadMacroTable()
        {
            macroTableInstance = null;
        }

        private static List<string> validNamespaces = null;
        internal static string[] MakeValidNamespaceArray()
        {
            if (validNamespaces == null)
            {
                HashSet<string> uniqueNamespaces = new HashSet<string>();
                foreach (string ns in settings.namespaces)
                {
                    string[] tokens = ns.Split('.');
                    foreach (string token in tokens)
                    {
                        if (!IsValidNamespace(token))
                            goto Skip;
                    }

                    if (uniqueNamespaces.Contains(ns)) continue;
                    uniqueNamespaces.Add(ns);
                Skip:
                    continue;
                }

                validNamespaces = new List<string>();
                foreach (string ns in uniqueNamespaces)
                {
                    validNamespaces.Add(ns);
                }
            }
            return validNamespaces.ToArray();
        }
        private static bool IsValidNamespace(string str)
        {
            if (str == null || str.Length == 0) return false;
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
        internal static void ReloadNamespaces()
        {
            validNamespaces = null;
        }


        internal static readonly List<string> history = LoadHistory();
        private static Command command = new Command();

        private static bool ExecuteCommand(string cmd, out object result)
        {
            result = typeof(void);

            if (cmd != null)
            {
                cmd = cmd.Trim();
                if (string.IsNullOrEmpty(cmd)) return false;
            }
            else
            {
                return false;
            }

            try
            {
                if (command.Parse(cmd) && command.Interpret(MakeAssemblyArray(), MakeValidNamespaceArray(), settings.safeMode) && command.Execute(out result))
                {
                    return true;
                }
                else if (command.exception != null)
                {
                    HandleException(command.exception);
                }
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }

            return false;
        }
        private static void HandleException(Exception ex)
        {
            if (ex is TargetInvocationException targetInvocationException)
            {
                ex = targetInvocationException.InnerException;
            }

            if (ex is ConsoleException)
            {
                Debug.LogError(ex.Message);
            }
            else if (ex != null)
            {
                Debug.LogException(ex);
            }
        }

#else
        public static void Open() { }
        public static void Close() { }

        public static bool Execute(string cmd)
        {
            return false;
        }
        public static bool Execute(string cmd, out object result)
        {
            result = null;
            return false;
        }

        public const bool isOpen = false;
        public const bool hasFocus = false;

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