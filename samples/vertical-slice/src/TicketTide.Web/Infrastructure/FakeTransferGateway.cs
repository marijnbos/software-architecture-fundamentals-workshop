using System.Collections.Concurrent;
using TicketTide.Web.Data;

namespace TicketTide.Web.Infrastructure;

/// <summary>
/// The external payment provider. TicketTide only ever sees tokens, never card numbers. Every call takes an
/// idempotency key, so sending the same request twice doesn't move money twice.
/// </summary>
public interface ITransferGateway
{
    Task<TransferResult> Transfer(Order order, IEmailSender recipiënt);
}

public sealed record TransferResult(bool Succeeded, string? FailureReason = null, string? Reference = null);

/// <summary>
/// A stand-in for a real provider such as Stripe. <c>tok_declined</c> is declined; every other token succeeds.
/// </summary>
internal sealed class FakeTransferGateway : ITransferGateway
{
    public const string DeclinedToken = "tok_declined";
    private readonly ConcurrentDictionary<string, TransferResult> _answers = new();


    public Task<TransferResult> Transfer(Order order, IEmailSender recipiënt)
    {
        return Task.FromResult(new TransferResult(true));
    }
}
