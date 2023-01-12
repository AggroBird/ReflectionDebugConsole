# Reflection Debug Console

Repository for maintaining the debug console plugin that I use in client projects. This plugin is continuously under development and is not optimized for production. Use at your own risk.

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

By default, the console will only scan exposed public classes and members. Searching can be expanded into unexposed private members by turning off "Safe Mode" in the console's editor settings window. Accessing unexposed private code can cause damage to your editor environment. Make sure to only disable this setting when you are aware of the consequences.

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
UnityEngine.Object.FindObjectsOfType(typeof(Camera))[0].ToString();
```

Constructing new objects:
```
Game.player.transform.position = UnityEngine.Vector3(1, 2, 3);
```

Multiple commands can be chained together within one execution by separating them with a semicolon. For singular commands, the semicolon is optional.
```
EditorApplication.isPlaying = true; EditorApplication.isPaused = true;
```

The debug console try to pick the best matching overload when multiple are available, but not as strict as the C# language, so make sure to avoid ambiguity in debug commands.

The environment supports all C# basetypes and build-in operators between them.

## Macros and Keybinds

The console comes with an interface to store frequently called commands as macros. These can be executed with a keybind, eliminating the need to program these keybinds into your project.

These macros are stored in an asset file and can be checked into source control. Besides project-wide macros, you can also define local macros, which get stored in your local player prefs.

![alt text](https://github.com/AggroBird/ReflectionDebugConsole/blob/main/macro.png?raw=true "Macro")

## Environment and Utility

The console can also be invoked via script (see DebugConsole.Execute). By default, the console will not disable game input when focused. This has to be implemented by the game (see DebugConsole.hasFocus and DebugConsole.onConsoleFocusChange).

You can have specific namespaces be skipped in the search by adding them to the "Using Namespaces" list in the console settings window. Commonly excluded namespaces include "UnityEngine", "System" and namespaces from your own project.

The console ships with a debug server that can accept commands remotely. To enable this in your build, add the INCLUDE_DEBUG_SERVER define. Connections can be established from the editor using the ```/open <ip address>``` command.

## Console Scaling

The console's font size can be changed in the settings. For the editor specifically, it is possible to run the game at a higher resolution than the editor's game view window size, causing the UI to scale down and making it hard to read. To resolve this, a setting called "Invert Scale" has been added which calculates the font size using screen resolution rather than game view resolution.

The DebugConsole class exposes a public property (see DebugConsole.scale) which allows you to implement your own custom scaling logic.

## Excluding the Console from Builds

The console can be excluded by either disabling the specific platform in the console's assembly definition, or by defining "EXCLUDE_DEBUG_CONSOLE" in your project's scripting define symbols. Using the latter will switch the console's public methods to empty implementations which do nothing when invoked, removing the need to wrap these calls with defines in your own code. To build the console along with standalone builds, use the "INCLUDE_DEBUG_CONSOLE" define. "EXCLUDE_DEBUG_CONSOLE" will always have precedence over "INCLUDE_DEBUG_CONSOLE".

## Limitations

- **No generics**: The debug console currently does not support C# generics. Generic classes will show up in the assembly search, and fields using generic types can be interacted with, but it is not possible to construct new classes that use generic parameters.
- **Limited support in IL2CPP**: The debug console is not intended for IL2CPP builds due to reflection limitations. The console may have its search scope limited in IL2CPP builds due to assembly stripping, or may not work at all.