namespace BookStore.Identity.Features.Registration;

internal static class PasswordPolicyEndpoint
{
    internal static void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("password-policy", (IOptions<IdentityOptions> options, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";

            return Results.Ok(PasswordPolicyFactory.Create(options.Value.Password));
        }).WithName("GetPasswordPolicy");
}

internal static class PasswordPolicyFactory
{
    internal static PasswordPolicy Create(PasswordOptions password) => new()
    {
        SchemaVersion = 1,
        RequiredLength = password.RequiredLength,
        RequiredUniqueChars = password.RequiredUniqueChars,
        RequireDigit = password.RequireDigit,
        RequireLowercase = password.RequireLowercase,
        RequireUppercase = password.RequireUppercase,
        RequireNonAlphanumeric = password.RequireNonAlphanumeric
    };
}
