using System.Reflection.Metadata;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.Json;

if (args.Length > 0 && args[0] == "--references") {
    var rows = args.Skip(1).Select(path => {
        using var stream = File.OpenRead(path);
        using var pe = new System.Reflection.PortableExecutable.PEReader(stream);
        var reader = System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(pe);
        var assembly = reader.GetAssemblyDefinition();
        return new { File = Path.GetFullPath(path), Name = reader.GetString(assembly.Name), Version = assembly.Version.ToString(),
            SHA256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant() };
    }).ToArray();
    Console.WriteLine(JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
    return;
}

if (args.Length == 2 && args[0] == "--mod-metadata") {
    using var stream = File.OpenRead(args[1]);
    using var pe = new System.Reflection.PortableExecutable.PEReader(stream);
    var reader = System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(pe);
    var assembly = reader.GetAssemblyDefinition();
    string TypeName(EntityHandle handle) {
        if (handle.Kind != HandleKind.TypeReference) return "unsupported";
        var type = reader.GetTypeReference((TypeReferenceHandle)handle);
        var name = reader.GetString(type.Name);
        if (type.ResolutionScope.Kind == HandleKind.TypeReference) return TypeName(type.ResolutionScope) + "+" + name;
        var ns = reader.GetString(type.Namespace);
        return ns.Length == 0 ? name : ns + "." + name;
    }
    string EntrySignature(MethodDefinition method) {
        var blob = reader.GetBlobReader(method.Signature);
        var header = blob.ReadSignatureHeader();
        if (header.Kind != SignatureKind.Method || header.IsGeneric || header.IsInstance) return null;
        if (blob.ReadCompressedInteger() != 1 || blob.ReadSignatureTypeCode() != SignatureTypeCode.Boolean ||
            blob.ReadSignatureTypeCode() != SignatureTypeCode.TypeHandle) return null;
        var parameter = TypeName(blob.ReadTypeHandle());
        return blob.RemainingBytes == 0 ? "bool(" + parameter + ")" : null;
    }
    var types = reader.TypeDefinitions.Select(handle => {
        var type = reader.GetTypeDefinition(handle);
        return new { Name = reader.GetString(type.Namespace) + "." + reader.GetString(type.Name),
            Methods = type.GetMethods().Select(methodHandle => {
                var method = reader.GetMethodDefinition(methodHandle);
                return new { Name = reader.GetString(method.Name),
                    Public = (method.Attributes & System.Reflection.MethodAttributes.MemberAccessMask) == System.Reflection.MethodAttributes.Public,
                    Static = (method.Attributes & System.Reflection.MethodAttributes.Static) != 0,
                    EntrySignature = reader.GetString(method.Name) == "Load" && reader.GetString(type.Name) == "Main" ? EntrySignature(method) : null };
            }).ToArray() };
    }).Where(type => type.Name == "IsekaiMod.Main" || type.Name.StartsWith("IsekaiMod.Localization.")).ToArray();
    Console.WriteLine(JsonSerializer.Serialize(new {
        Name = reader.GetString(assembly.Name), Version = assembly.Version.ToString(), Types = types,
        Resources = reader.ManifestResources.Select(handle => reader.GetString(reader.GetManifestResource(handle).Name)).ToArray()
    }, new JsonSerializerOptions { WriteIndented = true }));
    return;
}

var root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
var trees = Directory.GetFiles(Path.Combine(root, "IsekaiMod"), "*.cs", SearchOption.AllDirectories)
    .Where(p => !p.Contains("/obj/") && !p.Contains("/bin/") && !p.Contains("/Localization/"))
    .OrderBy(p => p, StringComparer.Ordinal).Select(p => CSharpSyntaxTree.ParseText(File.ReadAllText(p), path:p)).ToArray();
var compilation = CSharpCompilation.Create("Inventory", trees, new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
string ResolveString(ExpressionSyntax expression, SemanticModel model, HashSet<ISymbol> seen = null) {
    var value = model.GetConstantValue(expression);
    if (value.HasValue && value.Value is string text) return text;
    var symbol = model.GetSymbolInfo(expression).Symbol;
    var type = symbol is IFieldSymbol field ? field.Type : symbol is ILocalSymbol local ? local.Type : null;
    if (symbol == null || type?.SpecialType != SpecialType.System_String) return null;
    seen ??= new HashSet<ISymbol>(SymbolEqualityComparer.Default);
    if (!seen.Add(symbol)) return null;
    foreach (var reference in symbol.DeclaringSyntaxReferences) {
        if (reference.GetSyntax() is VariableDeclaratorSyntax variable && variable.Initializer != null)
            return ResolveString(variable.Initializer.Value, compilation.GetSemanticModel(variable.SyntaxTree), seen);
    }
    return null;
}
var constants = new List<object>();
var calls = new List<object>();
var factories = new List<object>();
foreach (var tree in trees) {
    var model = compilation.GetSemanticModel(tree);
    object Location(SyntaxNode node) => new { File = Path.GetRelativePath(root, tree.FilePath), Line = tree.GetLineSpan(node.Span).StartLinePosition.Line + 1 };
    // This is a syntax/constant inventory, NOT a proof of runtime reachability.
    foreach (var expr in tree.GetRoot().DescendantNodes().OfType<ExpressionSyntax>()) {
        var value = model.GetConstantValue(expr);
        if (!value.HasValue || value.Value is not string text || text.Length == 0) continue;
        if (expr.Parent is ExpressionSyntax parent && model.GetConstantValue(parent) is { HasValue:true, Value:string }) continue;
        constants.Add(new { Text = text, Source = Location(expr) });
    }
    foreach (var call in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()) {
        var method = call.Expression is MemberAccessExpressionSyntax member ? member.Name.Identifier.Text : call.Expression.ToString();
        if (new[] { "CreateToggleBuffFeature", "CreateToggleAuraBuffFeature", "CreateImmunity", "CreateSummonBuffToggleFeature", "CreateExceptionalWeapon", "CreateStartingWeapon" }.Contains(method)) {
            factories.Add(new { Method = method, Source = Location(call), Arguments = call.ArgumentList.Arguments.Select(a => {
                var value = model.GetConstantValue(a.Expression);
                return new { Name = a.NameColon?.Name.Identifier.Text, Expression = a.Expression.ToString(), Constant = value.HasValue ? value.Value : ResolveString(a.Expression, model) };
            }).ToArray() });
        }
        if (!new[] { "SetName", "SetDescription", "SetSummonDescription", "SetBackgroundDescription", "SetLocalisedName", "SetText", "CreateString", "LogToGeneral" }.Contains(method)) continue;
        calls.Add(new { Method = method, Source = Location(call), Arguments = call.ArgumentList.Arguments.Select(a => {
            var constant = model.GetConstantValue(a.Expression);
            return new { Expression = a.Expression.ToString(), Constant = constant.HasValue ? constant.Value : ResolveString(a.Expression, model) };
        }).ToArray() });
    }
}
Console.WriteLine(JsonSerializer.Serialize(new { Constants = constants, Calls = calls, Factories = factories, SyntaxErrors = trees.SelectMany(t => t.GetDiagnostics()).Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()).ToArray() }, new JsonSerializerOptions { WriteIndented = true }));
