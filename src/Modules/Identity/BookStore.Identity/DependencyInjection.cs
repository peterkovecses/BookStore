namespace BookStore.Identity;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddIdentityModuleServices(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("BookStoreDb")
            ?? throw new InvalidOperationException("Connection string 'BookStoreDb' not found.");

        builder.Services.AddDbContext<IdentityDbContext>(options => options.UseSqlServer(connectionString,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "identity")));
        builder.EnrichSqlServerDbContext<IdentityDbContext>();
        builder.Services.AddScoped<IIdentityDbContext>(services => services.GetRequiredService<IdentityDbContext>());
        builder.Services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.User.AllowedUserNameCharacters = string.Empty;
            options.SignIn.RequireConfirmedEmail = true;
        }).AddEntityFrameworkStores<IdentityDbContext>().AddDefaultTokenProviders()
            .AddTokenProvider<PasswordResetTokenProvider>("BookStorePasswordReset");

        builder.Services.AddOptions<VerificationOptions>()
            .Bind(builder.Configuration.GetSection("Identity:Verification"))
            .Validate(options => options.IsValid(), "Identity verification configuration is invalid.")
            .ValidateOnStart();
        builder.Services.AddOptions<DataProtectionTokenProviderOptions>()
            .Configure<IOptions<VerificationOptions>>((options, verification) =>
                options.TokenLifespan = verification.Value.TokenLifetime);
        builder.Services.AddOptions<PasswordRecoveryOptions>()
            .Bind(builder.Configuration.GetSection("Identity:PasswordRecovery"))
            .Validate(options => options.IsValid(), "Identity password recovery configuration is invalid.")
            .ValidateOnStart();
        builder.Services.AddOptions<PasswordResetTokenProviderOptions>()
            .Configure<IOptions<PasswordRecoveryOptions>>((options, recovery) =>
                options.TokenLifespan = recovery.Value.TokenLifetime);
        builder.Services.Configure<IdentityOptions>(options =>
            options.Tokens.PasswordResetTokenProvider = "BookStorePasswordReset");

        builder.Services.AddOptions<IdentityOptions>()
            .Configure(options => builder.Configuration.GetSection("Identity:Password").Bind(options.Password))
            .Validate(options => options.Password.RequiredLength >= 6,
                "Identity:Password:RequiredLength must be at least 6.")
            .Validate(options => options.Password.RequiredUniqueChars >= 1,
                "Identity:Password:RequiredUniqueChars must be at least 1.")
            .ValidateOnStart();

        builder.Services.AddOptions<AuthenticationOptions>()
            .Bind(builder.Configuration.GetSection("Identity:Authentication"))
            .Validate(options => options.IsValid(), "Identity authentication configuration is invalid.")
            .ValidateOnStart();
        builder.Services.AddOptions<IdentityOptions>()
            .Configure<IOptions<AuthenticationOptions>>((identity, authentication) =>
            {
                identity.Lockout.AllowedForNewUsers = true;
                identity.Lockout.MaxFailedAccessAttempts = authentication.Value.LockoutAttempts;
                identity.Lockout.DefaultLockoutTimeSpan = authentication.Value.LockoutDuration;
            });
        var authentication = builder.Configuration.GetSection("Identity:Authentication").Get<AuthenticationOptions>() ?? new();
        builder.Services.AddCors(options => options.AddPolicy(IdentityAuthenticationPolicies.Cors, policy =>
            policy.WithOrigins(authentication.AllowedOrigins).WithMethods("POST").WithHeaders("Content-Type", "X-BookStore-Request")
                .AllowCredentials()));
        builder.Services.AddRateLimiter(options =>
            options.AddPolicy(IdentityAuthenticationPolicies.LoginRateLimit, context =>
                RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = authentication.LoginPermitLimit,
                        Window = TimeSpan.FromSeconds(authentication.LoginWindowSeconds),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    })));
        builder.Services.AddScoped<IdentitySessionService>();
        builder.Services.AddScoped<OriginAndCsrfEndpointFilter>();
        builder.Services.AddScoped<LoginHandler>();
        builder.Services.AddScoped<RefreshHandler>();
        builder.Services.AddScoped<LogoutHandler>();
        builder.Services.AddVerificationRateLimiting(builder.Configuration);
        builder.Services.AddPasswordRecoveryRateLimiting(builder.Configuration);
        builder.Services.AddSingleton<IUserIdGenerator, UserIdGenerator>();
        builder.Services.AddScoped<IVerificationRequestFactory, VerificationRequestFactory>();
        builder.Services.AddSingleton<IOutboxExpiry<VerificationEmailRequested>, VerificationRequestExpiry>();
        builder.Services.AddSingleton<IOutboxExpiry<PasswordRecoveryRequested>, PasswordRecoveryRequestedExpiry>();
        builder.Services.AddSingleton<IOutboxExpiry<PasswordResetEmailRequested>, PasswordResetRequestExpiry>();
        builder.Services.AddSingleton(new OutboxRegistry<IdentityOutboxStore>()
            .Register<VerificationResendRequested>(VerificationResendRequested.EventType)
            .Register<VerificationEmailRequested>(VerificationRequestFactory.EventType)
            .Register<PasswordRecoveryRequested>(PasswordRecoveryRequested.EventType)
            .Register<PasswordResetEmailRequested>(PasswordRecoveryRequestedHandler.EmailEventType));
        builder.Services.AddOutbox<IdentityOutboxStore>(builder.Configuration);
        builder.Services.AddScoped<IIdentityOutboxOperations>(services => services.GetRequiredService<IdentityOutboxStore>());
        builder.Services.AddRequests(Assembly.GetExecutingAssembly());
        builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly(), includeInternalTypes: true);

        return builder;
    }

    public static async Task InitialiseIdentityModuleAsync(this IHost host)
    {
        if (!host.Services.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            return;
        }

        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
    }

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/identity").WithTags("Identity");
        RegistrationEndpoint.MapEndpoint(group);
        PasswordPolicyEndpoint.MapEndpoint(group);
        VerificationEndpoint.MapVerificationEndpoints(group);
        ForgotPasswordEndpoint.MapEndpoint(group);
        ResetPasswordEndpoint.MapEndpoint(group);
        AuthenticationEndpoints.MapEndpoints(group);

        return app;
    }
}
