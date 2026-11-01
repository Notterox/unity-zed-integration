# Unity Zed Integration (UZI)

A lightweight package to integrate the [Zed editor](https://zed.dev) as your external script editor in Unity.

> *Initially forked from [Unity-zed](https://github.com/maligan/unity-zed). Forked to add Windows support.*

---

## Supported Platforms

* **Windows**
* **macOS**
* **Linux**

---

## Installation

You can install this package directly through the Unity Package Manager using the GitHub repository URL:

1. Open Unity and navigate to **Window > Package Management > Package Manager**.
2. Click the **+** (plus) button in the top left corner of the window.
3. Select **Add package from git URL...**
4. Paste the following link and click **Add**:

```text
https://github.com/SuperJura/unity-zed-integration.git
```

---

## Namespace check (UZI0001)

Roslyn's own check (IDE0130) counts folders from the `.csproj`, which Unity always puts in the
project root, so it flags almost every file. The package replaces it in the generated projects
with UZI0001: a file's namespace should be its assembly's root namespace (the asmdef's
`rootNamespace`) followed by the folders below the asmdef (or below `Assets` for
`Assembly-CSharp`). The code fix "Change namespace to 'X'" renames the file's namespace
declaration; references in other files are not updated.

Folders that only organize files can be skipped, and folders can be renamed, in `.editorconfig`
(folder names, case-insensitive; nested `.editorconfig` files override per folder tree):

```ini
[*.cs]
unity_namespace_ignored_folders = Scripts, Math, Systems
unity_namespace_renamed_folders = Diagnostics:Diag, Tools:Editor.Tools
```

Turn it off for some files like any diagnostic, e.g. `dotnet_diagnostic.UZI0001.severity = none`.
It applies to assemblies in `Assets` and in embedded or local packages; read-only registry, git
and built-in packages are left alone. Unity's own compilation is not affected.

The analyzer's source is in `Analyzers~` (ignored by Unity); `Analyzers~/build.sh` runs its
checks and rebuilds the committed `Analyzers~/UnityZedIntegration.Analyzers.dll` (.NET SDK).
