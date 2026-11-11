namespace BookStore.Identity.IntegrationTests.Authentication;

public sealed class AuthenticationEndpointTests(IdentityFactory factory) : IClassFixture<IdentityFactory>
{
    private const string Origin = "https://ui.example.test";
    private const string Password = "Password1!";
    private const string CookieName = "__Secure-bookstore-refresh";
    private const string LoginPath = "/api/identity/login";
    private const string RefreshPath = "/api/identity/refresh";
    private const string LogoutPath = "/api/identity/logout";

    [Fact]
    public async Task Login_WhenCredentialsAndConfirmationAreValid_ShouldIssueSignedTokenAndSecureCookie()
    {
        // Arrange
        var email = await CreateUserAsync(confirmed: true);
        using var sut = CreateClient();

        // Act
        using var actual = await SendAsync(sut, LoginPath, new { Email = email, Password });
        var body = await actual.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var cookie = actual.Headers.GetValues("Set-Cookie").Single();
        var token = CookieValue(actual);
        var access = JsonSerializer.Deserialize<AccessTokenResponse>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var claims = new JwtSecurityTokenHandler().ValidateToken(access.AccessToken, new TokenValidationParameters
        {
            ValidIssuer = "https://api.example.test",
            ValidAudience = "bookstore-test",
            IssuerSigningKey = new SymmetricSecurityKey(new byte[32]),
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        }, out _);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var stored = await context.RefreshTokens.SingleAsync(item => item.Hash == Hash(token), TestContext.Current.CancellationToken);

        // Assert
        actual.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("accessToken").And.NotContain(token).And.NotContain(Password);
        claims.Identity!.IsAuthenticated.Should().BeTrue();
        access.ExpiresAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddMinutes(10), TimeSpan.FromSeconds(15));
        cookie.Should().Contain("httponly").And.Contain("secure").And.Contain("samesite=strict")
            .And.Contain("path=/api/identity");
        stored.Hash.Should().NotBe(token);
        string.Join('\n', factory.Logs.Messages).Should().NotContain(token).And.NotContain(Password).And.NotContain(access.AccessToken);
    }

    [Fact]
    public async Task Login_WhenEmailIsUnconfirmedOrPasswordIsInvalid_ShouldReject()
    {
        // Arrange
        var email = await CreateUserAsync(confirmed: false);
        using var sut = CreateClient();

        // Act
        using var unconfirmed = await SendAsync(sut, LoginPath, new { Email = email, Password });
        using var invalid = await SendAsync(sut, LoginPath, new { Email = email, Password = "wrong" });

        // Assert
        unconfirmed.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await unconfirmed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Contain("not been confirmed");
        invalid.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        invalid.Headers.Contains("Set-Cookie").Should().BeFalse();
    }

    [Fact]
    public async Task Login_WhenEmailIsConfirmedThroughVerification_ShouldSucceed()
    {
        // Arrange
        var email = await CreateUserAsync(confirmed: false);
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByEmailAsync(email))!;
        var confirmation = await users.GenerateEmailConfirmationTokenAsync(user);
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(confirmation));
        using var sut = CreateClient();

        // Act
        using var before = await SendAsync(sut, LoginPath, new { Email = email, Password });
        using var confirmed = await sut.PostAsJsonAsync("/api/identity/verification/confirmation",
            new { UserId = user.Id, Token = encoded }, TestContext.Current.CancellationToken);
        using var after = await SendAsync(sut, LoginPath, new { Email = email, Password });

        // Assert
        before.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        confirmed.StatusCode.Should().Be(HttpStatusCode.NoContent);
        after.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_WhenAccountIsLocked_ShouldRejectCorrectPasswordUntilLockoutExpires()
    {
        // Arrange
        var email = await CreateUserAsync(confirmed: true);
        using var sut = CreateClient();
        await using var secondHost = factory.WithWebHostBuilder(_ => { });
        using var secondClient = secondHost.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false
        });
        var failures = await Task.WhenAll(Enumerable.Range(0, 5).Select(index =>
            SendAsync(index % 2 == 0 ? sut : secondClient, LoginPath, new { Email = email, Password = "wrong" })));
        foreach (var failure in failures) failure.Dispose();

        // Act
        using var locked = await SendAsync(sut, LoginPath, new { Email = email, Password });
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByEmailAsync(email))!;
        (await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(-1))).Succeeded.Should().BeTrue();
        using var recovered = await SendAsync(sut, LoginPath, new { Email = email, Password });

        // Assert
        locked.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        recovered.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_WhenTokenIsExpiredOrRevoked_ShouldReject()
    {
        // Arrange
        var email = await CreateUserAsync(confirmed: true);
        using var sut = CreateClient();
        using var login = await SendAsync(sut, LoginPath, new { Email = email, Password });
        var token = CookieValue(login);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var stored = await context.RefreshTokens.SingleAsync(item => item.Hash == Hash(token), TestContext.Current.CancellationToken);
        stored.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        using var expired = await SendAsync(sut, RefreshPath, cookie: token);
        stored.ExpiresAt = DateTimeOffset.UtcNow.AddDays(1);
        stored.Revoked = true;
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        using var revoked = await SendAsync(sut, RefreshPath, cookie: token);

        // Assert
        expired.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        revoked.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WhenInputIsInvalid_ShouldRejectWithoutEchoingPassword()
    {
        // Arrange
        const string invalidEmail = "not-an-email";
        const string secret = "secret-that-must-not-be-echoed";
        using var sut = CreateClient();

        // Act
        using var actual = await SendAsync(sut, LoginPath, new { Email = invalidEmail, Password = secret });
        var body = await actual.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        actual.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().NotContain(secret);
    }

    [Fact]
    public async Task Login_WhenNetworkLimitIsExceeded_ShouldThrottle()
    {
        // Arrange
        await using var configured = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Identity:Authentication:LoginPermitLimit", "1"));
        using var sut = configured.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false
        });

        // Act
        using var first = await SendAsync(sut, LoginPath, new { Email = "unknown@example.com", Password });
        using var second = await SendAsync(sut, LoginPath, new { Email = "unknown@example.com", Password });

        // Assert
        first.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        second.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Refresh_WhenRotatedTokenIsReplayed_ShouldRevokeOnlyItsFamily()
    {
        // Arrange
        var email = await CreateUserAsync(confirmed: true);
        using var sut = CreateClient();
        using var firstLogin = await SendAsync(sut, LoginPath, new { Email = email, Password });
        using var secondLogin = await SendAsync(sut, LoginPath, new { Email = email, Password });
        var first = CookieValue(firstLogin);
        var second = CookieValue(secondLogin);
        using var rotated = await SendAsync(sut, RefreshPath, cookie: first);
        var successor = CookieValue(rotated);

        // Act
        using var replay = await SendAsync(sut, RefreshPath, cookie: first);
        using var revoked = await SendAsync(sut, RefreshPath, cookie: successor);
        using var otherSession = await SendAsync(sut, RefreshPath, cookie: second);

        // Assert
        rotated.StatusCode.Should().Be(HttpStatusCode.OK);
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        revoked.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        otherSession.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_WhenRequestsRace_ShouldIssueOneSuccessAndRevokeItsSuccessor()
    {
        // Arrange
        var email = await CreateUserAsync(confirmed: true);
        using var sut = CreateClient();
        await using var secondHost = factory.WithWebHostBuilder(_ => { });
        using var secondClient = secondHost.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false
        });
        using var login = await SendAsync(sut, LoginPath, new { Email = email, Password });
        var token = CookieValue(login);

        // Act
        var responses = await Task.WhenAll(SendAsync(sut, RefreshPath, cookie: token), SendAsync(secondClient, RefreshPath, cookie: token));
        using var successful = responses.Single(response => response.StatusCode == HttpStatusCode.OK);
        using var rejected = responses.Single(response => response.StatusCode == HttpStatusCode.Unauthorized);
        using var successor = await SendAsync(sut, RefreshPath, cookie: CookieValue(successful));

        // Assert
        successor.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_WhenCurrentSessionEnds_ShouldClearCookieAndLeaveOtherSessionActive()
    {
        // Arrange
        var email = await CreateUserAsync(confirmed: true);
        using var sut = CreateClient();
        using var firstLogin = await SendAsync(sut, LoginPath, new { Email = email, Password });
        using var secondLogin = await SendAsync(sut, LoginPath, new { Email = email, Password });
        var first = CookieValue(firstLogin);
        var second = CookieValue(secondLogin);

        // Act
        using var logout = await SendAsync(sut, LogoutPath, cookie: first);
        using var revoked = await SendAsync(sut, RefreshPath, cookie: first);
        using var otherSession = await SendAsync(sut, RefreshPath, cookie: second);

        // Assert
        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
        logout.Headers.GetValues("Set-Cookie").Single().Should().Contain("path=/api/identity")
            .And.Contain("secure").And.Contain("httponly").And.Contain("samesite=strict").And.Contain("expires=");
        revoked.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        otherSession.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Logout_WhenSameSiteNoneIsConfigured_ShouldMatchIssuedCookieAttributes()
    {
        // Arrange
        var email = await CreateUserAsync(confirmed: true);
        await using var configured = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Identity:Authentication:CookieSameSite", "None"));
        using var sut = configured.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false
        });
        using var login = await SendAsync(sut, LoginPath, new { Email = email, Password });

        // Act
        using var logout = await SendAsync(sut, LogoutPath, cookie: CookieValue(login));

        // Assert
        login.Headers.GetValues("Set-Cookie").Single().Should().Contain("samesite=none").And.Contain("secure");
        logout.Headers.GetValues("Set-Cookie").Single().Should().Contain("samesite=none").And.Contain("secure")
            .And.Contain("path=/api/identity");
    }

    [Fact]
    public async Task Login_WhenOriginOrCsrfHeaderIsMissingOrInvalid_ShouldReject()
    {
        // Arrange
        using var sut = CreateClient();
        using var missing = new HttpRequestMessage(HttpMethod.Post, LoginPath) { Content = JsonContent.Create(new { Email = "x@example.com", Password }) };
        using var untrusted = new HttpRequestMessage(HttpMethod.Post, LoginPath) { Content = JsonContent.Create(new { Email = "x@example.com", Password }) };
        untrusted.Headers.Add("Origin", "https://attacker.example");
        untrusted.Headers.Add("X-BookStore-Request", "1");

        // Act
        using var first = await sut.SendAsync(missing, TestContext.Current.CancellationToken);
        using var second = await sut.SendAsync(untrusted, TestContext.Current.CancellationToken);

        // Assert
        first.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        second.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Login_WhenCorsPreflightIsAllowed_ShouldAllowCredentialsOnlyForConfiguredOrigin()
    {
        // Arrange
        using var sut = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, LoginPath);
        request.Headers.Add("Origin", Origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type,x-bookstore-request");

        // Act
        using var actual = await sut.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        actual.Headers.GetValues("Access-Control-Allow-Origin").Single().Should().Be(Origin);
        actual.Headers.GetValues("Access-Control-Allow-Credentials").Single().Should().Be("true");
    }

    [Theory]
    [InlineData(RefreshPath)]
    [InlineData(LogoutPath)]
    public async Task CookieEndpoint_WhenCsrfHeaderIsMissing_ShouldReject(string path)
    {
        // Arrange
        using var sut = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("Origin", Origin);

        // Act
        using var actual = await sut.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        actual.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Login_WhenCorsPreflightOriginIsUntrusted_ShouldNotAllowCredentials()
    {
        // Arrange
        using var sut = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, LoginPath);
        request.Headers.Add("Origin", "https://attacker.example");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type,x-bookstore-request");

        // Act
        using var actual = await sut.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        actual.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
        actual.Headers.Contains("Access-Control-Allow-Credentials").Should().BeFalse();
    }
    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = false
    });

    private async Task<string> CreateUserAsync(bool confirmed)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"{Guid.NewGuid():N}@example.com";
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            Email = email,
            UserName = email,
            FirstName = "Jane",
            LastName = "Doe",
            EmailConfirmed = confirmed
        };
        (await users.CreateAsync(user, Password)).Succeeded.Should().BeTrue();

        return email;
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string path, object? body = null, string? cookie = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("Origin", Origin);
        request.Headers.Add("X-BookStore-Request", "1");
        if (body is not null) request.Content = JsonContent.Create(body);
        if (cookie is not null) request.Headers.Add("Cookie", $"{CookieName}={cookie}");

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static string CookieValue(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").Single().Split(';')[0].Split('=')[1];

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
