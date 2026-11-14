var projectPath = Path.GetFullPath(args[^3]);
var outputPath = Path.GetFullPath(args[^2]);
var invocationPath = Path.Combine(Path.GetDirectoryName(projectPath)!, "compiler-invocations.txt");
var count = File.Exists(invocationPath) ? int.Parse(File.ReadAllText(invocationPath)) : 0;
File.WriteAllText(invocationPath, (count + 1).ToString());
Directory.CreateDirectory(outputPath);
foreach (var name in new[] { "index.js", "index.js.map", "msil.bin" })
{
    var target = Path.Combine(outputPath, name);
    var temporary = target + ".tmp";
    File.WriteAllText(temporary, $"{name}:{count + 1}");
    File.Move(temporary, target, overwrite: true);
}
