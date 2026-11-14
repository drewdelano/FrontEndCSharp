using System.Diagnostics;
using System.IO.Compression;

namespace FrontEndCSharp.Tailwind;

public static class TailwindEditorCatalogGenerator
{
    private const string CatalogScript = """
        import fs from "node:fs";
        import path from "node:path";
        import { __unstable__loadDesignSystem } from "@tailwindcss/node";

        const packageJson = JSON.parse(fs.readFileSync("./node_modules/tailwindcss/package.json", "utf8"));
        const inputPath = process.argv[2] ? path.resolve(process.argv[2]) : null;
        const css = fs.readFileSync(inputPath ?? "./node_modules/tailwindcss/index.css", "utf8");
        const designSystem = await __unstable__loadDesignSystem(css, {
          base: inputPath ? path.dirname(inputPath) : process.cwd(),
        });
        const entries = designSystem.getClassList();
        const generated = designSystem.candidatesToCss(entries.map((entry) => entry[0]));
        const spacing = designSystem.resolveThemeValue("--spacing") ?? "";
        const entryFamilies = entries.map(() => "");
        const candidateInfo = [];
        for (let index = 0; index < entries.length; index++) {
          const [name] = entries[index];
          const candidates = designSystem.parseCandidate(name);
          const candidate = candidates
            .filter((candidate) => candidate.kind === "functional" && candidate.value !== null)
            .sort((left, right) => right.root.length - left.root.length)[0];
          candidateInfo.push({
            index,
            name,
            root: candidate?.root ?? "",
            property: primaryProperty(generated[index]),
          });
        }
        const familiesByPrefix = new Map();
        const entriesByRoot = groupBy(
          candidateInfo.filter((entry) => entry.root && entry.name.startsWith(`${entry.root}-`)),
          (entry) => entry.root,
        );
        for (const [root, rootEntries] of entriesByRoot) {
          const basePrefix = `${root}-`;
          const propertyGroups = groupBy(rootEntries, (entry) => entry.property);
          const arbitraryRaw = designSystem.candidatesToCss([`${basePrefix}[13px]`])[0];
          const customPropertyRaw = designSystem.candidatesToCss([`${basePrefix}(--value)`])[0];
          const arbitraryProperty = primaryProperty(arbitraryRaw);
          const customProperty = primaryProperty(customPropertyRaw);
          const baseFromArbitrary = arbitraryProperty.length > 0
            && propertyGroups.has(arbitraryProperty);
          const baseProperty = baseFromArbitrary
            ? arbitraryProperty
            : propertyGroups.size === 1
              ? propertyGroups.keys().next().value
              : "";

          for (const [property, propertyEntries] of propertyGroups) {
            if (property
                && property === baseProperty
                && (propertyEntries.length > 1 || baseFromArbitrary)) {
              addFamily(
                basePrefix,
                property,
                propertyEntries,
                arbitraryProperty === property ? summarize(arbitraryRaw) : "",
                customProperty === property ? summarize(customPropertyRaw) : "",
              );
              continue;
            }

            const entriesByFirstValue = groupBy(propertyEntries, (entry) => {
              const remainder = entry.name.slice(basePrefix.length);
              const separator = remainder.indexOf("-");
              return separator > 0 ? remainder.slice(0, separator) : "";
            });
            for (const [firstValue, nestedEntries] of entriesByFirstValue) {
              if (firstValue && nestedEntries.length > 1) {
                addFamily(`${basePrefix}${firstValue}-`, property, nestedEntries, "", "");
              }
            }
          }
        }
        const families = Array.from(familiesByPrefix.values())
          .sort((left, right) => left.prefix < right.prefix ? -1 : left.prefix > right.prefix ? 1 : 0);
        const variantsForCompletion = [];
        const variants = designSystem.getVariants();
        for (const kind of ["min", "max"]) {
          const variant = variants.find((entry) => entry.name === kind);
          if (!variant) throw new Error(`Tailwind did not expose its '${kind}' media variant.`);
          for (const value of variant.values) {
            const selectors = variant.selectors({ value, modifier: null });
            if (selectors.length !== 1 || !selectors[0].startsWith("@media ")) {
              throw new Error(`Tailwind exposed an unsupported '${kind}-${value}' media variant.`);
            }
            variantsForCompletion.push([
              kind === "min" ? `${value}:` : `${kind}-${value}:`,
              selectors[0],
            ]);
          }
        }
        for (const name of ["hover", "focus", "dark"]) {
          const variant = variants.find((entry) => entry.name === name);
          if (!variant) throw new Error(`Tailwind did not expose its '${name}' variant.`);
          const selectors = variant.selectors({ value: null, modifier: null });
          if (selectors.length !== 1) {
            throw new Error(`Tailwind exposed an unsupported '${name}' variant.`);
          }
          variantsForCompletion.push([`${name}:`, selectors[0]]);
        }

        function summarize(css) {
          if (!css) return "";
          const end = css.indexOf("\n}");
          if (end < 0) return "";
          return css
            .slice(css.indexOf("{") + 1, end)
            .split("\n")
            .map((line) => line.trim())
            .filter(Boolean)
            .join(" ")
            .replaceAll("\t", " ");
        }

        function primaryProperty(css) {
          if (!css) return "";
          const properties = css
            .split("\n")
            .map((line) => line.trim().match(/^([\w-]+):/)?.[1] ?? "")
            .filter(Boolean);
          return properties.find((property) => !property.startsWith("--tw-"))
            ?? properties[0]
            ?? "";
        }

        function addFamily(prefix, property, familyEntries, arbitrary, customProperty) {
          const existing = familiesByPrefix.get(prefix);
          if (existing && existing.property !== property) {
            throw new Error(`Tailwind utility family '${prefix}' emits both '${existing.property}' and '${property}'.`);
          }
          familiesByPrefix.set(prefix, { prefix, property, arbitrary, customProperty });
          for (const entry of familyEntries) entryFamilies[entry.index] = prefix;
        }

        function groupBy(values, getKey) {
          const groups = new Map();
          for (const value of values) {
            const key = getKey(value);
            const group = groups.get(key);
            if (group) group.push(value);
            else groups.set(key, [value]);
          }
          return groups;
        }

        process.stdout.write(`#\t${packageJson.version}\t${entries.length}\t${spacing}\n`);
        for (let index = 0; index < entries.length; index++) {
          const [name, metadata] = entries[index];
          process.stdout.write(`${name}\t${metadata.modifiers.join(",")}\t${summarize(generated[index])}\t${entryFamilies[index]}\n`);
        }
        process.stdout.write(`#families\t${families.length}\n`);
        for (const family of families) {
          process.stdout.write(`${family.prefix}\t${family.property}\t${family.arbitrary}\t${family.customProperty}\n`);
        }
        process.stdout.write(`#variants\t${variantsForCompletion.length}\n`);
        for (const [name, condition] of variantsForCompletion) {
          process.stdout.write(`${name}\t${condition}\n`);
        }
        """;

    public static void Generate(
        string packageDirectory,
        string outputPath,
        string? effectiveInputPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var fullPackageDirectory = Path.GetFullPath(packageDirectory);
        var packageJson = Path.Combine(fullPackageDirectory, "node_modules", "tailwindcss", "package.json");
        if (!File.Exists(packageJson))
        {
            throw new TailwindBuildException(
                $"The Tailwind package is not installed beneath '{fullPackageDirectory}'. Run npm ci before generating the editor catalog.");
        }

        var fullOutputPath = Path.GetFullPath(outputPath);
        var fullEffectiveInputPath = string.IsNullOrWhiteSpace(effectiveInputPath)
            ? null
            : Path.GetFullPath(effectiveInputPath);
        if (fullEffectiveInputPath is not null && !File.Exists(fullEffectiveInputPath))
        {
            throw new TailwindBuildException(
                $"The effective Tailwind input does not exist: '{fullEffectiveInputPath}'.");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
        var temporaryPath = fullOutputPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var scriptPath = Path.Combine(fullPackageDirectory, ".tailwind-editor-catalog." + Guid.NewGuid().ToString("N") + ".mjs");
        try
        {
            File.WriteAllText(scriptPath, CatalogScript);
            var startInfo = new ProcessStartInfo
            {
                FileName = "node",
                WorkingDirectory = fullPackageDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add(scriptPath);
            if (fullEffectiveInputPath is not null)
            {
                startInfo.ArgumentList.Add(fullEffectiveInputPath);
            }

            using var process = Process.Start(startInfo)
                ?? throw new TailwindBuildException("Node.js did not start while generating the Tailwind editor catalog.");
            var errorTask = process.StandardError.ReadToEndAsync();
            using (var output = File.Create(temporaryPath))
            using (var compressed = new GZipStream(output, CompressionLevel.SmallestSize))
            {
                process.StandardOutput.BaseStream.CopyTo(compressed);
            }

            process.WaitForExit();
            var error = errorTask.GetAwaiter().GetResult();
            if (process.ExitCode != 0)
            {
                throw new TailwindBuildException(
                    "Tailwind failed to generate the editor catalog. " + error.Trim());
            }

            ValidateCatalog(temporaryPath);

            File.Move(temporaryPath, fullOutputPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
            if (File.Exists(scriptPath))
            {
                File.Delete(scriptPath);
            }
        }
    }

    private static void ValidateCatalog(string path)
    {
        try
        {
            using var input = File.OpenRead(path);
            using var compressed = new GZipStream(input, CompressionMode.Decompress);
            using var reader = new StreamReader(compressed);
            var fields = reader.ReadLine()?.Split('\t');
            if (fields is not ["#", _, var countText, _]
                || !int.TryParse(countText, out var count)
                || count <= 0)
            {
                throw new TailwindBuildException("Tailwind produced an empty or invalid editor catalog.");
            }
            for (var index = 0; index < count; index++)
            {
                if (reader.ReadLine()?.Split('\t') is not [_, _, _, _])
                {
                    throw new TailwindBuildException("Tailwind produced an incomplete editor class catalog.");
                }
            }
            if (reader.ReadLine()?.Split('\t') is not ["#families", var familyCountText]
                || !int.TryParse(familyCountText, out var familyCount)
                || familyCount <= 0)
            {
                throw new TailwindBuildException("Tailwind produced an empty or invalid editor utility-family catalog.");
            }
            for (var index = 0; index < familyCount; index++)
            {
                if (reader.ReadLine()?.Split('\t') is not [_, _, _, _])
                {
                    throw new TailwindBuildException("Tailwind produced an incomplete editor utility-family catalog.");
                }
            }
            if (reader.ReadLine()?.Split('\t') is not ["#variants", var variantCountText]
                || !int.TryParse(variantCountText, out var variantCount)
                || variantCount <= 0)
            {
                throw new TailwindBuildException("Tailwind produced an empty or invalid editor variant catalog.");
            }
            for (var index = 0; index < variantCount; index++)
            {
                if (reader.ReadLine()?.Split('\t') is not [_, _])
                {
                    throw new TailwindBuildException("Tailwind produced an incomplete editor variant catalog.");
                }
            }
            if (reader.ReadLine() != null)
            {
                throw new TailwindBuildException("Tailwind produced unexpected trailing editor-catalog data.");
            }
        }
        catch (InvalidDataException exception)
        {
            throw new TailwindBuildException(
                "Tailwind produced an invalid editor catalog: " + exception.Message);
        }
    }
}
