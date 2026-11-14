using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;

namespace FrontEndCSharp.ReactDemo;

public sealed class FocusIntentForm
{
    [Required(ErrorMessage = "Name one focus outcome.")]
    [StringLength(80, MinimumLength = 3, ErrorMessage = "Use between 3 and 80 characters.")]
    public string? FocusIntent { get; set; }

    [Required(ErrorMessage = "Add an accountability email.")]
    [EmailAddress(ErrorMessage = "Enter a valid accountability email.")]
    public string? AccountabilityEmail { get; set; }

    [Range(1, 3, ErrorMessage = "Choose a priority from 1 to 3.")]
    public int Priority { get; set; }
}

public sealed record FocusIntentFieldError(string FieldName, string ErrorMessage);

public sealed class FocusIntentValidationResult
{
    private readonly IReadOnlyList<FocusIntentFieldError> _errors;

    public FocusIntentValidationResult(IReadOnlyList<FocusIntentFieldError> errors)
    {
        _errors = errors ?? throw new ArgumentNullException(nameof(errors));
    }

    public bool IsValid => _errors.Count == 0;

    public string ErrorFor<TValue>(Expression<Func<FocusIntentForm, TValue>> selector)
    {
        var fieldName = FormField<FocusIntentForm>.MemberNameFor(selector);
        for (var index = 0; index < _errors.Count; index++)
        {
            var error = _errors[index];
            if (error.FieldName == fieldName)
            {
                return error.ErrorMessage;
            }
        }

        return string.Empty;
    }
}

public static class FocusIntentFormValidation
{
    public static FocusIntentValidationResult Validate(FocusIntentForm model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(
            model,
            new ValidationContext(model),
            validationResults,
            validateAllProperties: true);
        var errors = new List<FocusIntentFieldError>();
        foreach (var validationResult in validationResults)
        {
            foreach (var memberName in validationResult.MemberNames.DefaultIfEmpty(string.Empty))
            {
                errors.Add(new FocusIntentFieldError(
                    memberName,
                    validationResult.ErrorMessage ?? string.Empty));
            }
        }

        return new FocusIntentValidationResult(errors);
    }
}
