using Interop.JS.Browser;
using System;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Interop.JS.React;

public abstract class ReactNode
{
    internal ReactNode()
    {
    }
}

public sealed class ReactElement : ReactNode
{
    private ReactElement()
    {
    }
}

public sealed class ReactComponent
{
    private ReactComponent()
    {
    }
}

public sealed class ImportedReactComponent
{
    private ImportedReactComponent()
    {
    }
}

public sealed class ImportedReactElementProps
{
    public ImportedReactComponent? Component { get; set; }

    public ReactProps? Props { get; set; }
}

public static class ImportedReactElement
{
    public static ReactElement Create(
        Action<ImportedReactElementProps> configure,
        params ReactNode[] children)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var wrapper = new ImportedReactElementProps();
        configure(wrapper);
        if (wrapper.Component is null)
        {
            throw new ArgumentException("An imported React component is required.", nameof(configure));
        }
        return React.CreateElement(wrapper.Component, wrapper.Props, children);
    }
}

public sealed class ReactComponent<TProps>
    where TProps : class
{
    private ReactComponent()
    {
    }
}

public class ReactProps
{
    private ReactPropertyDictionary? _properties;

    protected ReactProps()
    {
        _properties = new ReactPropertyDictionary();
    }

    private ReactProps(ReactPropertyDictionary properties)
    {
        _properties = properties.Snapshot();
    }

    public ReactPropertyDictionary Properties =>
        _properties ??= new ReactPropertyDictionary();

    public string? Id
    {
        get => GetReferenceProperty<string>("id");
        set => Properties["id"] = value;
    }

    public string? ClassName
    {
        get => GetReferenceProperty<string>("className");
        set => Properties["className"] = value;
    }

    public string? Type
    {
        get => GetReferenceProperty<string>("type");
        set => Properties["type"] = value;
    }

    public string? Title
    {
        get => GetReferenceProperty<string>("title");
        set => Properties["title"] = value;
    }

    public string? Role
    {
        get => GetReferenceProperty<string>("role");
        set => Properties["role"] = value;
    }

    public string? AriaLabel
    {
        get => GetReferenceProperty<string>("aria-label");
        set => Properties["aria-label"] = value;
    }

    public bool Hidden
    {
        get => GetValueProperty<bool>("hidden");
        set => Properties["hidden"] = value;
    }

    public double TabIndex
    {
        get => GetValueProperty<double>("tabIndex");
        set => Properties["tabIndex"] = value;
    }

    public Action<DomEvent>? OnBlur
    {
        get => GetReferenceProperty<Action<DomEvent>>("onBlur");
        set => Properties["onBlur"] = value;
    }

    public Action<DomEvent>? OnChange
    {
        get => GetReferenceProperty<Action<DomEvent>>("onChange");
        set => Properties["onChange"] = value;
    }

    public Action<DomEvent>? OnClick
    {
        get => GetReferenceProperty<Action<DomEvent>>("onClick");
        set => Properties["onClick"] = value;
    }

    public Action<DomEvent>? OnFocus
    {
        get => GetReferenceProperty<Action<DomEvent>>("onFocus");
        set => Properties["onFocus"] = value;
    }

    public Action<DomEvent>? OnInput
    {
        get => GetReferenceProperty<Action<DomEvent>>("onInput");
        set => Properties["onInput"] = value;
    }

    public Action<DomEvent>? OnSubmit
    {
        get => GetReferenceProperty<Action<DomEvent>>("onSubmit");
        set => Properties["onSubmit"] = value;
    }

    public static ReactProps Create() => new ReactProps();

    public ReactProps SetString(string name, string? value)
    {
        Properties[name] = value;
        return this;
    }

    public ReactProps SetNumber(string name, double value)
    {
        Properties[name] = value;
        return this;
    }

    public ReactProps SetBoolean(string name, bool value)
    {
        Properties[name] = value;
        return this;
    }

    public ReactProps SetEventHandler(string name, Action<DomEvent>? handler)
    {
        Properties[name] = handler;
        return this;
    }

    public string? GetString(string name) => GetReferenceProperty<string>(name);

    protected T? GetReferenceProperty<T>(string name)
        where T : class
    {
        if (!Properties.TryGetValue(name, out var value) || value == null)
        {
            return null;
        }
        return (T)value;
    }

    protected T GetValueProperty<T>(string name)
        where T : struct
    {
        if (!Properties.TryGetValue(name, out var value))
        {
            return default;
        }
        return (T)value!;
    }

    internal string[] GetPropertyNames()
    {
        return Properties.GetNames();
    }

    internal object? GetPropertyValue(string name) => Properties[name];

    internal ReactProps Snapshot() => new ReactProps(Properties);
}

public static class React
{
    [MethodImpl(MethodImplOptions.InternalCall)]
    public static extern ReactNode Text(string value);

    public static ReactNode Node(ReactNode value) => value;

    public static ReactNode Node(ReactNode[] value) => CreateFragment(value);

    public static ReactNode Node(string value) => Text(value);

    public static ReactNode Node(char value) => Text(value.ToString());

    public static ReactNode Node(int value) => Text(value.ToString(CultureInfo.InvariantCulture));

    public static ReactNode Node(long value) => Text(value.ToString(CultureInfo.InvariantCulture));

    public static ReactNode Node(float value) => Text(value.ToString(CultureInfo.InvariantCulture));

    public static ReactNode Node(double value) => Text(value.ToString(CultureInfo.InvariantCulture));

    public static ReactNode Node(decimal value) => Text(value.ToString(CultureInfo.InvariantCulture));

    [MethodImpl(MethodImplOptions.InternalCall)]
    public static extern ReactComponent Component(Func<ReactProps, ReactNode> render);

    public static ReactComponent<TProps> Component<TProps>(
        Func<TProps, ReactNode[], ReactNode> render)
        where TProps : class =>
        throw new PlatformNotSupportedException("React is available only in FrontEndCSharp browser output.");

    [MethodImpl(MethodImplOptions.InternalCall)]
    public static extern ReactElement CreateElement(
        string elementName,
        ReactProps? props,
        params ReactNode[] children);

    [MethodImpl(MethodImplOptions.InternalCall)]
    public static extern ReactElement CreateFragment(params ReactNode[] children);

    [MethodImpl(MethodImplOptions.InternalCall)]
    public static extern ReactElement CreateElement(
        ReactComponent component,
        ReactProps? props,
        params ReactNode[] children);

    [MethodImpl(MethodImplOptions.InternalCall)]
    public static extern ReactElement CreateElement(
        ImportedReactComponent component,
        ReactProps? props,
        params ReactNode[] children);

    public static ReactElement CreateElement<TProps>(
        ReactComponent<TProps> component,
        TProps props,
        params ReactNode[] children)
        where TProps : class =>
        throw new PlatformNotSupportedException("React is available only in FrontEndCSharp browser output.");
}

public sealed class ReactRoot
{
    private ReactRoot()
    {
    }

    [MethodImpl(MethodImplOptions.InternalCall)]
    public static extern ReactRoot Create(Element container);

    [MethodImpl(MethodImplOptions.InternalCall)]
    public extern void Render(ReactNode node);

    [MethodImpl(MethodImplOptions.InternalCall)]
    public extern void Unmount();
}
