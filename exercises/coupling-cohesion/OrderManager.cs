// Run with: dotnet run OrderManager.cs
// This code works. Your job is to find what makes it hard to change.

var manager = new OrderManager();
var order = manager.PlaceOrder("fan@example.com", "ARENA-2026-07-14", 4, 89.50m, true, false, true);
Console.WriteLine($"Order {order.Id} status {order.Status}, total {order.Total:C}");

var report = new SettlementReport();
Console.WriteLine($"Venue fee for order: {report.VenueFee(order):C}");

manager.Cancel(order);
Console.WriteLine($"Order {order.Id} status {order.Status}");

public class Order
{
    public int Id;
    public string Email = ""; //value object Person
    public string EventCode = ""; // start value object Financial
    public int Quantity;
    public decimal Total; //end value object financial
    public int Status; // 0 = new, 1 = paid, 2 = cancelled, 3 = refunded // enum maken
    public DateTime PlacedAt; //private maken
}

public enum OrderStatus
{
    New = 0,
    Paid = 1,
    Cancelled = 2,
    Refunded = 3
}

public class OrderManager
{
    public static List<Order> AllOrders = new();
    private static int _nextId = 1;

    public Order PlaceOrder(string email, string eventCode, int quantity, decimal price,
        bool sendEmail, bool isComp, bool applyFee) // order meegeven
    // options pattern
    {
        if (!email.Contains('@')) throw new ArgumentException("bad email"); //functional bug

        var order = new Order
        {
            Id = _nextId++, // auto increment
            Email = email,
            EventCode = eventCode,
            Quantity = quantity,
            PlacedAt = DateTime.Now, //time provider gebruiken
        };

        //private methode start
        var subtotal = isComp ? 0 : quantity * price;
        var fee = applyFee ? Math.Round(subtotal * 0.07m + quantity * 1.25m, 2) : 0;
        var tax = eventCode.StartsWith("ARENA") ? subtotal * 0.06m : subtotal * 0.04m;
        order.Total = subtotal + fee + tax;
        //private methode end
        
        order.Status = 1;
        AllOrders.Add(order);

        if (sendEmail)
        {
            var body = $"<h1>Thanks!</h1><p>You bought {quantity} tickets for {eventCode}.</p>";
            new SmtpEmailClient("smtp.tickettide.local", 25).Send(email, "Your tickets", body); //DI
        }

        File.AppendAllText("orders.log", $"{DateTime.Now:O} placed {order.Id}{Environment.NewLine}");
        return order;
    }

    public void Cancel(Order order)
    {
        if (order.Status == 1 && (DateTime.Now - order.PlacedAt).TotalHours < 24) 
        {
            order.Status = 2;
        }
        else if (order.Status == 1)
        {
            order.Status = 3;
        }
        File.AppendAllText("orders.log", $"{DateTime.Now:O} cancelled {order.Id}{Environment.NewLine}"); //di
    }

    public string FormatSeatLabel(string section, int row, int seat) => $"{section}-{row:D2}-{seat:D3}";

    public decimal ConvertToEuros(decimal amount) => amount * 0.92m;
}

public class SettlementReport
{
    public decimal VenueFee(Order order)
    {
        return order.Status == 1 ? order.Total : 0; // value object status en fee
    }

    public int PaidOrderCount() => OrderManager.AllOrders.Count(o => o.Status == 1);
}

public class SmtpEmailClient(string host, int port)
{
    public void Send(string to, string subject, string body) =>
        Console.WriteLine($"[smtp {host}:{port}] to={to} subject={subject} ({body.Length} chars)");
}
