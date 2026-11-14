using System.ComponentModel.DataAnnotations;

namespace Compiler.Tests;

public sealed class ValidationIntegrationTests : IntegrationTestBase
{
    [Test]
    public void DataAnnotationsValidatesRequiredProperty()
    {
        const string source = """
            using System.ComponentModel.DataAnnotations;

            // Exercise the browser validation companion selected by the test project.
            var model = new Contact { Name = null };
            var results = new List<ValidationResult>();
            var valid = Validator.TryValidateObject(
                model,
                new ValidationContext(model),
                results,
                validateAllProperties: true);

            Console.WriteLine(valid ? "valid" : "invalid");
            Console.WriteLine(results.Count);
            Console.WriteLine(results[0].ErrorMessage);
            foreach (var memberName in results[0].MemberNames)
            {
                Console.WriteLine(memberName);
            }

            sealed class Contact
            {
                [Required]
                public string? Name { get; set; }
            }
            """;

        var output = CompileAndRun(source);

        Assert.Equal("invalid\n1\nThe Name field is required.\nName", output.Replace("\r\n", "\n"));
    }

    [Test]
    public void DataAnnotationsValidatesPracticalBuiltInAttributesAndOptions()
    {
        const string source = """
            using System.ComponentModel.DataAnnotations;

            var model = new Contact
            {
                Name = "x",
                Age = 17,
                Email = "not-an-email",
                Confirmation = "different"
            };
            var results = new List<ValidationResult>();
            Console.WriteLine(Validator.TryValidateObject(
                model, new ValidationContext(model), results, false));
            Console.WriteLine(results.Count);
            Console.WriteLine(Validator.TryValidateObject(
                model, new ValidationContext(model), results, true));
            foreach (var result in results)
            {
                Console.WriteLine(result.ErrorMessage);
                foreach (var memberName in result.MemberNames)
                {
                    Console.WriteLine(memberName);
                }
            }

            sealed class Contact
            {
                [StringLength(8, MinimumLength = 2)]
                public string? Name { get; set; }

                [Range(18, 120)]
                public int Age { get; set; }

                [EmailAddress]
                public string? Email { get; set; }

                public string? Password { get; set; } = "secret";

                [Compare(nameof(Password))]
                public string? Confirmation { get; set; }
            }
            """;

        var output = CompileAndRun(source).Replace("\r\n", "\n");

        Assert.Equal(
            "True\n0\nFalse\n"
            + "The field Name must be a string with a minimum length of '2' and a maximum length of '8'.\nName\n"
            + "The field Age must be between 18 and 120.\nAge\n"
            + "The Email field is not a valid e-mail address.\nEmail\n"
            + "'Confirmation' and 'Password' do not match.\nConfirmation",
            output);
    }

    [Test]
    public void DataAnnotationsExecutesCustomValidationAttributeAsManagedCode()
    {
        const string source = """
            using System.ComponentModel.DataAnnotations;

            var model = new Contact { Code = "wrong" };
            var results = new List<ValidationResult>();
            Console.WriteLine(Validator.TryValidateObject(
                model, new ValidationContext(model), results, true));
            Console.WriteLine(results[0].ErrorMessage);
            foreach (var memberName in results[0].MemberNames)
            {
                Console.WriteLine(memberName);
            }

            sealed class Contact
            {
                [ExactValue("expected")]
                public string? Code { get; set; }
            }

            sealed class ExactValueAttribute : ValidationAttribute
            {
                private readonly string _expected;

                public ExactValueAttribute(string expected) => _expected = expected;

                protected override ValidationResult? IsValid(
                    object? value,
                    ValidationContext validationContext) =>
                    value is string text && text == _expected
                        ? ValidationResult.Success
                        : new ValidationResult(
                            $"{validationContext.MemberName} must equal {_expected}.",
                            new[] { validationContext.MemberName! });
            }
            """;

        var output = CompileAndRun(source).Replace("\r\n", "\n");

        Assert.Equal("False\nCode must equal expected.\nCode", output);
    }

    [Test]
    public void DataAnnotationsSupportsPropertyValueAndThrowingApis()
    {
        const string source = """
            using System.ComponentModel.DataAnnotations;

            var model = new Contact();
            var propertyContext = new ValidationContext(model) { MemberName = nameof(Contact.Name) };
            var propertyResults = new List<ValidationResult>();
            Console.WriteLine(Validator.TryValidateProperty(null, propertyContext, propertyResults));
            Console.WriteLine(propertyResults[0].ErrorMessage);

            var valueResults = new List<ValidationResult>();
            Console.WriteLine(Validator.TryValidateValue(
                null,
                new ValidationContext(model),
                valueResults,
                new ValidationAttribute[] { new RequiredAttribute() }));
            Console.WriteLine(valueResults[0].ErrorMessage);

            try
            {
                Validator.ValidateProperty(null, propertyContext);
            }
            catch (ValidationException)
            {
                Console.WriteLine("threw");
            }

            sealed class Contact
            {
                [Required(ErrorMessage = "Name is mandatory.")]
                public string? Name { get; set; }
            }
            """;

        var output = CompileAndRun(source).Replace("\r\n", "\n");

        Assert.Equal(
            "False\nName is mandatory.\nFalse\nThe value field is required.\nthrew",
            output);
    }

    [Test]
    public void DataAnnotationsSupportsInheritanceAndIValidatableObject()
    {
        const string source = """
            using System.ComponentModel.DataAnnotations;

            var model = new Contact { Name = "ok", Accepted = false };
            var results = new List<ValidationResult>();
            Console.WriteLine(Validator.TryValidateObject(
                model, new ValidationContext(model), results, true));
            foreach (var result in results)
            {
                Console.WriteLine(result.ErrorMessage);
                foreach (var memberName in result.MemberNames)
                {
                    Console.WriteLine(memberName);
                }
            }

            class BaseContact
            {
                [Required]
                public string? Name { get; set; }
            }

            sealed class Contact : BaseContact, IValidatableObject
            {
                public bool Accepted { get; set; }

                public IEnumerable<ValidationResult> Validate(ValidationContext context) =>
                    Accepted
                        ? Array.Empty<ValidationResult>()
                        : new[] { new ValidationResult("Acceptance is required.", new[] { nameof(Accepted) }) };
            }
            """;

        var output = CompileAndRun(source).Replace("\r\n", "\n");

        Assert.Equal("False\nAcceptance is required.\nAccepted", output);
    }

    [Test]
    public void DataAnnotationsRejectsNullMismatchedContextsAndMalformedAttributes()
    {
        const string source = """
            using System.ComponentModel.DataAnnotations;

            var model = new Contact { Name = "value" };
            try
            {
                Validator.TryValidateObject(null!, new ValidationContext(model), null, true);
            }
            catch (ArgumentNullException)
            {
                Console.WriteLine("null");
            }

            try
            {
                Validator.TryValidateObject(model, new ValidationContext(new Contact()), null, true);
            }
            catch (ArgumentException)
            {
                Console.WriteLine("context");
            }

            try
            {
                Validator.TryValidateObject(model, new ValidationContext(model), null, true);
            }
            catch (InvalidOperationException)
            {
                Console.WriteLine("malformed");
            }

            sealed class Contact
            {
                [StringLength(-1)]
                public string? Name { get; set; }
            }
            """;

        var output = CompileAndRun(source).Replace("\r\n", "\n");

        Assert.Equal("null\ncontext\nmalformed", output);
    }
}
