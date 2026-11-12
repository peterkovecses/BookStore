namespace BookStore.UI.UnitTests.Features.Authentication.Registration;

public sealed class RegistrationFormTests : BunitTestContext
{
    [Theory]
    [InlineData("")]
    [InlineData("Different1!")]
    public void Submit_WhenConfirmationIsInvalid_ShouldShowFeedbackAndClearPasswords(string confirmation)
    {
        // Arrange
        var handler = new Handler(_ => PolicyResponse(6));
        var sut = RenderForm(handler);
        FillForm(sut, "Password1!", confirmation);

        // Act
        sut.Find("form").Submit();

        // Assert
        sut.Find("#confirm-password-errors").TextContent.Should().NotBeNullOrWhiteSpace();
        sut.Find("#password").GetAttribute("value").Should().BeEmpty();
        sut.Find("#confirmPassword").GetAttribute("value").Should().BeEmpty();
        handler.PostCount.Should().Be(0);
    }

    [Fact]
    public void Submit_WhenConfiguredPolicyRejectsPassword_ShouldNotSendRegistration()
    {
        // Arrange
        var handler = new Handler(_ => PolicyResponse(12));
        var sut = RenderForm(handler);
        FillForm(sut, "Short1!", "Short1!");

        // Act
        sut.Find("form").Submit();

        // Assert
        sut.Find("#password-errors").TextContent.Should().Contain("12");
        sut.Find("#password").GetAttribute("value").Should().BeEmpty();
        sut.Find("#confirmPassword").GetAttribute("value").Should().BeEmpty();
        handler.PostCount.Should().Be(0);
    }

    [Theory]
    [InlineData(HttpStatusCode.NoContent)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public void Submit_WhenPolicyIsUnavailable_ShouldUseServerAndClearPasswords(HttpStatusCode registrationStatus)
    {
        // Arrange
        var handler = new Handler(request => request.Method == HttpMethod.Get
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : RegistrationResponse(registrationStatus));
        var sut = RenderForm(handler);
        FillForm(sut, "weak", "weak");

        // Act
        sut.Find("form").Submit();

        // Assert
        handler.PostCount.Should().Be(1);
        handler.Body.Should().NotContain("confirmPassword");
        sut.Markup.Should().NotContain("Password policy could not");
        var model = (RegistrationModel)typeof(Register).GetField("_model", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(sut.Instance)!;
        model.Password.Should().BeEmpty();
        model.ConfirmPassword.Should().BeEmpty();
        if (registrationStatus != HttpStatusCode.NoContent)
        {
            sut.Find("#password").GetAttribute("value").Should().BeEmpty();
            sut.Find("#confirmPassword").GetAttribute("value").Should().BeEmpty();
        }
        if (registrationStatus == HttpStatusCode.BadRequest)
        {
            sut.Find("#password-errors").TextContent.Should().Contain("Server rejected password");
        }
    }

    [Fact]
    public void Submit_WhenPolicyChanges_ShouldUseNewPolicyInsteadOfPreviousPolicy()
    {
        // Arrange
        var handler = new Handler(request => request.Method == HttpMethod.Get
            ? PolicyResponse(12)
            : RegistrationResponse(HttpStatusCode.NoContent));
        var sut = RenderForm(handler);
        FillForm(sut, "Short1!", "Short1!");
        sut.Find("form").Submit();
        handler.Respond = request => request.Method == HttpMethod.Get
            ? PolicyResponse(6)
            : RegistrationResponse(HttpStatusCode.NoContent);
        sut.Find("#password").Change("Short1!");
        sut.Find("#confirmPassword").Change("Short1!");

        // Act
        sut.Find("form").Submit();

        // Assert
        handler.PostCount.Should().Be(1);
        sut.Markup.Should().Contain("Check your email");
    }

    [Fact]
    public void Submit_WhenPreviouslyRetrievedPolicyBecomesUnavailable_ShouldNotUseStalePolicy()
    {
        // Arrange
        var handler = new Handler(_ => PolicyResponse(12));
        var sut = RenderForm(handler);
        FillForm(sut, "Short1!", "Short1!");
        sut.Find("form").Submit();
        handler.Respond = request => request.Method == HttpMethod.Get
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : RegistrationResponse(HttpStatusCode.NoContent);
        sut.Find("#password").Change("Short1!");
        sut.Find("#confirmPassword").Change("Short1!");

        // Act
        sut.Find("form").Submit();

        // Assert
        handler.PostCount.Should().Be(1);
        sut.Markup.Should().Contain("Check your email");
    }

    [Fact]
    public void Submit_WhenRegistrationNetworkFails_ShouldClearBothPasswordsAndShowError()
    {
        // Arrange
        var handler = new Handler(request => request.Method == HttpMethod.Get
            ? PolicyResponse(6)
            : throw new HttpRequestException());
        var sut = RenderForm(handler);
        FillForm(sut, "Password1!", "Password1!");

        // Act
        sut.Find("form").Submit();

        // Assert
        sut.Find("#password").GetAttribute("value").Should().BeEmpty();
        sut.Find("#confirmPassword").GetAttribute("value").Should().BeEmpty();
        sut.Markup.Should().Contain("Registration is unavailable");
    }

    [Fact]
    public void ChangePassword_WhenConfirmationWasMatching_ShouldUpdateMismatchFeedback()
    {
        // Arrange
        var sut = RenderForm(new Handler(_ => PolicyResponse(6)));
        FillForm(sut, "Password1!", "Password1!");

        // Act
        sut.Find("#password").Change("Different1!");

        // Assert
        sut.Find("#confirm-password-errors").TextContent.Should().Contain("do not match");
    }

    private IRenderedComponent<Register> RenderForm(Handler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        Services.AddSingleton(new RegistrationClient(http));
        Services.AddSingleton(provider => new PasswordPolicyClient(http, provider.GetRequiredService<ILogger<PasswordPolicyClient>>()));

        return RenderComponent<Register>();
    }

    private static void FillForm(IRenderedComponent<Register> form, string password, string confirmation)
    {
        form.Find("#firstName").Change("Jane");
        form.Find("#lastName").Change("Doe");
        form.Find("#email").Change("jane@example.com");
        form.Find("#password").Change(password);
        form.Find("#confirmPassword").Change(confirmation);
    }

    private static HttpResponseMessage PolicyResponse(int length) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new PasswordPolicy
        {
            SchemaVersion = 1, RequiredLength = length, RequiredUniqueChars = 1,
            RequireDigit = true, RequireLowercase = true, RequireUppercase = true, RequireNonAlphanumeric = true
        })
    };

    private static HttpResponseMessage RegistrationResponse(HttpStatusCode status) => new(status)
    {
        Content = status == HttpStatusCode.BadRequest
            ? JsonContent.Create(new { errors = new Dictionary<string, string[]> { ["Password"] = ["Server rejected password"] } })
            : null
    };

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        internal Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = respond;
        internal int PostCount { get; private set; }
        internal string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post)
            {
                PostCount++;
                Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            }

            return Respond(request);
        }
    }
}
