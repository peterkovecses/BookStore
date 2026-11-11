namespace BookStore.Identity.IntegrationTests.PasswordRecovery;

public sealed class PasswordRecoveryTests(IdentityFactory factory) : IClassFixture<IdentityFactory>
{
    private const string OldPassword = "Password1!";
    private const string NewPassword = "Different2!";
    private const string ResetPath = "/api/identity/password/reset";
    private const string ForgotPath = "/api/identity/password/forgot";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Reset_WhenTokenIsValid_ShouldConfirmAccountAndRevokeAllSessions(bool confirmed)
    {
        // Arrange
        var (userId, email, token) = await CreateUserAsync(confirmed);
        await AddSessionAsync(userId);
        await AddSessionAsync(userId);
        using var sut = factory.CreateClient();

        // Act
        using var actual = await sut.PostAsJsonAsync(ResetPath, new { UserId = userId, Token = token, NewPassword },
            TestContext.Current.CancellationToken);
        using var repeated = await sut.PostAsJsonAsync(ResetPath, new { UserId = userId, Token = token, NewPassword },
            TestContext.Current.CancellationToken);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByEmailAsync(email))!;

        // Assert
        actual.StatusCode.Should().Be(HttpStatusCode.NoContent);
        repeated.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        user.EmailConfirmed.Should().BeTrue();
        (await users.CheckPasswordAsync(user, NewPassword)).Should().BeTrue();
        (await users.CheckPasswordAsync(user, OldPassword)).Should().BeFalse();
        (await db.RefreshTokens.Where(item => item.UserId == userId).AllAsync(item => item.Revoked,
            TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    [Theory]
    [InlineData("invalid", "Different2!")]
    [InlineData("tampered", "Different2!")]
    [InlineData("valid", "weak")]
    public async Task Reset_WhenTokenOrPasswordIsInvalid_ShouldPreservePasswordAndSessions(string tokenKind, string password)
    {
        // Arrange
        var (userId, email, token) = await CreateUserAsync(false);
        await AddSessionAsync(userId);
        using var sut = factory.CreateClient();

        // Act
        using var actual = await sut.PostAsJsonAsync(ResetPath, new
        {
            UserId = userId,
            Token = tokenKind switch
            {
                "valid" => token,
                "tampered" => (token[0] == 'A' ? "B" : "A") + token[1..],
                _ => "invalid"
            },
            NewPassword = password
        }, TestContext.Current.CancellationToken);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByEmailAsync(email))!;

        // Assert
        actual.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        user.EmailConfirmed.Should().BeFalse();
        (await users.CheckPasswordAsync(user, OldPassword)).Should().BeTrue();
        (await db.RefreshTokens.SingleAsync(item => item.UserId == userId, TestContext.Current.CancellationToken)).Revoked.Should().BeFalse();
    }

    [Fact]
    public async Task Reset_WhenRequestsRace_ShouldAllowOnlyOneSuccess()
    {
        // Arrange
        var (userId, _, token) = await CreateUserAsync(true);
        await AddSessionAsync(userId);
        using var sut = factory.CreateClient();
        await using var otherHost = factory.WithWebHostBuilder(_ => { });
        using var other = otherHost.CreateClient();
        var request = new { UserId = userId, Token = token, NewPassword };

        // Act
        var responses = await Task.WhenAll(sut.PostAsJsonAsync(ResetPath, request, TestContext.Current.CancellationToken),
            other.PostAsJsonAsync(ResetPath, request, TestContext.Current.CancellationToken));

        // Assert
        responses.Select(response => response.StatusCode).Should().BeEquivalentTo([HttpStatusCode.NoContent, HttpStatusCode.BadRequest]);
        foreach (var response in responses) response.Dispose();
    }

    [Fact]
    public async Task Reset_WhenRevocationFails_ShouldRollBackPasswordAndConfirmation()
    {
        // Arrange
        var (userId, email, token) = await CreateUserAsync(false);
        await AddSessionAsync(userId);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER [identity].[FailPasswordRecoveryTest] ON [identity].[RefreshTokens] AFTER UPDATE AS THROW 51000, 'Simulated failure', 1;",
            TestContext.Current.CancellationToken);
        try
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var sut = new ResetPasswordHandler(db, users);
            var command = new ResetPasswordCommand(new ResetPasswordRequest { UserId = userId, Token = token, NewPassword = NewPassword });

            // Act
            var act = () => sut.HandleAsync(command, TestContext.Current.CancellationToken);

            // Assert
            await act.Should().ThrowAsync<SqlException>();
            using var verifyScope = factory.Services.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var verifyUsers = verifyScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await verifyUsers.FindByEmailAsync(email))!;
            user.EmailConfirmed.Should().BeFalse();
            (await verifyUsers.CheckPasswordAsync(user, OldPassword)).Should().BeTrue();
            (await verifyDb.RefreshTokens.SingleAsync(item => item.UserId == userId, TestContext.Current.CancellationToken)).Revoked.Should().BeFalse();
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER [identity].[FailPasswordRecoveryTest]", CancellationToken.None);
        }
    }

    [Fact]
    public async Task Forgot_WhenAccountExistsOrIsUnknown_ShouldPersistIndistinguishableProtectedRequests()
    {
        // Arrange
        var (_, email, _) = await CreateUserAsync(true);
        var unknown = $"{Guid.NewGuid():N}@example.com";
        using var sut = factory.CreateClient();

        // Act
        using var existing = await sut.PostAsJsonAsync(ForgotPath, new { Email = email }, TestContext.Current.CancellationToken);
        using var missing = await sut.PostAsJsonAsync(ForgotPath, new { Email = unknown }, TestContext.Current.CancellationToken);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var messages = await db.OutboxMessages.Where(item => item.EventType == PasswordRecoveryRequested.EventType)
            .OrderByDescending(item => item.CreatedAt).Take(2).ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        existing.StatusCode.Should().Be(HttpStatusCode.NoContent);
        missing.StatusCode.Should().Be(existing.StatusCode);
        messages.Should().HaveCount(2);
        messages.Should().OnlyContain(item => !item.Payload.Contains(email) && !item.Payload.Contains(unknown));
    }

    [Fact]
    public async Task Forgot_WhenRateLimitIsExceeded_ShouldRejectFurtherRequests()
    {
        // Arrange
        await using var host = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Identity:PasswordRecovery:RateLimiting:Forgot:PermitLimit", "1"));
        using var sut = host.CreateClient();
        var request = new { Email = "unknown@example.com" };

        // Act
        using var first = await sut.PostAsJsonAsync(ForgotPath, request, TestContext.Current.CancellationToken);
        using var second = await sut.PostAsJsonAsync(ForgotPath, request, TestContext.Current.CancellationToken);

        // Assert
        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Reset_WhenRateLimitIsExceeded_ShouldRejectFurtherRequests()
    {
        // Arrange
        await using var host = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Identity:PasswordRecovery:RateLimiting:Reset:PermitLimit", "1"));
        using var sut = host.CreateClient();
        var request = new { UserId = "unknown", Token = "invalid", NewPassword };

        // Act
        using var first = await sut.PostAsJsonAsync(ResetPath, request, TestContext.Current.CancellationToken);
        using var second = await sut.PostAsJsonAsync(ResetPath, request, TestContext.Current.CancellationToken);

        // Assert
        first.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        second.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Reset_WhenOldSessionsExist_ShouldRequireNewLogin()
    {
        // Arrange
        var (userId, email, token) = await CreateUserAsync(true);
        using var sut = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), HandleCookies = false
        });
        using var login = await SendAuthenticationAsync(sut, "/api/identity/login", new { Email = email, Password = OldPassword });
        var cookie = login.Headers.GetValues("Set-Cookie").Single().Split(';')[0];

        // Act
        using var reset = await sut.PostAsJsonAsync(ResetPath, new { UserId = userId, Token = token, NewPassword },
            TestContext.Current.CancellationToken);
        using var refresh = await SendAuthenticationAsync(sut, "/api/identity/refresh", cookie: cookie);
        using var newLogin = await SendAuthenticationAsync(sut, "/api/identity/login", new { Email = email, Password = NewPassword });

        // Assert
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        newLogin.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reset_WhenRefreshRaces_ShouldRevokeAnyIssuedSuccessor()
    {
        // Arrange
        var (userId, email, token) = await CreateUserAsync(true);
        using var sut = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), HandleCookies = false
        });
        using var login = await SendAuthenticationAsync(sut, "/api/identity/login", new { Email = email, Password = OldPassword });
        var cookie = login.Headers.GetValues("Set-Cookie").Single().Split(';')[0];

        // Act
        var responses = await Task.WhenAll(
            sut.PostAsJsonAsync(ResetPath, new { UserId = userId, Token = token, NewPassword }, TestContext.Current.CancellationToken),
            SendAuthenticationAsync(sut, "/api/identity/refresh", cookie: cookie));
        using var reset = responses[0];
        using var refresh = responses[1];
        var refreshCookie = refresh.StatusCode == HttpStatusCode.OK
            ? refresh.Headers.GetValues("Set-Cookie").Single().Split(';')[0]
            : cookie;
        using var successor = await SendAuthenticationAsync(sut, "/api/identity/refresh", cookie: refreshCookie);

        // Assert
        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);
        successor.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        refresh.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Reset_WhenOldPasswordLoginRaces_ShouldNotLeaveAnActiveSession()
    {
        // Arrange
        var (userId, email, token) = await CreateUserAsync(true);
        using var sut = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), HandleCookies = false
        });

        // Act
        var responses = await Task.WhenAll(
            sut.PostAsJsonAsync(ResetPath, new { UserId = userId, Token = token, NewPassword }, TestContext.Current.CancellationToken),
            SendAuthenticationAsync(sut, "/api/identity/login", new { Email = email, Password = OldPassword }));
        using var reset = responses[0];
        using var login = responses[1];
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var activeSessions = await db.RefreshTokens.CountAsync(item => item.UserId == userId && !item.Revoked,
            TestContext.Current.CancellationToken);

        // Assert
        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);
        login.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Unauthorized);
        activeSessions.Should().Be(0);
    }

    private static async Task<HttpResponseMessage> SendAuthenticationAsync(HttpClient client, string path, object? body = null, string? cookie = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("Origin", "https://ui.example.test");
        request.Headers.Add("X-BookStore-Request", "1");
        if (body is not null) request.Content = JsonContent.Create(body);
        if (cookie is not null) request.Headers.Add("Cookie", cookie);

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<(string UserId, string Email, string Token)> CreateUserAsync(bool confirmed)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"{Guid.NewGuid():N}@example.com";
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(), Email = email, UserName = email,
            FirstName = "Jane", LastName = "Doe", EmailConfirmed = confirmed
        };
        (await users.CreateAsync(user, OldPassword)).Succeeded.Should().BeTrue();
        var token = await users.GeneratePasswordResetTokenAsync(user);

        return (user.Id, email, WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token)));
    }

    private async Task AddSessionAsync(string userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        db.RefreshTokens.Add(new RefreshToken
        {
            Hash = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), FamilyId = Guid.NewGuid(),
            UserId = userId, ExpiresAt = DateTimeOffset.UtcNow.AddDays(1)
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
