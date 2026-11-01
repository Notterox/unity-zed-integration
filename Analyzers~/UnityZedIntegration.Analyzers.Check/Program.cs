using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using UnityZedIntegration.Analyzers;

const string Root = @"C:\Project\Packages\com.example\Runtime";

var failures = 0;

void Check(string name, bool passed, string details)
{
    Console.WriteLine($"{(passed ? "PASS" : "FAIL")} {name}{(passed ? "" : $": {details}")}");
    if (!passed)
    {
        failures++;
    }
}

// The diagnostics UZI0001 reports for one file, as "declared -> expected".
async Task<string[]> Analyze(string path, string source, string? rootNamespace = "Example.Anim",
    string? ignored = null, string? renamed = null, string root = Root)
{
    var tree = CSharpSyntaxTree.ParseText(source, path: path);
    var compilation = CSharpCompilation.Create("Test", new[] { tree },
        new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    var global = new Dictionary<string, string> { [NamespaceRules.RootFolderProperty] = root };
    if (rootNamespace is not null)
    {
        global[NamespaceRules.RootNamespaceProperty] = rootNamespace;
    }
    var file = new Dictionary<string, string>();
    if (ignored is not null)
    {
        file[NamespaceRules.IgnoredFoldersKey] = ignored;
    }
    if (renamed is not null)
    {
        file[NamespaceRules.RenamedFoldersKey] = renamed;
    }
    var options = new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty, new OptionsProvider(global, file));
    var diagnostics = await compilation
        .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new NamespaceMatchesFolderAnalyzer()), options)
        .GetAnalyzerDiagnosticsAsync();
    return diagnostics
        .Where(d => d.Id == NamespaceMatchesFolderAnalyzer.DiagnosticId)
        .Select(d => d.GetMessage())
        .ToArray();
}

string Expect(params string[] messages) => string.Join(" | ", messages);

async Task Case(string name, Task<string[]> actual, params string[] expected)
{
    var messages = await actual;
    Check(name, messages.SequenceEqual(expected), $"got [{Expect(messages)}], expected [{Expect(expected)}]");
}

static string Mismatch(string declared, string expected) =>
    $"Namespace \"{declared}\" does not match folder structure, expected \"{expected}\"";

await Case("folder adds a segment",
    Analyze($@"{Root}\Math\A.cs", "namespace Example.Anim { class A {} }"),
    Mismatch("Example.Anim", "Example.Anim.Math"));

await Case("ignored folder adds nothing",
    Analyze($@"{Root}\Math\A.cs", "namespace Example.Anim { class A {} }", ignored: "Pose, Math"));

await Case("ignored folders match case-insensitively",
    Analyze($@"{Root}\Math\A.cs", "namespace Example.Anim { class A {} }", ignored: "math"));

await Case("renamed folder uses its segment",
    Analyze($@"{Root}\Diagnostics\Reports\B.cs", "namespace Example.Anim.Diag.Reports;",
        renamed: "Diagnostics:Diag"));

await Case("rename to a dotted name adds several segments",
    Analyze($@"{Root}\Tools\B.cs", "namespace Example.Anim;", renamed: "Tools:Editor.Tools"),
    Mismatch("Example.Anim", "Example.Anim.Editor.Tools"));

await Case("rename to nothing works like ignore",
    Analyze($@"{Root}\Scripts\B.cs", "namespace Example.Anim;", renamed: "Scripts:"));

await Case("file in the root folder",
    Analyze($@"{Root}\C.cs", "namespace Wrong { }"),
    Mismatch("Wrong", "Example.Anim"));

await Case("no root namespace",
    Analyze($@"{Root}\Math\C.cs", "namespace Example.Anim { }", rootNamespace: null),
    Mismatch("Example.Anim", "Math"));

await Case("no root namespace and no folders is skipped",
    Analyze($@"{Root}\C.cs", "namespace Example.Anim { }", rootNamespace: null));

await Case("file outside the root folder is skipped",
    Analyze(@"C:\Project\Assets\Other\D.cs", "namespace Wrong { }"));

await Case("folder that isn't an identifier is skipped",
    Analyze($@"{Root}\com.vendor\E.cs", "namespace Wrong { }"));

await Case("file without a namespace is skipped",
    Analyze($@"{Root}\Math\F.cs", "class F {}"));

await Case("file with two namespaces is skipped",
    Analyze($@"{Root}\Math\G.cs", "namespace A { } namespace B { }"));

await Case("forward slashes and a trailing slash on the root",
    Analyze("C:/Project/Packages/com.example/Runtime/Math/H.cs", "namespace Example.Anim.Math { }",
        root: Root.Replace('\\', '/') + "/"));

// The code fix replaces the namespace name and keeps the rest of the file.
async Task<string> Fix(string path, string source)
{
    var tree = CSharpSyntaxTree.ParseText(source, path: path);
    var compilation = CSharpCompilation.Create("Test", new[] { tree },
        new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
    var global = new Dictionary<string, string>
    {
        [NamespaceRules.RootFolderProperty] = Root,
        [NamespaceRules.RootNamespaceProperty] = "Example.Anim",
    };
    var options = new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty,
        new OptionsProvider(global, new Dictionary<string, string>()));
    var diagnostic = (await compilation
        .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new NamespaceMatchesFolderAnalyzer()), options)
        .GetAnalyzerDiagnosticsAsync()).Single();

    using var workspace = new AdhocWorkspace();
    var project = workspace.AddProject("Test", LanguageNames.CSharp);
    var document = workspace.AddDocument(DocumentInfo.Create(
        DocumentId.CreateNewId(project.Id), "file.cs",
        loader: TextLoader.From(TextAndVersion.Create(SourceText.From(source), VersionStamp.Create())),
        filePath: path));
    CodeAction? fix = null;
    var context = new CodeFixContext(document, diagnostic, (action, _) => fix ??= action, CancellationToken.None);
    await new ChangeNamespaceCodeFixProvider().RegisterCodeFixesAsync(context);
    if (fix is null)
    {
        return "<no fix offered>";
    }
    var operation = (await fix.GetOperationsAsync(CancellationToken.None)).OfType<ApplyChangesOperation>().Single();
    var changed = operation.ChangedSolution.GetDocument(document.Id)!;
    return $"{fix.Title} => {(await changed.GetTextAsync()).ToString()}";
}

var blockFix = await Fix($@"{Root}\Math\I.cs", "namespace Wrong.Name\n{\n    class I {}\n}\n");
Check("fix renames a block namespace",
    blockFix == "Change namespace to 'Example.Anim.Math' => namespace Example.Anim.Math\n{\n    class I {}\n}\n",
    blockFix);

var fileScopedFix = await Fix($@"{Root}\Math\J.cs", "using System;\n\nnamespace Wrong;\n\nclass J {}\n");
Check("fix renames a file-scoped namespace",
    fileScopedFix == "Change namespace to 'Example.Anim.Math' => using System;\n\nnamespace Example.Anim.Math;\n\nclass J {}\n",
    fileScopedFix);

Console.WriteLine(failures == 0 ? "All checks passed." : $"{failures} check(s) failed.");
return failures == 0 ? 0 : 1;

sealed class OptionsProvider(Dictionary<string, string> global, Dictionary<string, string> file)
    : AnalyzerConfigOptionsProvider
{
    public override AnalyzerConfigOptions GlobalOptions { get; } = new Options(global);

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new Options(file);

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => new Options(file);

    sealed class Options(Dictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) =>
            values.TryGetValue(key, out value);
    }
}
