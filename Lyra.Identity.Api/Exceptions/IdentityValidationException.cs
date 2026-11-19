namespace Lyra.Identity.Api.Exceptions;

using Microsoft.AspNetCore.Identity;

public class IdentityValidationException : Exception
{
    public IEnumerable<IdentityError> Errors { get; }

    public IdentityValidationException(IEnumerable<IdentityError> errors, string message)
        : base(message)
    {
        Errors = errors;
    }

    public IdentityValidationException(string code, string description)
        : base(description)
    {
        Errors = new[] { new IdentityError { Code = code, Description = description } };
    }
}
