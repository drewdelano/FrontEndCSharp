using Interop.JS.Browser;

namespace FrontEndCSharp.ReactDemo;

public static class Actions
{
    public static void ToggleFocus(OrbitStore store) =>
        store.IsRunning = !store.IsRunning;

    public static void CycleLength(OrbitStore store)
    {
        store.FocusLength = store.FocusLength switch
        {
            25 => 45,
            45 => 15,
            _ => 25,
        };
        var focusMode = store.FocusLength switch
        {
            15 => "reset",
            45 => "deep dive",
            _ => "session",
        };
        store.FocusLengthLabel = $"{store.FocusLength} minute {focusMode}";
    }

    public static void SetEnergy(OrbitStore store, int level) =>
        store.EnergyLevel = level;

    public static void UpdateFocusIntent(OrbitStore store, DomEvent domEvent)
    {
        var value = ReadFormValue(domEvent);
        store.FocusIntent = value;
        ClearFocusIntentValidation(store);
    }

    public static void UpdateAccountabilityEmail(OrbitStore store, DomEvent domEvent)
    {
        var value = ReadFormValue(domEvent);
        store.AccountabilityEmail = value;
        ClearFocusIntentValidation(store);
    }

    public static void UpdatePriority(OrbitStore store, DomEvent domEvent)
    {
        var value = ReadFormValue(domEvent);
        store.PriorityInput = value;
        ClearFocusIntentValidation(store);
    }

    public static void SubmitFocusIntent(OrbitStore store, DomEvent domEvent)
    {
        domEvent.PreventDefault();
        var result = FocusIntentFormValidation.Validate(
            new FocusIntentForm
            {
                FocusIntent = store.FocusIntent,
                AccountabilityEmail = store.AccountabilityEmail,
                Priority = ParsePriority(store.PriorityInput),
            });
        store.FocusIntentError = result.ErrorFor(model => model.FocusIntent);
        store.AccountabilityEmailError = result.ErrorFor(model => model.AccountabilityEmail);
        store.PriorityError = result.ErrorFor(model => model.Priority);
        store.FocusIntentSuccess = result.IsValid
            ? $"Intent saved: {store.FocusIntent}"
            : string.Empty;
    }

    private static string ReadFormValue(DomEvent domEvent) =>
        (domEvent.CurrentTarget
            ?? throw new InvalidOperationException("The input event has no current target."))
        .Value;

    private static int ParsePriority(string value) =>
        value == "1" ? 1
        : value == "2" ? 2
        : value == "3" ? 3
        : 0;

    private static void ClearFocusIntentValidation(OrbitStore store)
    {
        store.FocusIntentError = string.Empty;
        store.AccountabilityEmailError = string.Empty;
        store.PriorityError = string.Empty;
        store.FocusIntentSuccess = string.Empty;
    }

    public static void ToggleTask(OrbitStore store, int task)
    {
        if (task == 1)
        {
            store.FirstTaskDone = !store.FirstTaskDone;
        }
        else if (task == 2)
        {
            store.SecondTaskDone = !store.SecondTaskDone;
        }
        else
        {
            store.ThirdTaskDone = !store.ThirdTaskDone;
        }
    }
}
