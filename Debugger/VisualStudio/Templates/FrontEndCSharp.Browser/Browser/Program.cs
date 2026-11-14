using Interop.JS.Browser;

namespace $safeprojectname$;

public static class Program
{
    public static void Main()
    {
        Document.Title = "FrontEndCSharp browser application";

        var message = Document.GetElementById("message")
            ?? throw new InvalidOperationException("The message element was not found.");
        var counter = Document.GetElementById("counter")
            ?? throw new InvalidOperationException("The counter button was not found.");
        message.InnerText = "Managed C# is running in the browser.";
        counter.AddEventListener("click", _ =>
        {
            counter.InnerText = "Clicked from managed C#.";
        });
    }
}
