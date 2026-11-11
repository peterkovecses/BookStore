namespace BookStore.Identity.Common;

internal sealed class PasswordResetTokenProvider(
    IDataProtectionProvider protection,
    IOptions<PasswordResetTokenProviderOptions> options,
    ILogger<DataProtectorTokenProvider<ApplicationUser>> logger)
    : DataProtectorTokenProvider<ApplicationUser>(protection, options, logger);
