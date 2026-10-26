using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using TicketTide.Web.Data;
using TicketTide.Web.Infrastructure;

namespace TicketTide.Web.Features.TransferOrder;

public sealed record TransferOrder(Guid OrderId);

/// <summary>transfer a paid order. Its tickets stop working and it generates a new ticket for the recipient.</summary>
internal sealed class RefundOrderEndpoint(AppDbContext db, ITransferGateway transferGateway, IEmailSender recipiënt)
    : Endpoint<TransferOrder, OrderResponse>
{
    public override void Configure()
    {
        Post("/orders/{orderId}/tickets/{ticketId}/transfer");
        AllowAnonymous();
        Summary(s => s.Summary = "transfer a paid order. Its tickets stop working and it generates a new ticket for the recipient.");
    }

    public override async Task HandleAsync(TransferOrder req, CancellationToken ct)
    {
        var order = await db.Orders.Include(o => o.Tickets).FirstOrDefaultAsync(o => o.Id == req.OrderId, ct);
        if (order is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (order.Status != OrderStatus.Paid)
        {
            await Send.ResultAsync(TypedResults.Problem(statusCode: 400, detail: $"Only a paid order can be refunded. This one is {order.Status}."));
            return;
        }

        order.Status = OrderStatus.Transferd;
        foreach (var ticket in order.Tickets)
        {
            ticket.Status = TicketStatus.Transferd;
        }

        // The same idempotency key every time, so a retried or concurrent refund never refunds the card twice.
        var refund = await transferGateway.Transfer(order, recipiënt);
        if (!refund.Succeeded)
        {
            await Send.ResultAsync(TypedResults.Problem(statusCode: 500, detail: refund.FailureReason ?? "The payment provider could not refund the card."));
            return;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request refunded this order first. The card was refunded once, by whichever request got there.
            await Send.ResultAsync(TypedResults.Problem(statusCode: 409, detail: "This order was refunded by another request."));
        }
    }
}
