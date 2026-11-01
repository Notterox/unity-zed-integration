using System.IO;
using System.Security;
using UnityEditor;
using UnityEditor.Compilation;

namespace UnityZedIntegration
{
    /// <summary>
    /// Adds the package's Roslyn analyzers (Analyzers~) to the C# projects generated for the IDE.
    /// For the namespace analyzer (UZI0001) it also passes each assembly's root folder, the folder
    /// of its .asmdef or Assets for the predefined assemblies, and turns off Roslyn's IDE0130,
    /// which counts folders from the .csproj in the project root. Unity's own compilation is not
    /// affected.
    /// </summary>
    class ZedProjectFiles : AssetPostprocessor
    {
        const string AnalyzerFile = "Analyzers~/UnityZedIntegration.Analyzers.dll";

        static string OnGeneratedCSProject(string path, string content)
        {
            var analyzer = analyzerPath();
            var root = assemblyRootFolder(Path.GetFileNameWithoutExtension(path));
            var end = content.LastIndexOf("</Project>", System.StringComparison.Ordinal);
            if (analyzer == null || root == null || end < 0)
                return content;

            var newline = content.Contains("\r\n") ? "\r\n" : "\n";
            var addition = string.Join(newline,
                "  <!-- Added by com.notterox.unity-zed-integration: namespace analyzer (UZI0001). -->",
                "  <PropertyGroup>",
                $"    <UnityZedNamespaceRoot>{SecurityElement.Escape(root)}</UnityZedNamespaceRoot>",
                "    <NoWarn>$(NoWarn);IDE0130</NoWarn>",
                "  </PropertyGroup>",
                "  <ItemGroup>",
                "    <CompilerVisibleProperty Include=\"UnityZedNamespaceRoot\" />",
                $"    <Analyzer Include=\"{SecurityElement.Escape(analyzer)}\" />",
                "  </ItemGroup>",
                "");
            return content.Insert(end, addition);
        }

        static string analyzerPath()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ZedProjectFiles).Assembly);
            if (package == null)
                return null;
            var path = Path.GetFullPath(Path.Combine(package.resolvedPath, AnalyzerFile));
            return File.Exists(path) ? path : null;
        }

        /// <summary>The absolute folder an assembly's namespaces are counted from.</summary>
        static string assemblyRootFolder(string projectName)
        {
            // Player projects (project-player.csproj) share the editor assembly's sources.
            var assemblyName = projectName.EndsWith(".Player")
                ? projectName.Substring(0, projectName.Length - ".Player".Length)
                : projectName;
            var asmdef = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assemblyName);
            if (string.IsNullOrEmpty(asmdef))
                return assemblyName.StartsWith("Assembly-CSharp") ? Path.GetFullPath("Assets") : null;
            var folder = Path.GetDirectoryName(asmdef)?.Replace('\\', '/');
            if (folder == null)
                return null;
            if (!folder.StartsWith("Packages/"))
                return Path.GetFullPath(folder);

            // Only packages whose sources can be edited; registry, git and built-in packages are
            // read-only copies in Library/PackageCache.
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(folder);
            if (package == null
                || (package.source != UnityEditor.PackageManager.PackageSource.Embedded
                    && package.source != UnityEditor.PackageManager.PackageSource.Local))
                return null;
            // "Packages/name/..." is virtual; file: packages live elsewhere on disk.
            var rest = folder.Substring(package.assetPath.Length).TrimStart('/');
            return Path.GetFullPath(Path.Combine(package.resolvedPath, rest));
        }
    }
}
