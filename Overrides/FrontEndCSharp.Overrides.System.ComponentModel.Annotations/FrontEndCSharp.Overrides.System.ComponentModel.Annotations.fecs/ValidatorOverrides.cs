using FrontEndCSharp.RuntimeOverrides;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace System.ComponentModel.AnnotationsRuntimeOverrides;

[RuntimeOverrideType(typeof(Validator))]
internal static class ValidatorOverrides
{
    [JavaScriptOverride]
    internal static bool TryValidateObject(
        object instance,
        ValidationContext validationContext,
        ICollection<ValidationResult>? validationResults) =>
        Validator.TryValidateObject(instance, validationContext, validationResults, false);

    [JavaScriptOverride]
    internal static bool TryValidateObject(
        object instance,
        ValidationContext validationContext,
        ICollection<ValidationResult>? validationResults,
        bool validateAllProperties)
    {
        _ = new ValidationResult(string.Empty, Array.Empty<string>());
        new List<ValidationResult>().Add(null!);
        throw new NotSupportedException();
    }

    [JavaScriptOverride]
    internal static bool TryValidateProperty(
        object? value,
        ValidationContext validationContext,
        ICollection<ValidationResult>? validationResults)
    {
        _ = Validator.TryValidateObject(
            new object(), validationContext, validationResults, false);
        throw new NotSupportedException();
    }

    [JavaScriptOverride]
    internal static bool TryValidateValue(
        object? value,
        ValidationContext validationContext,
        ICollection<ValidationResult>? validationResults,
        IEnumerable<ValidationAttribute> validationAttributes)
    {
        _ = Validator.TryValidateObject(
            new object(), validationContext, validationResults, false);
        throw new NotSupportedException();
    }

    [JavaScriptOverride]
    internal static void ValidateObject(
        object instance,
        ValidationContext validationContext)
    {
        _ = Validator.TryValidateObject(instance, validationContext, null, false);
        throw new ValidationException();
    }

    [JavaScriptOverride]
    internal static void ValidateObject(
        object instance,
        ValidationContext validationContext,
        bool validateAllProperties)
    {
        _ = Validator.TryValidateObject(
            instance, validationContext, null, validateAllProperties);
        throw new ValidationException();
    }

    [JavaScriptOverride]
    internal static void ValidateProperty(
        object? value,
        ValidationContext validationContext)
    {
        _ = Validator.TryValidateObject(
            new object(), validationContext, null, false);
        throw new ValidationException();
    }

    [JavaScriptOverride]
    internal static void ValidateValue(
        object? value,
        ValidationContext validationContext,
        IEnumerable<ValidationAttribute> validationAttributes)
    {
        _ = Validator.TryValidateObject(
            new object(), validationContext, null, false);
        throw new ValidationException();
    }
}
