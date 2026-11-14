using FrontEndCSharp.Debugger.Integration;

namespace FrontEndCSharp.Debugger.Tests;

public sealed class VisualStudioCompletionItemIdentityTests
{
    [TestCase("ReactDemo", "ReactDemo", "ReactDemo. 1 of 1")]
    [TestCase("ReactDemo", "", "ReactDemo")]
    [TestCase("App.Footer", "", "App.Footer. 2 of 3")]
    [TestCase("drop-shadow-xl", "", "drop-shadow-xl. 1 of 300 ")]
    public void MatchesAutomationIdsAndAccessibleNames(
        string expected,
        string automationId,
        string accessibleName)
    {
        Assert.True(VisualStudioCompletionItemIdentity.Matches(
            expected,
            automationId,
            accessibleName));
    }

    [TestCase("ReactDemo", "", "ReactDemoExtra. 1 of 1")]
    [TestCase("ReactDemo", "Other", "Other. 1 of 1")]
    public void RejectsDifferentCompletionItems(
        string expected,
        string automationId,
        string accessibleName)
    {
        Assert.False(VisualStudioCompletionItemIdentity.Matches(
            expected,
            automationId,
            accessibleName));
    }
}
