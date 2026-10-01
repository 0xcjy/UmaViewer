// Read-only IL2CPP method pointer lookup against a hash-verified recovered target.
// Accepts RVAs on the command line; outputs exact metadata matches, not guesses.
using System.Reflection;
using System.Security.Cryptography;
using AssetRipper.Primitives;
using LibCpp2IL;
using LibCpp2IL.Metadata;

const string root = @"D:\Projects\UmaTools\UmaViewer-master5\tmp";
var binaryPath = Path.Combine(root, "target-gameassembly-unpacked.dll");
var metadataPath = Path.Combine(root, "global-metadata.runtime.dat");
const string expectedHash = "E273B4F499E4449BBABC361C03C91F260178BF3E687825AA3ABE6E686664569B";
const string expectedMetadataHash = "88A465567FBB7189F00689D0DEC6C741F2BAD0915E123D6CE1415E3261F49D0D";
if (!SHA256.HashData(File.ReadAllBytes(binaryPath)).AsSpan().SequenceEqual(Convert.FromHexString(expectedHash)))
    throw new InvalidDataException("Recovered target binary hash mismatch");
if (!SHA256.HashData(File.ReadAllBytes(metadataPath)).AsSpan().SequenceEqual(Convert.FromHexString(expectedMetadataHash)))
    throw new InvalidDataException("Recovered target metadata hash mismatch");

var wanted = args.Select(a => Convert.ToUInt64(a.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? a[2..] : a, 16))
    .ToHashSet();
if (wanted.Count == 0)
    throw new ArgumentException("Pass one or more hexadecimal RVAs");
// LibCpp2IL emits several pages of initialization messages; keep the report bounded.
var stdout = Console.Out;
LibCpp2IlContext context;
try
{
    Console.SetOut(TextWriter.Null);
    context = LibCpp2IlMain.LoadFromFileAsContext(binaryPath, metadataPath, UnityVersion.Parse("2022.3.62f3"));
}
finally { Console.SetOut(stdout); }
var metadata = context.Metadata;
var found = new Dictionary<ulong, List<string>>();
for (int i = 0; i < metadata.TypeDefinitionCount; i++)
{
    var index = (Il2CppVariableWidthIndex<Il2CppTypeDefinition>)Activator.CreateInstance(
        typeof(Il2CppVariableWidthIndex<Il2CppTypeDefinition>),
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
        binder: null, args: new object[] { i }, culture: null)!;
    var type = metadata.GetTypeDefinitionFromIndex(index);
    foreach (var method in type.Methods ?? Array.Empty<Il2CppMethodDefinition>())
    {
        ulong rva;
        try { rva = method.Rva; }
        catch { continue; }
        if (!wanted.Contains(rva)) continue;
        var name = $"{type.Namespace}.{type.Name}.{method.Name}";
        if (!found.TryGetValue(rva, out var names)) found[rva] = names = new List<string>();
        names.Add(name);
    }
}
Console.WriteLine($"verified target SHA256={expectedHash}; metadataVersion={context.Binary.MetadataVersion}");
foreach (ulong rva in wanted.OrderBy(x => x))
    Console.WriteLine($"0x{rva:x}: {(found.TryGetValue(rva, out var names) ? string.Join("; ", names) : "<no metadata method pointer>")}");
