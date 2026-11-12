namespace BookStore.Identity.EndToEndTests.PasswordRecovery;

[Collection("Identity E2E")]
public sealed class PasswordRecoveryBrowserFlowTests(IdentityOutboxTestApplicationFactory factory)
    : IClassFixture<IdentityOutboxTestApplicationFactory>
{
    private const string OldPassword = "Password1!";
    private const string NewPassword = "Different2!";

    [Fact]
    public async Task PasswordRecovery_WhenUserSubmitsForgotAndResetForms_ShouldChangePasswordFromDeliveredLink()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var userEmail = $"{Guid.NewGuid():N}@example.com";
        await using var uiHost = await StartUiHostAsync(cancellationToken);
        var uiAddress = GetAddress(uiHost);
        var uiOrigin = uiAddress.GetLeftPart(UriPartial.Authority);
        await using var apiHost = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("AllowedOrigins", uiOrigin);
            builder.UseSetting("Identity:Authentication:AllowedOrigins:0", uiOrigin);
            builder.UseSetting("Identity:PasswordRecovery:LinkBaseUrl", new Uri(uiAddress, "/reset-password").ToString());
        });
        using var apiClient = apiHost.CreateClient();
        using var scope = apiHost.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            Email = userEmail,
            UserName = userEmail,
            FirstName = "Jane",
            LastName = "Doe",
            EmailConfirmed = true
        };
        (await users.CreateAsync(user, OldPassword)).Succeeded.Should().BeTrue();
        using var mailpit = new HttpClient { BaseAddress = factory.MailpitAddress };
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var context = await browser.NewContextAsync(new BrowserNewContextOptions { IgnoreHTTPSErrors = true });
        var page = await context.NewPageAsync();
        await page.RouteAsync("https://localhost:5001/**", async route =>
        {
            using var request = new HttpRequestMessage(new HttpMethod(route.Request.Method), route.Request.Url);
            if (route.Request.PostDataBuffer is { } body)
            {
                request.Content = new ByteArrayContent(body);
            }

            foreach (var header in route.Request.Headers)
            {
                if (header.Key.Equals("host", StringComparison.OrdinalIgnoreCase) ||
                    header.Key.Equals("content-length", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value))
                {
                    request.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            using var response = await apiClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var responseHeaders = response.Headers
                .Concat(response.Content.Headers)
                .Where(header => !header.Key.Equals("transfer-encoding", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(header => header.Key, header => string.Join(", ", header.Value));
            await route.FulfillAsync(new RouteFulfillOptions
            {
                Status = (int)response.StatusCode,
                Headers = responseHeaders,
                BodyBytes = responseBody
            });
        });
        OutboxWorker<IdentityOutboxStore>? worker = null;

        try
        {
            await page.GotoAsync(new Uri(uiAddress, "/forgot-password").ToString());
            await page.GetByRole(AriaRole.Heading, new() { Name = "Forgot password" })
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
            worker = ActivatorUtilities.CreateInstance<OutboxWorker<IdentityOutboxStore>>(apiHost.Services);
            await worker.StartAsync(cancellationToken);

            // Act
            await page.GetByLabel("Email").FillAsync(userEmail);
            await page.GetByRole(AriaRole.Button, new() { Name = "Send reset link" }).ClickAsync();
            await page.GetByRole(AriaRole.Status)
                .GetByText("If an account matches this address", new() { Exact = false })
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

            var outboxMessage = await WaitForOutboxMessageAsync(db, cancellationToken);
            await WaitForStatusAsync(db, outboxMessage.Id, OutboxStatus.Completed, cancellationToken);
            using var messages = await mailpit.GetFromJsonAsync<JsonDocument>("/api/v1/messages", cancellationToken);
            var messageId = messages!.RootElement.GetProperty("messages").EnumerateArray().Single().GetProperty("ID").GetString();
            using var message = await mailpit.GetFromJsonAsync<JsonDocument>($"/api/v1/message/{messageId}", cancellationToken);
            var emailText = message!.RootElement.GetProperty("Text").GetString()!;
            var resetLink = Regex.Match(emailText, @"https://[^\s]+").Value;
            await page.GotoAsync(resetLink);
            await page.GetByRole(AriaRole.Heading, new() { Name = "Reset password" })
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
            await page.WaitForFunctionAsync("() => window.location.search === ''");
            await page.GetByLabel("New password", new() { Exact = true }).FillAsync(NewPassword);
            await page.GetByLabel("Confirm new password", new() { Exact = true }).FillAsync(NewPassword);
            await page.GetByRole(AriaRole.Button, new() { Name = "Reset password" }).ClickAsync();
            await page.GetByRole(AriaRole.Status)
                .GetByText("Your password has been reset", new() { Exact = false })
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

            // Assert
            new Uri(resetLink).GetLeftPart(UriPartial.Path).Should().Be(new Uri(uiAddress, "/reset-password").ToString());
            new Uri(await page.EvaluateAsync<string>("() => window.location.href")).Query.Should().BeEmpty();
            messages.RootElement.GetProperty("total").GetInt32().Should().Be(1);
            message.RootElement.GetProperty("HTML").GetString().Should().Contain("reset-password");
            await using var verificationScope = apiHost.Services.CreateAsyncScope();
            var verifiedUsers = verificationScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var verifiedUser = (await verifiedUsers.FindByEmailAsync(userEmail))!;
            (await verifiedUsers.CheckPasswordAsync(verifiedUser, NewPassword)).Should().BeTrue();
            (await verifiedUsers.CheckPasswordAsync(verifiedUser, OldPassword)).Should().BeFalse();
        }
        finally
        {
            if (worker is not null)
            {
                await worker.StopAsync(CancellationToken.None);
                worker.Dispose();
            }

            await context.CloseAsync();
        }
    }

    private static async Task<WebApplication> StartUiHostAsync(CancellationToken cancellationToken)
    {
        var uiProjectDirectory = GetUiProjectDirectory();
        using var rsa = RSA.Create(2048);
        var certificateRequest = new CertificateRequest("CN=BookStore UI integration test", rsa, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        var subjectAlternativeNames = new SubjectAlternativeNameBuilder();
        subjectAlternativeNames.AddIpAddress(IPAddress.Loopback);
        certificateRequest.CertificateExtensions.Add(subjectAlternativeNames.Build());
        var certificate = certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = "BookStore.UI",
            ContentRootPath = uiProjectDirectory,
            WebRootPath = Path.Combine(uiProjectDirectory, "wwwroot"),
            EnvironmentName = Environments.Development
        });
        builder.WebHost.UseStaticWebAssets();
        builder.WebHost.ConfigureKestrel(options =>
            options.Listen(IPAddress.Loopback, 0, listenOptions => listenOptions.UseHttps(certificate)));
        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.Response.Headers["Blazor-Environment"] = Environments.Development;
            await next();
        });
        app.MapStaticAssets();
        app.MapFallbackToFile("index.html");
        app.Lifetime.ApplicationStopped.Register(certificate.Dispose);
        await app.StartAsync(cancellationToken);

        return app;
    }

    private static Uri GetAddress(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();

        return new Uri(address);
    }

    private static string GetUiProjectDirectory()
    {
        var assemblyDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        var repositoryRoot = assemblyDirectory;
        while (repositoryRoot is not null && !File.Exists(Path.Combine(repositoryRoot.FullName, "BookStore.sln")))
        {
            repositoryRoot = repositoryRoot.Parent;
        }

        repositoryRoot.Should().NotBeNull();
        var uiProjectDirectory = Path.Combine(repositoryRoot!.FullName, "src", "Hosts", "BookStore.UI");
        Directory.Exists(Path.Combine(uiProjectDirectory, "wwwroot")).Should().BeTrue();
        File.Exists(Path.Combine(AppContext.BaseDirectory, "BookStore.UI.staticwebassets.runtime.json")).Should().BeTrue();

        return uiProjectDirectory;
    }

    private static async Task<OutboxMessage> WaitForOutboxMessageAsync(IdentityDbContext db, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        while (true)
        {
            var message = await db.OutboxMessages.AsNoTracking().SingleOrDefaultAsync(
                item => item.EventType == PasswordRecoveryRequestedHandler.EmailEventType, deadline.Token);
            if (message is not null)
            {
                return message;
            }

            await Task.Delay(100, deadline.Token);
        }
    }

    private static async Task WaitForStatusAsync(
        IdentityDbContext db, Guid id, OutboxStatus status, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        while (!await db.OutboxMessages.AsNoTracking().AnyAsync(item => item.Id == id && item.Status == status, deadline.Token))
        {
            await Task.Delay(100, deadline.Token);
        }
    }
}
