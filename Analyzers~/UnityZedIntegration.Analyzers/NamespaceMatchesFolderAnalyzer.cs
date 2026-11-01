using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace UnityZedIntegration.Analyzers
{
    /// <summary>
    /// UZI0001: the file's namespace differs from the one <see cref="NamespaceRules"/> expects.
    /// Replaces Roslyn's IDE0130, which counts folders from the .csproj (always the Unity project
    /// root) and can't skip or rename folders.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class NamespaceMatchesFolderAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "UZI0001";
        public const string ExpectedNamespaceKey = "ExpectedNamespace";

        static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            title: "Namespace does not match folder structure",
            messageFormat: "Namespace \"{0}\" does not match folder structure, expected \"{1}\"",
            category: "Style",
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: "The expected namespace is the assembly's root namespace followed by the folders "
                + "below its .asmdef (or Assets). Skip folders with unity_namespace_ignored_folders and "
                + "rename them with unity_namespace_renamed_folders (Folder:Segment) in .editorconfig.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxTreeAction(AnalyzeTree);
        }

        static void AnalyzeTree(SyntaxTreeAnalysisContext context)
        {
            var global = context.Options.AnalyzerConfigOptionsProvider.GlobalOptions;
            if (!global.TryGetValue(NamespaceRules.RootFolderProperty, out var rootFolder)
                || string.IsNullOrWhiteSpace(rootFolder))
            {
                return;
            }
            global.TryGetValue(NamespaceRules.RootNamespaceProperty, out var rootNamespace);

            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Tree);
            options.TryGetValue(NamespaceRules.IgnoredFoldersKey, out var ignoredFolders);
            options.TryGetValue(NamespaceRules.RenamedFoldersKey, out var renamedFolders);

            var expected = NamespaceRules.ExpectedNamespace(
                context.Tree.FilePath, rootFolder, rootNamespace, ignoredFolders, renamedFolders);
            if (expected is null)
            {
                return;
            }

            // Only files with exactly one top-level namespace: files without one (AssemblyInfo.cs)
            // or with several are left alone.
            var root = (CompilationUnitSyntax)context.Tree.GetRoot(context.CancellationToken);
            var namespaces = root.Members.OfType<BaseNamespaceDeclarationSyntax>().Take(2).ToList();
            if (namespaces.Count != 1)
            {
                return;
            }
            var name = namespaces[0].Name;
            var declared = string.Concat(name.ToString().Where(c => !char.IsWhiteSpace(c)));
            if (declared == expected)
            {
                return;
            }

            var properties = ImmutableDictionary<string, string?>.Empty.Add(ExpectedNamespaceKey, expected);
            context.ReportDiagnostic(Diagnostic.Create(Rule, name.GetLocation(), properties, declared, expected));
        }
    }
}
