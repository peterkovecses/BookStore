namespace BookStore.EmailDispatcher.Contracts;

/// <summary>Provides the current availability of email dispatch.</summary>
public interface IEmailDispatchAvailability
{
    /// <summary>Gets the current email dispatcher availability without reserving a send attempt.</summary>
    /// <returns>A snapshot of availability and, when known, the time after which a retry may be attempted.</returns>
    EmailDispatchAvailability GetAvailability();
}
