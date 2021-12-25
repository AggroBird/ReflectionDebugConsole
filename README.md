# Reflection Debug Console

Repository for maintaining the debug console plugin that I use in client projects. This plugin is continuously under development and is not optimized for production. Include at your own risk.

![alt text](https://github.com/AggroBird/ReflectionDebugConsole/blob/main/example.png?raw=true "Debug Console")

## Feature Overview

- Invoke any method or modify any field at runtime
- Support for basic C# types (strings, ints, floats, booleans, enums etc.)
- Support for indexing arrays and invoking delegates
- Chain multiple commands together within one single execution
- Extensive autocomplete with suggestions for types and members
- Keeps a history of recently executed commands which can be recalled
- Store commands as macros with keybinds for ingame debugging
- Run separate instances in editor windows for calling editor methods
- Set up using namespaces for immediate access to frequently used types
- Include or exclude specific assemblies from scanning
- Modify console keybinds to your preference

## General Description

Modifying script in Unity Editor while the game is running can be perilous, as it often resets references and it can break the state. The Reflection Debug Console can help you with debugging by allowing you to call methods without the need for script modification.

The Reflection Debug Console uses C#'s reflection facilities to expose methods and fields to you at runtime. Anything can be invoked, including .Net classes, methods from Unity (both editor and runtime) and methods in your own script, no boilerplate required.

By default, the console will only scan exposed public classes and members. Searching can be expanded into unexposed private members by turning off "Safe Mode" in the console's editor settings window. 
WARNING: Accessing unexposed private code can cause damage to your editor environment. Make sure to only disable this setting when you are aware of the consequences.

The settings can be accessed in the editor through Window > Analysis > Debug Console Settings.

## Syntax and Usage

The console supports most basic C# syntax, including:

Invoking methods (including variable number parameter):
```
UnityEngine.Debug.Log("Foo");
```
By default, the command’s return value is displayed in the console (if not void).

Getting and setting fields and properties:
```
UnityEngine.Time.timeScale = 2.0;
```
This can only be done with top level commands. Assignment cannot be nested as it has no return value.

Getting elements of an array:
```
UnityEngine.Object.FindObjectsOfType(Camera)[0].enabled;
```

Constructing new objects:
```
Game.player.transform.position = UnityEngine.Vector3(1, 2, 3);
```

Multiple commands can be chained together within one execution by separating them with a semicolon. For singular commands, the semicolon is optional.
```
EditorApplication.isPlaying = true; EditorApplication.isPaused = true;
```

The debug console will pick method overloads very liberally, stopping the search at the first method which will take the supplied parameters (either directly or through conversion). Make sure that you avoid ambiguity in your debug method parameter types.

By default, number literals will be parsed as integers. If the literal value does not fit within integer range or contains floating point notation, C#'s Decimal class will be used. Strings can be declared using double quotes and characters using single quotes. Conversion between value types is always implicit, and C#'s type casting rules are not enforced.

The environment contains a "typeof" method to get the type of the specified object, but this is optional for method parameters. The following two calls produce the same result:
```
UnityEngine.Object.FindObjectOfType(UnityEngine.Camera);
UnityEngine.Object.FindObjectOfType(typeof(UnityEngine.Camera));
```

The environment exposes a method called "cast" for type casting. This is useful for methods that return a base type, like FindObjectOfType.
```
cast(UnityEngine.Object.FindObjectOfType(UnityEngine.Camera), UnityEngine.Camera);
```
For this case in particular, the environment contains a "find" method which calls FindObjectOfType, but casts the return value to the specified type:
```
find(Camera);
```

The console allows you to store runtime variables, which can be shared between commands within the same execution. These variables are cleared after the execution.
```
$foo = 5; UnityEngine.Debug.Log($foo);
```

## Macros and Keybinds

The console comes with an interface to store frequently called commands as macros. These can be executed with a keybind, eliminating the need to program these keybinds into your project.

These macros are stored in an asset file and can be checked into source control. Besides project-wide macros, you can also define local macros, which get stored in your local player prefs.

![alt text](https://github.com/AggroBird/ReflectionDebugConsole/blob/main/macro.png?raw=true "Macro")

## Environment and Utility

The console comes with a small set of helper functions to make debugging easier. This is located in one single script file and can be extended for your particular project (see Environment.cs).

Static properties defined in this class will be interpreted as custom keywords, and static methods will be defined as helper functions. These functions will show up outside of any namespaces for quick access.

The console can also be invoked via script (see DebugConsole.Execute). By default, the console will not disable game input when focused. This has to be implemented by the game (see DebugConsole.hasFocus and DebugConsole.onConsoleFocusChange).

You can have specific namespaces be skipped in the search by adding them to the "Using Namespaces" list in the console settings window. Commonly excluded namespaces include "UnityEngine", "System" and namespaces from your own project.

## Console Scaling

The console's font size can be changed in the settings. For the editor specifically, it is possible to run the game at a higher resolution than the editor's game view window size, causing the UI to scale down and making it hard to read. To resolve this, a setting called "Invert Scale" has been added which calculates the font size using screen resolution rather than game view resolution.

The DebugConsole class exposes a public property (see DebugConsole.scale) which allows you to implement your own custom scaling logic.

## Excluding the Console from Builds

The console can be excluded by either disabling the specific platform in the console's assembly definition, or by defining "NO_DEBUG_CONSOLE" in your project's scripting define symbols. Using the latter will switch the console's public methods to empty implementations which do nothing when invoked, removing the need to wrap these calls with defines in your own code.

## Limitations

- **Not a scripting environment**: The debug console is not meant to be a scripting environment. It can only invoke existing methods and fields, and it has no control flow. Advanced debugging code should still be implemented in your project.
- **No generics**: The debug console currently does not support C# generics. Generic classes will show up in the assembly search, and fields using generic types can be interacted with, but it is not possible to construct new classes that use generic parameters.
- **Limited support in IL2CPP**: The debug console is not intended for IL2CPP builds due to reflection limitations. The console in IL2CPP builds may have its search scope limited due to assembly stripping, or may not work at all.