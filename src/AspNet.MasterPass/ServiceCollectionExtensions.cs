using AspNet.MasterPass.Internal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AspNet.MasterPass;

/// <summary>
/// Registration entry points for the master password feature.
/// </summary>
public static class ServiceCollectionExtensions
{
    private sealed class MasterPassRegistrationMarker;

    /// <summary>
    /// Enables the master password for ASP.NET Core Identity. Call it after
    /// <c>AddIdentity</c>, <c>AddDefaultIdentity</c> or <c>AddIdentityCore</c>.
    /// </summary>
    /// <typeparam name="TUser">The application user type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration of <see cref="MasterPassOptions"/>.</param>
    public static IServiceCollection AddMasterPass<TUser>(
        this IServiceCollection services,
        Action<MasterPassOptions>? configure = null)
        where TUser : class
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<MasterPassOptions>();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        if (services.Any(descriptor => descriptor.ServiceType == typeof(MasterPassRegistrationMarker)))
        {
            return services;
        }

        services.AddSingleton<MasterPassRegistrationMarker>();
        services.TryAddSingleton<IValidateOptions<MasterPassOptions>, MasterPassOptionsValidator>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, MasterPassStartupValidator>());
        services.AddHttpContextAccessor();
        services.TryAddScoped<MasterPassContext>();
        services.TryAddSingleton<IMasterPassSecretProvider, ConfigurationMasterPassSecretProvider>();
        services.TryAddSingleton<IMasterPassAuditSink, LoggingMasterPassAuditSink>();
        services.TryAddSingleton<IAuthorizationHandler, RequireRealPasswordHandler>();

        services.AddAuthorization(authorization =>
        {
            if (authorization.GetPolicy(MasterPassDefaults.RequireRealPasswordPolicy) is null)
            {
                authorization.AddPolicy(MasterPassDefaults.RequireRealPasswordPolicy, policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(RequireRealPasswordRequirement.Instance));
            }
        });

        DecoratePasswordHasher<TUser>(services);
        DecorateClaimsPrincipalFactory<TUser>(services);

        return services;
    }

    /// <summary>
    /// Enables the master password and binds <see cref="MasterPassOptions"/> from the
    /// <c>MasterPass</c> configuration section (for example
    /// <c>MasterPass:Password</c> or <c>MasterPass__Password</c>).
    /// </summary>
    /// <typeparam name="TUser">The application user type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="configure">Optional additional configuration of <see cref="MasterPassOptions"/>.</param>
    public static IServiceCollection AddMasterPass<TUser>(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<MasterPassOptions>? configure = null)
        where TUser : class
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return services.AddMasterPass<TUser>(options =>
        {
            configuration.GetSection(MasterPassOptions.SectionName).Bind(options);
            configure?.Invoke(options);
        });
    }

    private static void DecoratePasswordHasher<TUser>(IServiceCollection services)
        where TUser : class
    {
        var descriptor = FindLastDescriptor(services, typeof(IPasswordHasher<TUser>))
            ?? throw new InvalidOperationException(
                $"No IPasswordHasher<{typeof(TUser).Name}> is registered. Call AddMasterPass<{typeof(TUser).Name}>() " +
                "after AddIdentity, AddDefaultIdentity or AddIdentityCore.");

        services.Remove(descriptor);
        services.AddSingleton<IPasswordHasher<TUser>>(provider =>
        {
            var inner = (IPasswordHasher<TUser>)CreateFromDescriptor(provider, descriptor);

            return new MasterPassPasswordHasher<TUser>(
                inner,
                provider.GetRequiredService<IOptionsMonitor<MasterPassOptions>>(),
                provider.GetRequiredService<IHttpContextAccessor>(),
                provider.GetRequiredService<IServiceScopeFactory>(),
                provider,
                provider.GetRequiredService<IHostEnvironment>());
        });
    }

    private static void DecorateClaimsPrincipalFactory<TUser>(IServiceCollection services)
        where TUser : class
    {
        var descriptor = FindLastDescriptor(services, typeof(IUserClaimsPrincipalFactory<TUser>))
            ?? throw new InvalidOperationException(
                $"No IUserClaimsPrincipalFactory<{typeof(TUser).Name}> is registered. Call AddMasterPass<{typeof(TUser).Name}>() " +
                "after AddIdentity, AddDefaultIdentity or AddIdentityCore.");

        services.Remove(descriptor);
        services.AddScoped<IUserClaimsPrincipalFactory<TUser>>(provider =>
            new MasterPassClaimsPrincipalFactory<TUser>(
                (IUserClaimsPrincipalFactory<TUser>)CreateFromDescriptor(provider, descriptor),
                provider.GetRequiredService<MasterPassContext>()));
    }

    private static ServiceDescriptor? FindLastDescriptor(IServiceCollection services, Type serviceType)
    {
        for (var index = services.Count - 1; index >= 0; index--)
        {
            if (services[index].ServiceType == serviceType)
            {
                return services[index];
            }
        }

        return null;
    }

    private static object CreateFromDescriptor(IServiceProvider services, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is not null)
        {
            return descriptor.ImplementationInstance;
        }

        if (descriptor.ImplementationFactory is not null)
        {
            return descriptor.ImplementationFactory(services);
        }

        return ActivatorUtilities.CreateInstance(services, descriptor.ImplementationType!);
    }
}