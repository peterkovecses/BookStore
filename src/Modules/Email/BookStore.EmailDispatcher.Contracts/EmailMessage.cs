namespace BookStore.EmailDispatcher.Contracts;

public sealed record EmailMessage(string From, string To, string Subject, string TextBody, string? HtmlBody = null);
