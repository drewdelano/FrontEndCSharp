using FluentValidation;
using FluentValidation.Results;
using FluentValidation.Resources;
using System.Globalization;

var model = new Order
{
    Code = string.Empty,
    Mode = "strict",
    Customer = new Customer(),
    Lines = new List<Line> { new() },
    AsyncValue = "bad"
};

ValidatorOptions.Global.DisableAccessorCache = true;
ValidatorOptions.Global.LanguageManager = new FixtureLanguageManager();
var syncValidator = new OrderValidator();
var sync = syncValidator.Validate(model);
Console.WriteLine(sync.Errors.Count == 4);
foreach (var failure in sync.Errors)
{
    Console.WriteLine(failure.PropertyName);
    Console.WriteLine(failure.ErrorCode);
}
var codeFailure = sync.Errors[1];
Console.WriteLine(codeFailure.Severity == Severity.Warning);
Console.WriteLine((string?)codeFailure.CustomState);
Console.WriteLine(codeFailure.AttemptedValue is string);

var loose = syncValidator.Validate(new Order
{
    Code = string.Empty,
    Mode = "loose",
    Customer = new Customer { Name = "ok" },
    Lines = new List<Line> { new() { Sku = "ok" } }
});
Console.WriteLine(loose.Errors.Count == 0);

var publish = syncValidator.Validate(
    model,
    options => options.IncludeRuleSets("publish"));
Console.WriteLine(publish.Errors[0].PropertyName);

var asyncValidator = new AsyncOrderValidator();
var asyncResult = await asyncValidator.ValidateAsync(model);
Console.WriteLine(asyncResult.Errors.Count == 1);
Console.WriteLine(asyncResult.Errors[0].PropertyName);
Console.WriteLine(asyncResult.Errors[0].ErrorCode);
try
{
    asyncValidator.Validate(model);
}
catch (AsyncValidatorInvokedSynchronouslyException)
{
    Console.WriteLine("async-sync-failure");
}

using var cancellation = new CancellationTokenSource();
cancellation.Cancel();
try
{
    await asyncValidator.ValidateAsync(model, cancellation.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("cancelled");
}

sealed class OrderValidator : AbstractValidator<Order>
{
    public OrderValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        When(order => order.Mode == "strict", () =>
        {
            RuleFor(order => order.Code)
                .NotEmpty()
                .Length(3);
            RuleFor(order => order.Code).Custom((code, context) =>
            {
                if (!code.StartsWith("X", StringComparison.Ordinal))
                {
                    context.AddFailure(new ValidationFailure("Code", "must start with X")
                    {
                        ErrorCode = "starts-x",
                        Severity = Severity.Warning,
                        CustomState = "form",
                        AttemptedValue = code
                    });
                }
            });
        });
        RuleFor(order => order.Customer!).SetValidator(new CustomerValidator());
        RuleForEach(order => order.Lines).SetValidator(new LineValidator());
        RuleSet("publish", () =>
            RuleFor(order => order.Mode)
                .Equal("publish")
                .DependentRules(() => RuleFor(order => order.Code).NotEmpty()));
    }
}

sealed class AsyncOrderValidator : AbstractValidator<Order>
{
    public AsyncOrderValidator()
    {
        RuleFor(order => order.AsyncValue)
            .MustAsync((value, cancellation) =>
            {
                cancellation.ThrowIfCancellationRequested();
                return Task.FromResult(value == "ok");
            });
    }
}

sealed class CustomerValidator : AbstractValidator<Customer>
{
    public CustomerValidator() => RuleFor(customer => customer.Name).NotEmpty();
}

sealed class LineValidator : AbstractValidator<Line>
{
    public LineValidator() => RuleFor(line => line.Sku).NotEmpty();
}

sealed class Order
{
    public string Code { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public Customer? Customer { get; set; }
    public List<Line> Lines { get; set; } = new();
    public string? AsyncValue { get; set; }
}

sealed class Customer
{
    public string? Name { get; set; }
}

sealed class Line
{
    public string? Sku { get; set; }
}

sealed class FixtureLanguageManager : ILanguageManager
{
    public bool Enabled { get; set; } = true;
    public CultureInfo? Culture { get; set; }

    public string GetString(string key, CultureInfo? culture = null) =>
        "'{PropertyName}' is invalid.";
}
