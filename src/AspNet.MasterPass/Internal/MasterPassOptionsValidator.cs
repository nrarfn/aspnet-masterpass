using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AspNet.MasterPass.Internal;

/// <summary>
/// Fails startup when a master password is configured while running in Production without an
/// explicit opt-in. Shipping a working backdoor by accident is the failure mode this prevents.
/// </summary>
internal sealed class MasterPassOptionsValidator : IValidateOptions<MasterPassOptions>
{
    private readonly IHostEnvironment _environment;

    public MasterPassOptionsValidator(IHostEnvironment environment) => _environment = environment;

    public ValidateOptionsResult Validate(string? name, MasterPassOptions options)
    {
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.Password))
        {
            return ValidateOptionsResult.Success;
        }

        if (options.IsEnvironmentAllowed(_environment.EnvironmentName))
        {
            return ValidateOptionsResult.Success;
        }

        return ValidateOptionsResult.Fail(
            $"A master password is configured while the environment is '{_environment.EnvironmentName}'. " +
            "Remove the secret, or set MasterPass:AllowInProduction to true if this is intentional.");
    }
}