using System.Security.Cryptography;
using dnlib.DotNet;

// Local reference preparation only. Never replace an installed game assembly.
// Match the original Windows project's PubliciseCompilerGenerated=true policy.
if (args.Length != 2) throw new ArgumentException("Usage: PublicizeReferences <Assembly-CSharp.dll> <new-output-directory>");
var input = Path.GetFullPath(args[0]);
var outputDirectory = Path.GetFullPath(args[1]);
if (Path.GetFileName(input) != "Assembly-CSharp.dll") throw new ArgumentException("Only Assembly-CSharp.dll is supported.");
var output = Path.Combine(outputDirectory, "Assembly-CSharp.dll");
if (input == output || File.Exists(output)) throw new IOException("Output must be a new local copy, never the input or an existing file.");
var originalHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(input))).ToLowerInvariant();
Directory.CreateDirectory(outputDirectory);
using var module = ModuleDefMD.Load(input);
int types = 0, methods = 0, fields = 0;
foreach (var type in module.GetTypes()) {
    // Preserve module metadata pseudo-type; it has no normal type visibility.
    if (!type.IsGlobalModuleType) {
        type.Attributes = (type.Attributes & ~TypeAttributes.VisibilityMask) |
            (type.IsNested ? TypeAttributes.NestedPublic : TypeAttributes.Public);
        types++;
    }
    foreach (var method in type.Methods) {
        method.Attributes = (method.Attributes & ~MethodAttributes.MemberAccessMask) | MethodAttributes.Public;
        methods++;
    }
    foreach (var field in type.Fields) {
        field.Attributes = (field.Attributes & ~FieldAttributes.FieldAccessMask) | FieldAttributes.Public;
        fields++;
    }
}
using (var destination = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
    module.Write(destination);
}
if (originalHash != Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(input))).ToLowerInvariant())
    throw new IOException("Input changed during reference preparation.");
Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new {
    InputSHA256 = originalHash, OutputSHA256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(output))).ToLowerInvariant(),
    Types = types, Methods = methods, Fields = fields, Policy = "PubliciseCompilerGenerated=true; local compile reference only; do not deploy"
}, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
