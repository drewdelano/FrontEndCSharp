using System.Xml.Linq;

namespace FrontEndCSharp.Fecscn.Commands;

internal static class ProjectFileEditor
{
    public static void ValidatePackageReference(string path, string name, string version)
    {
        var document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
        var existing = document.Descendants("PackageReference")
            .FirstOrDefault(element => string.Equals(
                (string?)element.Attribute("Include"), name, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            return;
        }
        var existingVersion = (string?)existing.Attribute("Version")
            ?? existing.Element("Version")?.Value;
        if (!string.Equals(existingVersion, version, StringComparison.Ordinal))
        {
            throw new FecscnCommandException(
                $"Project '{path}' already references {name} version '{existingVersion}', not required version '{version}'.");
        }
    }

    public static void ValidateProperty(string path, string name, string value)
    {
        var existing = XDocument.Load(path, LoadOptions.PreserveWhitespace)
            .Descendants(name).FirstOrDefault();
        if (existing is not null && !string.Equals(existing.Value, value, StringComparison.Ordinal))
        {
            throw new FecscnCommandException(
                $"Project '{path}' already defines {name}; fecscn will not overwrite it.");
        }
    }

    public static bool EnsurePackageReference(string path, string name, string version, bool privateAssets = false)
    {
        var document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
        var existing = document.Descendants("PackageReference")
            .FirstOrDefault(element => string.Equals(
                (string?)element.Attribute("Include"), name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            ValidatePackageReference(path, name, version);
            return false;
        }

        var group = document.Root!.Elements("ItemGroup").LastOrDefault()
            ?? AddGroup(document.Root, "ItemGroup");
        var reference = new XElement("PackageReference",
            new XAttribute("Include", name),
            new XAttribute("Version", version));
        if (privateAssets)
        {
            reference.Add(new XAttribute("PrivateAssets", "all"));
        }
        group.Add(reference);
        Save(document, path);
        return true;
    }

    public static bool EnsureProperty(string path, string name, string value)
    {
        var document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
        var existing = document.Descendants(name).FirstOrDefault();
        if (existing is not null)
        {
            if (string.Equals(existing.Value, value, StringComparison.Ordinal))
            {
                return false;
            }
            ValidateProperty(path, name, value);
            return false;
        }
        var group = document.Root!.Elements("PropertyGroup").FirstOrDefault()
            ?? AddGroup(document.Root, "PropertyGroup");
        group.Add(new XElement(name, value));
        Save(document, path);
        return true;
    }

    private static XElement AddGroup(XElement root, string name)
    {
        var group = new XElement(name);
        root.Add(group);
        return group;
    }

    private static void Save(XDocument document, string path)
    {
        var temporary = path + ".fecscn.tmp";
        document.Save(temporary, SaveOptions.DisableFormatting);
        File.Move(temporary, path, overwrite: true);
    }
}
