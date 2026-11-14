using Interop.JS.Browser;
using Interop.JS.React;

namespace $safeprojectname$;

public static class Program
{
    public static void Main()
    {
        Document.Title = "FrontEndCSharp FECSX React application";
        var host = Document.GetElementById("app")
            ?? throw new InvalidOperationException("The React host element was not found.");
        var root = ReactRoot.Create(host);
        var count = 0;

        void Render() => root.Render(App.Create(props =>
        {
            props.Count = count;
            props.Increment = () =>
            {
                count++;
                Render();
            };
        }));

        Render();
    }
}
