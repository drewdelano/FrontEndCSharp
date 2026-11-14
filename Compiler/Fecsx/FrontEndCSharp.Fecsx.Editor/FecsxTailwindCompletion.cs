using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using FrontEndCSharp.Fecsx.Syntax;

namespace FrontEndCSharp.Fecsx.Editor;

internal sealed class FecsxTailwindCompletionContext
{
    public FecsxTailwindCompletionContext(
        FecsxTextSpan replacementSpan,
        string token,
        int caretOffset)
    {
        ReplacementSpan = replacementSpan;
        Token = token;
        CaretOffset = caretOffset;
    }

    public FecsxTextSpan ReplacementSpan { get; }
    public string Token { get; }
    public int CaretOffset { get; }
}

internal sealed class TailwindClassCatalog
{
    private const string ResourceName = "FrontEndCSharp.Fecsx.Editor.Assets.TailwindClassCatalog.tsv.gz";
    private static readonly Lazy<TailwindClassCatalog> CurrentCatalog = new(LoadEmbedded);
    private readonly Entry[] _entries;
    private readonly Family[] _families;
    private readonly Variant[] _variants;

    private TailwindClassCatalog(
        string version,
        string spacing,
        Entry[] entries,
        Family[] families,
        Variant[] variants)
    {
        Version = version;
        Spacing = spacing;
        _entries = entries;
        _families = families;
        _variants = variants;
    }

    public static TailwindClassCatalog Current => CurrentCatalog.Value;
    public string Version { get; }
    public string Spacing { get; }
    public int Count => _entries.Length;
    public IReadOnlyList<Family> Families => _families;
    public IReadOnlyList<Variant> Variants => _variants;

    public Entry? Find(string name)
    {
        var index = LowerBound(name);
        return index < _entries.Length && string.Equals(_entries[index].Name, name, StringComparison.Ordinal)
            ? _entries[index]
            : null;
    }

    public IEnumerable<Entry> FindByPrefix(string prefix)
    {
        var index = LowerBound(prefix);
        while (index < _entries.Length
            && _entries[index].Name.StartsWith(prefix, StringComparison.Ordinal))
        {
            yield return _entries[index++];
        }
    }

    public Family? FindContainingFamily(string utility) =>
        _families
            .Where(family => utility.StartsWith(family.Prefix, StringComparison.Ordinal))
            .OrderByDescending(family => family.Prefix.Length)
            .FirstOrDefault();

    private int LowerBound(string value)
    {
        var low = 0;
        var high = _entries.Length;
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            if (string.CompareOrdinal(_entries[middle].Name, value) < 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }
        return low;
    }

    private static TailwindClassCatalog LoadEmbedded()
    {
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded Tailwind class catalog '{ResourceName}' is missing.");
        return Load(resource);
    }

    internal static TailwindClassCatalog Load(Stream input)
    {
        using var compressed = new GZipStream(input, CompressionMode.Decompress);
        using var reader = new StreamReader(compressed, Encoding.UTF8, true);
        var header = reader.ReadLine()?.Split('\t')
            ?? throw new InvalidDataException("The Tailwind class catalog is empty.");
        if ((header.Length != 3 && header.Length != 4)
            || header[0] != "#"
            || !int.TryParse(header[2], NumberStyles.None, CultureInfo.InvariantCulture, out var expectedCount))
        {
            throw new InvalidDataException("The Tailwind class catalog header is invalid.");
        }
        var spacing = header.Length == 4 ? header[3] : string.Empty;

        var entries = new List<Entry>(expectedCount);
        for (var index = 0; index < expectedCount; index++)
        {
            var line = reader.ReadLine()
                ?? throw new InvalidDataException("The Tailwind class catalog ended before its declared entry count.");
            var fields = line.Split(new[] { '\t' }, 4);
            if (fields.Length != 3 && fields.Length != 4)
            {
                throw new InvalidDataException("A Tailwind class catalog entry is invalid.");
            }
            entries.Add(new Entry(
                fields[0],
                fields[1].Length == 0 ? Array.Empty<string>() : fields[1].Split(','),
                fields[2],
                fields.Length == 4 && fields[3].Length > 0 ? fields[3] : null));
        }

        var sectionHeader = reader.ReadLine()?.Split('\t')
            ?? throw new InvalidDataException("The Tailwind utility-family or variant catalog is missing.");
        Family[] families;
        if (sectionHeader.Length == 2 && sectionHeader[0] == "#families")
        {
            if (!int.TryParse(
                    sectionHeader[1],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var expectedFamilyCount)
                || expectedFamilyCount <= 0)
            {
                throw new InvalidDataException("The Tailwind utility-family catalog header is invalid.");
            }

            families = new Family[expectedFamilyCount];
            for (var index = 0; index < expectedFamilyCount; index++)
            {
                var fields = reader.ReadLine()?.Split('\t');
                if (fields == null
                    || (fields.Length != 3 && fields.Length != 4)
                    || fields[0].Length == 0)
                {
                    throw new InvalidDataException("A Tailwind utility-family catalog entry is invalid.");
                }
                families[index] = fields.Length == 4
                    ? new Family(fields[0], fields[1], fields[2], fields[3])
                    : new Family(fields[0], string.Empty, fields[1], fields[2]);
            }

            sectionHeader = reader.ReadLine()?.Split('\t')
                ?? throw new InvalidDataException("The Tailwind variant catalog is missing.");
        }
        else
        {
            families = Array.Empty<Family>();
        }
        var familyPrefixes = new HashSet<string>(StringComparer.Ordinal);
        if (families.Any(family => !familyPrefixes.Add(family.Prefix))
            || entries.Any(entry =>
                entry.FamilyPrefix != null
                && !familyPrefixes.Contains(entry.FamilyPrefix)))
        {
            throw new InvalidDataException(
                "The Tailwind class catalog contains an unknown or duplicate utility family.");
        }
        AssignFamilyPreviews(entries, families);

        var variantHeader = sectionHeader;
        if (variantHeader.Length != 2
            || variantHeader[0] != "#variants"
            || !int.TryParse(variantHeader[1], NumberStyles.None, CultureInfo.InvariantCulture, out var expectedVariantCount)
            || expectedVariantCount <= 0)
        {
            throw new InvalidDataException("The Tailwind variant catalog header is invalid.");
        }

        var variants = new Variant[expectedVariantCount];
        for (var index = 0; index < expectedVariantCount; index++)
        {
            var fields = reader.ReadLine()?.Split('\t');
            if (fields == null || fields.Length != 2)
            {
                throw new InvalidDataException("A Tailwind variant catalog entry is invalid.");
            }
            variants[index] = new Variant(fields[0], fields[1]);
        }
        if (reader.ReadLine() != null)
        {
            throw new InvalidDataException("The Tailwind editor catalog contains unexpected trailing data.");
        }
        return new TailwindClassCatalog(header[1], spacing, entries.ToArray(), families, variants);
    }

    private static void AssignFamilyPreviews(
        IReadOnlyList<Entry> entries,
        IEnumerable<Family> families)
    {
        var entriesByFamily = entries
            .Where(entry => entry.FamilyPrefix != null)
            .GroupBy(entry => entry.FamilyPrefix!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        foreach (var family in families)
        {
            if (!entriesByFamily.TryGetValue(family.Prefix, out var members)
                || members.Length == 0)
            {
                continue;
            }

            var declarations = members.ToDictionary(
                entry => entry,
                entry => ParseCssDeclarations(entry.Css)
                    .GroupBy(declaration => declaration.Property, StringComparer.Ordinal)
                    .Select(group => new PreviewCandidate(
                        group.Key,
                        string.Join("; ", group.Select(item => item.Css)),
                        string.Join("\n", group.Select(item => item.Value))))
                    .ToArray());
            var signaturesByProperty = declarations
                .SelectMany(pair => pair.Value.Select(candidate => candidate.Property))
                .Distinct(StringComparer.Ordinal)
                .ToDictionary(
                    property => property,
                    property => members.ToDictionary(
                        entry => entry,
                        entry => declarations[entry]
                            .FirstOrDefault(candidate => string.Equals(
                                candidate.Property,
                                property,
                                StringComparison.Ordinal))?.Signature ?? string.Empty),
                    StringComparer.Ordinal);
            var varyingProperties = new HashSet<string>(
                signaturesByProperty
                    .Where(pair => pair.Value.Values.Distinct(StringComparer.Ordinal).Count() > 1)
                    .Select(pair => pair.Key),
                StringComparer.Ordinal);
            var candidates = members.ToDictionary(
                entry => entry,
                entry => declarations[entry]
                    .Where(candidate => varyingProperties.Contains(candidate.Property))
                    .OrderBy(candidate => signaturesByProperty[candidate.Property].Values.Count(
                        value => string.Equals(value, candidate.Signature, StringComparison.Ordinal)))
                    .ThenBy(candidate => string.Equals(
                        candidate.Property,
                        family.Property,
                        StringComparison.Ordinal) ? 0 : 1)
                    .ThenBy(candidate => candidate.Property.StartsWith("--", StringComparison.Ordinal) ? 1 : 0)
                    .ThenBy(candidate => candidate.Property.StartsWith("-webkit-", StringComparison.Ordinal) ? 1 : 0)
                    .ToArray());
            var previewLengths = members.ToDictionary(
                entry => entry,
                entry => candidates[entry].Length > 0 ? 1 : 0);

            while (true)
            {
                var collapsed = members
                    .GroupBy(
                        entry => ComposePreview(candidates[entry], previewLengths[entry]),
                        StringComparer.Ordinal)
                    .Where(group => group
                        .Select(entry => entry.Css)
                        .Distinct(StringComparer.Ordinal)
                        .Count() > 1)
                    .ToArray();
                if (collapsed.Length == 0) break;

                var progressed = false;
                foreach (var group in collapsed)
                {
                    foreach (var entry in group)
                    {
                        if (previewLengths[entry] < candidates[entry].Length)
                        {
                            previewLengths[entry]++;
                            progressed = true;
                        }
                    }
                }
                if (!progressed) break;
            }

            foreach (var entry in members)
            {
                entry.PreviewCss = ComposePreview(candidates[entry], previewLengths[entry]);
                if (entry.PreviewCss.Length == 0)
                {
                    entry.PreviewCss = entry.Css;
                }
            }
            family.PreviewProperty = candidates.Values
                .Where(value => value.Length > 0)
                .Select(value => value[0].Property)
                .GroupBy(property => property, StringComparer.Ordinal)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => group.Key)
                .FirstOrDefault() ?? family.Property;
        }
    }

    private static string ComposePreview(
        IReadOnlyList<PreviewCandidate> candidates,
        int count) =>
        string.Join("; ", candidates.Take(count).Select(candidate => candidate.Css));

    internal static IReadOnlyList<CssDeclaration> ParseCssDeclarations(string css)
    {
        var result = new List<CssDeclaration>();
        foreach (var segment in css.Split(';'))
        {
            var declaration = segment.Trim();
            var boundary = Math.Max(
                declaration.LastIndexOf('{'),
                declaration.LastIndexOf('}'));
            if (boundary >= 0)
            {
                declaration = declaration.Substring(boundary + 1).Trim();
            }
            var separator = declaration.IndexOf(':');
            if (separator <= 0) continue;
            var property = declaration.Substring(0, separator).Trim();
            var value = declaration.Substring(separator + 1).Trim();
            if (property.Length == 0
                || value.Length == 0
                || property.Any(character =>
                    !char.IsLetterOrDigit(character) && character != '-'))
            {
                continue;
            }
            result.Add(new CssDeclaration(property, value));
        }
        return result;
    }

    private sealed class PreviewCandidate
    {
        public PreviewCandidate(string property, string css, string signature)
        {
            Property = property;
            Css = css;
            Signature = signature;
        }

        public string Property { get; }
        public string Css { get; }
        public string Signature { get; }
    }

    internal sealed class CssDeclaration
    {
        public CssDeclaration(string property, string value)
        {
            Property = property;
            Value = value;
        }

        public string Property { get; }
        public string Value { get; }
        public string Css => Property + ": " + Value;
    }

    internal sealed class Entry
    {
        public Entry(string name, string[] modifiers, string css, string? familyPrefix)
        {
            Name = name;
            Modifiers = modifiers;
            Css = css;
            FamilyPrefix = familyPrefix;
            PreviewCss = css;
        }

        public string Name { get; }
        public string[] Modifiers { get; }
        public string Css { get; }
        public string? FamilyPrefix { get; }
        public string PreviewCss { get; internal set; }
    }

    internal sealed class Family
    {
        public Family(
            string prefix,
            string property,
            string arbitraryCss,
            string customPropertyCss)
        {
            Prefix = prefix;
            Property = property;
            PreviewProperty = property;
            ArbitraryCss = arbitraryCss;
            CustomPropertyCss = customPropertyCss;
        }

        public string Prefix { get; }
        public string Property { get; }
        public string PreviewProperty { get; internal set; }
        public string ArbitraryCss { get; }
        public string CustomPropertyCss { get; }
    }

    internal sealed class Variant
    {
        public Variant(string name, string condition)
        {
            Name = name;
            Condition = condition;
        }

        public string Name { get; }
        public string Condition { get; }
    }
}

internal static class FecsxTailwindCompletion
{
    private const string DropShadowDocumentation = "https://tailwindcss.com/docs/filter-drop-shadow";
    private const string ResponsiveDesignDocumentation = "https://tailwindcss.com/docs/responsive-design";
    private const string ArbitraryDropShadowExample = "0_35px_35px_rgba(0,0,0,0.25)";
    private const string ArbitraryDropShadowCssExample = "0 35px 35px rgba(0,0,0,0.25)";
    private static readonly string[] OpacityModifiers =
        Enumerable.Range(0, 21).Select(index => (index * 5).ToString(CultureInfo.InvariantCulture)).ToArray();

    public static FecsxTailwindCompletionContext? GetContext(string source, int position)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (position < 0 || position > source.Length) throw new ArgumentOutOfRangeException(nameof(position));

        var syntax = FecsxRenderDocumentParser.Parse(source);
        foreach (var component in syntax.Components)
        {
            foreach (var element in FecsxEditorService.Descendants(component.Body).OfType<FecsxElementSyntax>())
            {
                foreach (var attribute in element.Attributes.OfType<FecsxStringAttributeSyntax>())
                {
                    if (!string.Equals(attribute.Name, "className", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var value = new FecsxTextSpan(
                        component.BodyOffset + attribute.ValueSpan.Start,
                        attribute.ValueSpan.Length);
                    if (position < value.Start || position > value.End)
                    {
                        continue;
                    }

                    var start = position;
                    while (start > value.Start && !char.IsWhiteSpace(source[start - 1])) start--;
                    var end = position;
                    while (end < value.End && !char.IsWhiteSpace(source[end])) end++;
                    var token = source.Substring(start, end - start);
                    return token.Length == 0
                        ? null
                        : new FecsxTailwindCompletionContext(
                            new FecsxTextSpan(start, end - start),
                            token,
                            position - start);
                }
            }
        }
        return null;
    }

    public static IReadOnlyList<FecsxTailwindCompletionItem> GetCompletions(
        string source,
        int position,
        FecsxTailwindClassCatalog? projectCatalog = null)
    {
        var context = GetContext(source, position);
        if (context == null)
        {
            return Array.Empty<FecsxTailwindCompletionItem>();
        }

        SplitVariantPrefix(context.Token, out var variantPrefix, out var utility);
        var catalog = projectCatalog?.Catalog ?? TailwindClassCatalog.Current;
        if (TryGetDropShadowArbitraryCompletion(context, variantPrefix, utility, catalog.Version, out var arbitrary))
        {
            return new[] { arbitrary };
        }
        if (TryGetFamilyArbitraryCompletion(context, variantPrefix, utility, catalog, out arbitrary))
        {
            return new[] { arbitrary };
        }

        var slash = LastTopLevelCharacter(utility, '/');
        if (slash >= 0)
        {
            var baseName = utility.Substring(0, slash);
            var modifierPrefix = utility.Substring(slash + 1);
            var entry = catalog.Find(baseName);
            if (entry == null)
            {
                return Array.Empty<FecsxTailwindCompletionItem>();
            }

            var modifiers = entry.Modifiers.Length == 0 && IsDropShadowSize(baseName)
                ? OpacityModifiers
                : entry.Modifiers;
            return modifiers
                .Where(modifier => modifier.StartsWith(modifierPrefix, StringComparison.Ordinal))
                .Select(modifier =>
                {
                    var className = variantPrefix + baseName + "/" + modifier;
                    return new FecsxTailwindCompletionItem(
                        className,
                        className,
                        context.ReplacementSpan,
                        DescribeModifier(catalog, entry, modifier),
                        sortText: GetSortText(className));
                })
                .ToArray();
        }

        var results = new List<FecsxTailwindCompletionItem>();
        var variants = utility.Length <= 1
            ? catalog.Variants
            : catalog.Variants.Where(variant => variant.Name.StartsWith(utility, StringComparison.Ordinal));
        foreach (var variant in variants)
        {
            var className = variantPrefix + variant.Name;
            results.Add(new FecsxTailwindCompletionItem(
                className,
                className,
                context.ReplacementSpan,
                () => DescribeVariant(catalog, variant),
                sortText: GetVariantSortText(className)));
        }

        var activeFamily = catalog.FindContainingFamily(utility);
        if (activeFamily != null
            && utility.StartsWith(activeFamily.Prefix, StringComparison.Ordinal))
        {
            AddFamilyTemplates(results, context, variantPrefix, catalog, activeFamily);
        }

        // Visual Studio keeps filtering the source set from the start of an
        // async-completion session even if the user erases the entire token and
        // types a different first character. Seed one-letter sessions with the
        // complete grouped surface so that retargeted text can still find exact
        // standalone utilities and every generated utility family.
        var groupedSurface = activeFamily == null;
        var families = catalog.Families.Where(family =>
            (groupedSurface || !ReferenceEquals(family, activeFamily))
            && (utility.Length <= 1
                || family.Prefix.StartsWith(utility, StringComparison.Ordinal)));
        foreach (var family in families)
        {
            var className = variantPrefix + family.Prefix;
            results.Add(new FecsxTailwindCompletionItem(
                className,
                className,
                context.ReplacementSpan,
                () => DescribeFamily(catalog, family, variantPrefix),
                sortText: GetSortText(className)));
        }

        var entries = utility.Length <= 1 && groupedSurface
            ? catalog.FindByPrefix(string.Empty)
            : catalog.FindByPrefix(utility);
        if (activeFamily != null)
        {
            entries = entries.Where(entry =>
                entry.FamilyPrefix == null
                || string.Equals(
                    entry.FamilyPrefix,
                    activeFamily.Prefix,
                    StringComparison.Ordinal));
        }
        foreach (var entry in entries)
        {
            if (groupedSurface && entry.FamilyPrefix != null)
            {
                continue;
            }
            var className = variantPrefix + entry.Name;
            results.Add(new FecsxTailwindCompletionItem(
                className,
                className,
                context.ReplacementSpan,
                () => DescribeEntry(catalog, entry),
                sortText: GetSortText(className)));
        }
        return results;
    }

    public static bool NeedsDynamicRefresh(string source, int position)
    {
        var context = GetContext(source, position);
        if (context == null)
        {
            return false;
        }

        SplitVariantPrefix(context.Token, out var variantPrefix, out var utility);
        return (variantPrefix.Length > 0 && utility.Length == 0)
            || utility.EndsWith("-", StringComparison.Ordinal)
            || LastTopLevelCharacter(utility, '/') >= 0
            || utility.IndexOf("-[", StringComparison.Ordinal) >= 0
            || utility.IndexOf("-(", StringComparison.Ordinal) >= 0;
    }

    private static void AddFamilyTemplates(
        ICollection<FecsxTailwindCompletionItem> results,
        FecsxTailwindCompletionContext context,
        string variantPrefix,
        TailwindClassCatalog catalog,
        TailwindClassCatalog.Family family)
    {
        if (family.ArbitraryCss.Length > 0)
        {
            results.Add(CreateFamilyArbitraryTemplate(
                context,
                variantPrefix,
                catalog.Version,
                family,
                customProperty: false));
        }
        if (family.CustomPropertyCss.Length > 0)
        {
            results.Add(CreateFamilyArbitraryTemplate(
                context,
                variantPrefix,
                catalog.Version,
                family,
                customProperty: true));
        }
    }

    private static FecsxTailwindCompletionItem CreateFamilyArbitraryTemplate(
        FecsxTailwindCompletionContext context,
        string variantPrefix,
        string version,
        TailwindClassCatalog.Family family,
        bool customProperty)
    {
        if (string.Equals(family.Prefix, "drop-shadow-", StringComparison.Ordinal))
        {
            return CreateArbitraryTemplate(context, variantPrefix, version, customProperty);
        }

        var display = variantPrefix + family.Prefix + (customProperty
            ? "(<custom-property>)"
            : "[13px]");
        var insert = variantPrefix + family.Prefix + (customProperty ? "(--)" : "[]");
        var css = customProperty
            ? family.CustomPropertyCss.Replace("--value", "--padding")
            : family.ArbitraryCss;
        var explanation = customProperty
            ? "Uses a CSS custom property as this utility's value. Parentheses are shorthand for var(--custom-property)."
            : "Uses a one-off arbitrary value for this Tailwind utility family.";
        return new FecsxTailwindCompletionItem(
            display,
            insert,
            context.ReplacementSpan,
            TailwindDescription(explanation, css, version),
            caretOffsetFromEnd: 1,
            sortText: GetSortText(display));
    }

    private static bool TryGetFamilyArbitraryCompletion(
        FecsxTailwindCompletionContext context,
        string variantPrefix,
        string utility,
        TailwindClassCatalog catalog,
        out FecsxTailwindCompletionItem completion)
    {
        foreach (var family in catalog.Families.OrderByDescending(family => family.Prefix.Length))
        {
            var arbitraryPrefix = family.Prefix + "[";
            if (family.ArbitraryCss.Length > 0
                && utility.StartsWith(arbitraryPrefix, StringComparison.Ordinal))
            {
                var closed = utility.EndsWith("]", StringComparison.Ordinal);
                var value = utility.Substring(
                    arbitraryPrefix.Length,
                    utility.Length - arbitraryPrefix.Length - (closed ? 1 : 0));
                if (value.Length == 0)
                {
                    completion = CreateFamilyArbitraryTemplate(
                        context,
                        variantPrefix,
                        catalog.Version,
                        family,
                        customProperty: false);
                    return true;
                }

                var completedUtility = utility + (closed ? string.Empty : "]");
                var cssValue = value.Replace('_', ' ');
                completion = new FecsxTailwindCompletionItem(
                    variantPrefix + completedUtility,
                    variantPrefix + completedUtility,
                    context.ReplacementSpan,
                    TailwindDescription(
                        "Uses an arbitrary value for this Tailwind utility. In arbitrary values, each underscore maps to a CSS space.",
                        family.ArbitraryCss.Replace("13px", cssValue),
                        catalog.Version));
                return true;
            }

            var propertyPrefix = family.Prefix + "(";
            if (family.CustomPropertyCss.Length > 0
                && utility.StartsWith(propertyPrefix, StringComparison.Ordinal))
            {
                var closed = utility.EndsWith(")", StringComparison.Ordinal);
                var property = utility.Substring(
                    propertyPrefix.Length,
                    utility.Length - propertyPrefix.Length - (closed ? 1 : 0));
                if (property.Length == 0)
                {
                    completion = CreateFamilyArbitraryTemplate(
                        context,
                        variantPrefix,
                        catalog.Version,
                        family,
                        customProperty: true);
                    return true;
                }

                var completedUtility = utility + (closed ? string.Empty : ")");
                completion = new FecsxTailwindCompletionItem(
                    variantPrefix + completedUtility,
                    variantPrefix + completedUtility,
                    context.ReplacementSpan,
                    TailwindDescription(
                        "Uses a CSS custom property as this utility's value. This shorthand adds var() automatically.",
                        family.CustomPropertyCss.Replace("--value", property),
                        catalog.Version));
                return true;
            }
        }

        completion = null!;
        return false;
    }

    private static FecsxTailwindCompletionItem CreateArbitraryTemplate(
        FecsxTailwindCompletionContext context,
        string variantPrefix,
        string version,
        bool customProperty)
    {
        var display = variantPrefix + (customProperty
            ? "drop-shadow-(<custom-property>)"
            : $"drop-shadow-[{ArbitraryDropShadowExample}]");
        var insert = variantPrefix + (customProperty ? "drop-shadow-(--)" : "drop-shadow-[]");
        var explanation = customProperty
            ? "Uses a CSS custom property as the drop-shadow value. Parentheses are shorthand for var(--custom-property)."
            : "Uses a one-off drop-shadow value. In arbitrary values, each underscore maps to a CSS space.\n"
                + $"Example authored value: {ArbitraryDropShadowExample}\n"
                + $"Resulting CSS value: {ArbitraryDropShadowCssExample}";
        return new FecsxTailwindCompletionItem(
            display,
            insert,
            context.ReplacementSpan,
            TailwindDescription(explanation, customProperty
                ? "filter: drop-shadow(var(--custom-property));"
                : $"filter: drop-shadow({ArbitraryDropShadowCssExample});", version, DropShadowDocumentation),
            caretOffsetFromEnd: 1,
            sortText: GetSortText(display));
    }

    private static bool TryGetDropShadowArbitraryCompletion(
        FecsxTailwindCompletionContext context,
        string variantPrefix,
        string utility,
        string version,
        out FecsxTailwindCompletionItem completion)
    {
        const string arbitraryPrefix = "drop-shadow-[";
        const string propertyPrefix = "drop-shadow-(";
        if (utility.StartsWith(arbitraryPrefix, StringComparison.Ordinal))
        {
            var closed = utility.EndsWith("]", StringComparison.Ordinal);
            var value = utility.Substring(arbitraryPrefix.Length, utility.Length - arbitraryPrefix.Length - (closed ? 1 : 0));
            if (value.Length == 0 && !closed)
            {
                completion = CreateArbitraryTemplate(
                    context,
                    variantPrefix,
                    version,
                    customProperty: false);
                return true;
            }
            var completedUtility = utility + (closed ? string.Empty : "]");
            var cssValue = value.Replace('_', ' ');
            var parameterDescription = DescribeDropShadowParameter(
                value,
                GetUtilityCaretOffset(context, variantPrefix) - arbitraryPrefix.Length);
            completion = new FecsxTailwindCompletionItem(
                variantPrefix + completedUtility,
                variantPrefix + completedUtility,
                context.ReplacementSpan,
                TailwindDescription(
                    "Applies a drop-shadow filter to the element's rendered shape using Tailwind CSS.\n"
                        + "Syntax: drop-shadow-[offset-x_offset-y_blur_color]\n\n"
                        + parameterDescription + "\n\n"
                        + "In arbitrary values, each underscore maps to a CSS space.\n"
                        + $"Authored value: {value}\n"
                        + $"Resulting CSS value: {cssValue}",
                    $"filter: drop-shadow({cssValue});",
                    version,
                    DropShadowDocumentation));
            return true;
        }

        if (utility.StartsWith(propertyPrefix, StringComparison.Ordinal))
        {
            var closed = utility.EndsWith(")", StringComparison.Ordinal);
            var property = utility.Substring(propertyPrefix.Length, utility.Length - propertyPrefix.Length - (closed ? 1 : 0));
            var completedUtility = utility + (closed ? string.Empty : ")");
            completion = new FecsxTailwindCompletionItem(
                variantPrefix + completedUtility,
                variantPrefix + completedUtility,
                context.ReplacementSpan,
                TailwindDescription(
                    "Applies a drop shadow from a CSS custom property. This shorthand adds var() automatically.",
                    $"filter: drop-shadow(var({property}));",
                    version,
                    DropShadowDocumentation));
            return true;
        }

        completion = null!;
        return false;
    }

    private static int GetUtilityCaretOffset(
        FecsxTailwindCompletionContext context,
        string variantPrefix) =>
        Math.Min(
            Math.Max(context.CaretOffset - variantPrefix.Length, 0),
            context.Token.Length - variantPrefix.Length);

    private static string DescribeDropShadowParameter(string value, int caretOffset)
    {
        var parameter = GetDropShadowParameterIndex(value, caretOffset);
        return parameter switch
        {
            0 => "Active parameter: offset-x. This is the shadow's horizontal offset and accepts a CSS length. Positive values move the shadow right; negative values move it left.",
            1 => "Active parameter: offset-y. This is the shadow's vertical offset and accepts a CSS length. Positive values move the shadow down; negative values move it up.",
            2 => "Active parameter: blur. This is a non-negative CSS length used as the Gaussian blur standard deviation. Zero creates a sharp edge; larger values make the shadow wider and more diffuse.",
            _ => "Active parameter: color. This accepts a CSS color such as rgba(), a hexadecimal color, a named color, or a CSS custom property.",
        };
    }

    private static int GetDropShadowParameterIndex(string value, int caretOffset)
    {
        var limit = Math.Min(Math.Max(caretOffset, 0), value.Length);
        var parenthesisDepth = 0;
        var parameter = 0;
        for (var index = 0; index < limit; index++)
        {
            switch (value[index])
            {
                case '(':
                    parenthesisDepth++;
                    break;
                case ')':
                    parenthesisDepth = Math.Max(0, parenthesisDepth - 1);
                    break;
                case '_' when parenthesisDepth == 0:
                    parameter = Math.Min(parameter + 1, 3);
                    break;
            }
        }
        return parameter;
    }

    private static string GetSortText(string className)
    {
        var family = className.EndsWith("-", StringComparison.Ordinal);
        var stem = family ? className.Substring(0, className.Length - 1) : className;
        return stem.Length.ToString("D5", CultureInfo.InvariantCulture)
            + ":" + stem + (family ? " 1:" : " 0:") + className;
    }

    private static string GetVariantSortText(string className)
    {
        var stem = className.EndsWith(":", StringComparison.Ordinal)
            ? className.Substring(0, className.Length - 1)
            : className;
        return stem.Length.ToString("D5", CultureInfo.InvariantCulture)
            + ":" + stem + " 0:" + className;
    }

    private static string DescribeFamily(
        TailwindClassCatalog catalog,
        TailwindClassCatalog.Family family,
        string variantPrefix)
    {
        var entries = catalog.FindByPrefix(family.Prefix)
            .Where(entry => string.Equals(
                entry.FamilyPrefix,
                family.Prefix,
                StringComparison.Ordinal))
            .ToArray();
        var property = family.Property.Length > 0
            ? family.Property
            : entries.Select(entry => GetPrimaryCssProperty(entry.Css))
                .FirstOrDefault(value => value.Length > 0) ?? string.Empty;
        var builder = new StringBuilder()
            .Append("Tailwind CSS v").Append(catalog.Version).AppendLine()
            .AppendLine(DescribeFamilyPurpose(property, family.PreviewProperty))
            .AppendLine()
            .Append("Available classes in `").Append(variantPrefix).Append(family.Prefix).AppendLine("`:");
        foreach (var entry in entries)
        {
            builder.Append(variantPrefix).Append(entry.Name).Append(" → ")
                .AppendLine(DescribeFamilyEntryPreview(catalog, entry));
        }

        if (family.ArbitraryCss.Length > 0)
        {
            builder.Append(variantPrefix).Append(family.Prefix).Append("[13px] → ")
                .AppendLine(DescribeCssDeclaration(
                    catalog,
                    family.ArbitraryCss,
                    family.PreviewProperty));
        }
        if (family.CustomPropertyCss.Length > 0)
        {
            var css = family.CustomPropertyCss.Replace("--value", "--padding");
            builder.Append(variantPrefix).Append(family.Prefix).Append("[var(--padding)] → ")
                .AppendLine(DescribeCssDeclaration(catalog, css, family.PreviewProperty));
            builder.Append(variantPrefix).Append(family.Prefix).Append("(--padding) → ")
                .AppendLine(DescribeCssDeclaration(catalog, css, family.PreviewProperty));
        }
        return builder.ToString().TrimEnd();
    }

    private static string DescribeFamilyPurpose(
        string property,
        string previewProperty) => previewProperty switch
    {
        "--tw-ring-shadow" => "Draws an outline ring around the element.",
        "--tw-inset-ring-shadow" => "Draws an inset outline ring inside the element.",
        _ => property switch
        {
            "margin" => "Adds whitespace outside the box.",
            "padding" => "Adds whitespace inside the box.",
            "background-color" => "Sets the element's background color.",
            "background-position" => "Positions the element's background image.",
            "background-size" => "Controls the size of the element's background image.",
            "flex-grow" => "Controls how the item grows to fill available flex space.",
            "box-shadow" => "Adds or configures a shadow around the element.",
            "--tw-ring-color" => "Sets the color of the element's outline ring.",
            "" => "Selects a value for this Tailwind utility.",
            _ when property.StartsWith("--", StringComparison.Ordinal) =>
                "Configures this Tailwind utility.",
            _ => $"Controls the element's CSS `{property}` property.",
        },
    };

    internal static string DescribeFamilyEntryPreview(
        TailwindClassCatalog catalog,
        TailwindClassCatalog.Entry entry) =>
        DescribeCssPreview(catalog, entry.PreviewCss);

    private static string DescribeCssPreview(
        TailwindClassCatalog catalog,
        string css)
    {
        var resolved = ResolveSpacingCss(catalog, css);
        var declarations = TailwindClassCatalog.ParseCssDeclarations(resolved);
        return declarations.Count == 0
            ? resolved.Trim()
            : string.Join("; ", declarations.Select(FormatCssDeclaration));
    }

    private static string GetPrimaryCssProperty(string css)
    {
        string? customProperty = null;
        foreach (var declaration in css.Split(';'))
        {
            var separator = declaration.IndexOf(':');
            if (separator <= 0) continue;
            var property = declaration.Substring(0, separator).Trim();
            if (property.Length == 0) continue;
            if (!property.StartsWith("--tw-", StringComparison.Ordinal)) return property;
            customProperty ??= property;
        }
        return customProperty ?? string.Empty;
    }

    private static string DescribeEntry(TailwindClassCatalog catalog, TailwindClassCatalog.Entry entry)
    {
        if (entry.Name.StartsWith("drop-shadow-", StringComparison.Ordinal))
        {
            var explanation = entry.Css.IndexOf("--tw-drop-shadow-color:", StringComparison.Ordinal) >= 0
                ? "Sets the color used by Tailwind drop-shadow filters."
                : "Applies a drop-shadow filter to the element's rendered shape, which is especially useful for text and SVG artwork.";
            return TailwindDescription(explanation, entry.Css, catalog.Version, DropShadowDocumentation);
        }

        return TailwindDescription(
            DescribeCss(ResolveSpacingCss(catalog, entry.Css)),
            entry.Css,
            catalog.Version);
    }

    private static string DescribeVariant(
        TailwindClassCatalog catalog,
        TailwindClassCatalog.Variant variant) =>
        TailwindDescription(
            "Applies the selected utility when this Tailwind variant condition matches.",
            variant.Condition,
            catalog.Version,
            ResponsiveDesignDocumentation,
            generatedLabel: "Variant condition:");

    private static string DescribeModifier(
        TailwindClassCatalog catalog,
        TailwindClassCatalog.Entry entry,
        string modifier)
    {
        if (entry.Name.StartsWith("drop-shadow-", StringComparison.Ordinal))
        {
            return TailwindDescription(
                $"Sets the opacity of `{entry.Name}` to {modifier}%. The slash introduces the opacity parameter without changing the selected size or color.",
                $"--tw-drop-shadow-alpha: {modifier}%;\n{entry.Css}",
                catalog.Version,
                DropShadowDocumentation);
        }
        return TailwindDescription(
            $"Applies the `{modifier}` modifier to `{entry.Name}`.",
            entry.Css,
            catalog.Version);
    }

    private static string DescribeCss(string css)
    {
        foreach (var declaration in css.Split(';'))
        {
            var separator = declaration.IndexOf(':');
            if (separator <= 0) continue;
            var property = declaration.Substring(0, separator).Trim();
            var value = declaration.Substring(separator + 1).Trim();
            if (!property.StartsWith("--tw-", StringComparison.Ordinal) && value.Length > 0)
            {
                return $"Sets the CSS `{property}` property to `{value}`.";
            }
        }
        return "Configures the selected Tailwind utility.";
    }

    private static string DescribeCssDeclaration(
        TailwindClassCatalog catalog,
        string css,
        string? preferredProperty = null)
    {
        var resolved = ResolveSpacingCss(catalog, css);
        var declarations = TailwindClassCatalog.ParseCssDeclarations(resolved);
        var selected = preferredProperty == null
            ? null
            : declarations.FirstOrDefault(declaration => string.Equals(
                declaration.Property,
                preferredProperty,
                StringComparison.Ordinal));
        selected ??= declarations.FirstOrDefault(declaration =>
            !declaration.Property.StartsWith("--", StringComparison.Ordinal));
        selected ??= declarations.FirstOrDefault();
        return selected == null ? resolved.Trim() : FormatCssDeclaration(selected);
    }

    private static string FormatCssDeclaration(
        TailwindClassCatalog.CssDeclaration declaration)
    {
        if (string.Equals(
                declaration.Property,
                "--tw-ring-shadow",
                StringComparison.Ordinal))
        {
            const string startMarker = "calc(";
            const string endMarker = " + var(--tw-ring-offset-width))";
            var start = declaration.Value.IndexOf(startMarker, StringComparison.Ordinal);
            var end = start < 0
                ? -1
                : declaration.Value.IndexOf(endMarker, start, StringComparison.Ordinal);
            if (start >= 0 && end > start)
            {
                return "ring width: " + declaration.Value.Substring(
                    start + startMarker.Length,
                    end - start - startMarker.Length);
            }
        }
        if (string.Equals(
                declaration.Property,
                "--tw-inset-ring-shadow",
                StringComparison.Ordinal))
        {
            const string colorMarker = " var(--tw-inset-ring-color";
            var end = declaration.Value.IndexOf(colorMarker, StringComparison.Ordinal);
            var value = end < 0 ? declaration.Value : declaration.Value.Substring(0, end);
            var separator = value.LastIndexOf(' ');
            if (separator >= 0 && separator + 1 < value.Length)
            {
                return "inset ring width: " + value.Substring(separator + 1);
            }
        }

        if (!declaration.Property.StartsWith("--", StringComparison.Ordinal))
        {
            return declaration.Css;
        }

        var name = declaration.Property.StartsWith("--tw-", StringComparison.Ordinal)
            ? declaration.Property.Substring("--tw-".Length)
            : declaration.Property.Substring(2);
        var label = name.Replace('-', ' ');
        var valueText = declaration.Value;
        var opening = valueText.IndexOf('(');
        if (opening > 0 && valueText.EndsWith(")", StringComparison.Ordinal))
        {
            var function = valueText.Substring(0, opening);
            var normalizedFunction = new string(function
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
            var normalizedName = new string(name
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
            if (string.Equals(normalizedFunction, normalizedName, StringComparison.Ordinal))
            {
                valueText = valueText.Substring(
                    opening + 1,
                    valueText.Length - opening - 2);
            }
        }
        return label + ": " + valueText;
    }

    private static string ResolveSpacingCss(TailwindClassCatalog catalog, string css)
    {
        if (!TryConvertCssLengthToPixels(catalog.Spacing, out var spacingPixels))
        {
            return css;
        }

        var declarations = css.Split(';');
        for (var index = 0; index < declarations.Length; index++)
        {
            var separator = declarations[index].IndexOf(':');
            if (separator <= 0) continue;
            var valueStart = separator + 1;
            var value = declarations[index].Substring(valueStart).Trim();
            if (TryResolveSpacingValue(value, spacingPixels, out var resolved))
            {
                declarations[index] = declarations[index].Substring(0, valueStart) + " " + resolved;
            }
        }
        return string.Join(";", declarations);
    }

    private static bool TryResolveSpacingValue(
        string value,
        decimal spacingPixels,
        out string resolved)
    {
        const string prefix = "calc(var(--spacing) * ";
        if (string.Equals(value, "var(--spacing)", StringComparison.Ordinal))
        {
            resolved = FormatPixels(spacingPixels);
            return true;
        }
        if (value.StartsWith(prefix, StringComparison.Ordinal)
            && value.EndsWith(")", StringComparison.Ordinal)
            && decimal.TryParse(
                value.Substring(prefix.Length, value.Length - prefix.Length - 1),
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var multiplier))
        {
            resolved = FormatPixels(spacingPixels * multiplier);
            return true;
        }

        resolved = string.Empty;
        return false;
    }

    private static bool TryConvertCssLengthToPixels(string value, out decimal pixels)
    {
        var trimmed = value.Trim();
        decimal multiplier;
        string number;
        if (trimmed.EndsWith("px", StringComparison.Ordinal))
        {
            multiplier = 1m;
            number = trimmed.Substring(0, trimmed.Length - 2);
        }
        else if (trimmed.EndsWith("rem", StringComparison.Ordinal))
        {
            multiplier = 16m;
            number = trimmed.Substring(0, trimmed.Length - 3);
        }
        else
        {
            pixels = 0;
            return false;
        }

        if (!decimal.TryParse(
                number,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var amount))
        {
            pixels = 0;
            return false;
        }
        pixels = amount * multiplier;
        return true;
    }

    private static string FormatPixels(decimal pixels) =>
        pixels.ToString("0.####", CultureInfo.InvariantCulture) + "px";

    private static string TailwindDescription(
        string explanation,
        string css,
        string version,
        string? documentation = null,
        string generatedLabel = "Generated CSS:")
    {
        var builder = new StringBuilder()
            .Append("Tailwind CSS v").Append(version).AppendLine()
            .AppendLine(explanation)
            .AppendLine()
            .AppendLine(generatedLabel)
            .AppendLine(css);
        if (documentation != null)
        {
            builder.AppendLine().Append("Documentation: ").Append(documentation);
        }
        return builder.ToString().TrimEnd();
    }

    private static bool IsDropShadowSize(string className) => className is
        "drop-shadow-xs" or "drop-shadow-sm" or "drop-shadow-md" or
        "drop-shadow-lg" or "drop-shadow-xl" or "drop-shadow-2xl";

    private static void SplitVariantPrefix(string token, out string variantPrefix, out string utility)
    {
        var colon = LastTopLevelCharacter(token, ':');
        if (colon < 0)
        {
            variantPrefix = string.Empty;
            utility = token;
        }
        else
        {
            variantPrefix = token.Substring(0, colon + 1);
            utility = token.Substring(colon + 1);
        }
    }

    private static int LastTopLevelCharacter(string value, char target)
    {
        var squareDepth = 0;
        var parenthesisDepth = 0;
        var last = -1;
        for (var index = 0; index < value.Length; index++)
        {
            switch (value[index])
            {
                case '[': squareDepth++; break;
                case ']': squareDepth = Math.Max(0, squareDepth - 1); break;
                case '(': parenthesisDepth++; break;
                case ')': parenthesisDepth = Math.Max(0, parenthesisDepth - 1); break;
                default:
                    if (value[index] == target && squareDepth == 0 && parenthesisDepth == 0) last = index;
                    break;
            }
        }
        return last;
    }
}
