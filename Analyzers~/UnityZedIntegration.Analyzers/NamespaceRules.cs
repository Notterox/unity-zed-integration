using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.CodeAnalysis.CSharp;

namespace UnityZedIntegration.Analyzers
{
    /// <summary>
    /// Computes the namespace a file is expected to have in a Unity project: the assembly's root
    /// namespace followed by the folders between the assembly's root folder (the folder of its
    /// .asmdef, or Assets for the predefined assemblies) and the file, where folders can be
    /// ignored or renamed in .editorconfig.
    /// </summary>
    internal static class NamespaceRules
    {
        /// <summary>The assembly's root folder, an absolute path set by the package in the .csproj.</summary>
        public const string RootFolderProperty = "build_property.UnityZedNamespaceRoot";

        /// <summary>The .csproj RootNamespace (the asmdef's rootNamespace).</summary>
        public const string RootNamespaceProperty = "build_property.RootNamespace";

        /// <summary>.editorconfig key: comma-separated folder names that add no namespace segment.</summary>
        public const string IgnoredFoldersKey = "unity_namespace_ignored_folders";

        /// <summary>.editorconfig key: comma-separated <c>Folder:Segment</c> pairs.</summary>
        public const string RenamedFoldersKey = "unity_namespace_renamed_folders";

        /// <summary>
        /// The expected namespace of the file at <paramref name="filePath"/>, or null when it can't be
        /// computed: the file is outside <paramref name="rootFolder"/>, a folder that would become a
        /// segment isn't a valid identifier, or there would be no segments at all.
        /// </summary>
        public static string? ExpectedNamespace(
            string filePath,
            string rootFolder,
            string? rootNamespace,
            string? ignoredFolders,
            string? renamedFolders)
        {
            var root = Normalize(rootFolder).TrimEnd('/') + "/";
            var directory = Normalize(Path.GetDirectoryName(filePath) ?? "") + "/";
            if (!directory.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var ignored = new HashSet<string>(ParseList(ignoredFolders), StringComparer.OrdinalIgnoreCase);
            var renamed = ParseRenames(renamedFolders);

            var segments = new List<string>();
            AddSegments(segments, rootNamespace);
            var folders = directory.Substring(root.Length)
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var folder in folders)
            {
                if (ignored.Contains(folder))
                {
                    continue;
                }
                if (renamed.TryGetValue(folder, out var replacement))
                {
                    AddSegments(segments, replacement);
                }
                else
                {
                    segments.Add(folder);
                }
            }

            // No segments would mean the global namespace, which isn't a useful expectation.
            if (segments.Count == 0)
            {
                return null;
            }
            foreach (var segment in segments)
            {
                if (!SyntaxFacts.IsValidIdentifier(segment))
                {
                    return null;
                }
            }
            return string.Join(".", segments);
        }

        static void AddSegments(List<string> segments, string? dottedName)
        {
            if (dottedName is null)
            {
                return;
            }
            foreach (var part in dottedName.Split('.'))
            {
                var trimmed = part.Trim();
                if (trimmed.Length > 0)
                {
                    segments.Add(trimmed);
                }
            }
        }

        static IEnumerable<string> ParseList(string? value)
        {
            if (value is null)
            {
                yield break;
            }
            foreach (var item in value.Split(','))
            {
                var trimmed = item.Trim();
                if (trimmed.Length > 0)
                {
                    yield return trimmed;
                }
            }
        }

        static Dictionary<string, string> ParseRenames(string? value)
        {
            var renames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in ParseList(value))
            {
                var separator = pair.IndexOf(':');
                if (separator <= 0)
                {
                    continue;
                }
                renames[pair.Substring(0, separator).Trim()] = pair.Substring(separator + 1).Trim();
            }
            return renames;
        }

        static string Normalize(string path) => path.Replace('\\', '/');
    }
}
