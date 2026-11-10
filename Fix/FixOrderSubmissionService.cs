
using Microsoft.Extensions.Logging;
using QuickFix;
using QuickFix.Fields;
using TradingBackend.Api.Configuration;
using TradingBackend.Api.DTOs.Orders;

namespace TradingBackend.Api.Fix;

public class FixOrderSubmissionService
{
    private readonly FixApplication _fixApplication;
    private readonly ILogger<FixOrderSubmissionService> _logger;

    public FixOrderSubmissionService(
        FixApplication fixApplication,
        ILogger<FixOrderSubmissionService> logger)
    {
        _fixApplication = fixApplication;
        _logger = logger;
    }

    public bool IsConnected => _fixApplication.IsLoggedOn;

    public SessionID? GetActiveSession()
    {
        if (!IsConnected)
            return null;

        return _fixApplication.ActiveSession;
    }

    public QuickFix.FIX50SP2.NewOrderSingle BuildNewOrderSingle(
        CreateOrderDto order)
    {

        // 2. Validate mandatory fields
        if (string.IsNullOrWhiteSpace(order.ClOrdId))
            throw new ArgumentException("ClOrdID is required.");

        if (string.IsNullOrWhiteSpace(order.Symbol))
            throw new ArgumentException("Symbol is required.");

        if (order.OrderBook == null)
            throw new ArgumentException("OrderBook is required.");

        if (order.OrderQty <= 0)
            throw new ArgumentException("OrderQty must be greater than zero.");

        if (order.Side != "1" && order.Side != "2")
            throw new ArgumentException("Side must be 1 (Buy) or 2 (Sell).");

        // 3. Validate order type
        string[] validOrderTypes = { "1", "2", "3", "4", "K", "J" };

        if (string.IsNullOrWhiteSpace(order.OrdType) ||
            !validOrderTypes.Contains(order.OrdType))
        {
            throw new ArgumentException("Invalid OrdType.");
        }

        bool requiresPrice =
            order.OrdType == "2" || order.OrdType == "4";

        bool requiresStopPx =
            order.OrdType == "3" ||
            order.OrdType == "4" ||
            order.OrdType == "J";

        if (requiresPrice &&
            (!order.Price.HasValue || order.Price.Value <= 0))
        {
            throw new ArgumentException(
                "Price is required and must be greater than zero for this order type.");
        }

        if (requiresStopPx &&
            (!order.StopPx.HasValue || order.StopPx.Value <= 0))
        {
            throw new ArgumentException(
                "StopPx is required and must be greater than zero for this order type.");
        }

        // 4. Validate optional OrderSource
        if (!OrderSourceConstants.IsValid(order.OrderSource))
        {
            throw new ArgumentException(
                $"Invalid OrderSource: {order.OrderSource}");
        }

        // 5. Construct FIX 5.0 SP2 NewOrderSingle
        var message = new QuickFix.FIX50SP2.NewOrderSingle();

        message.Set(new ClOrdID(order.ClOrdId));
        message.Set(new Symbol(order.Symbol));
        message.Set(new Side(order.Side[0]));
        message.Set(new TransactTime(DateTime.UtcNow));
        message.Set(new OrdType(order.OrdType[0]));
        message.Set(new OrderQty(order.OrderQty));

        // 6. CHSE custom field: OrderBook (30001)
        message.SetField(
            new IntField(30001, order.OrderBook.Value));

        // 7. Price (44)
        if (requiresPrice)
        {
            message.Set(new Price(order.Price!.Value));
        }

        // 8. StopPx (99)
        if (requiresStopPx)
        {
            message.Set(new StopPx(order.StopPx!.Value));
        }

        // 9. Optional Account (1)
        if (!string.IsNullOrWhiteSpace(order.Account))
        {
            message.Set(new Account(order.Account));
        }

        // 10. Optional AccountType (581)
        if (order.AccountType.HasValue)
        {
            message.SetField(
                new IntField(581, order.AccountType.Value));
        }

        // 11. Optional OrderSource (30004)
        if (!string.IsNullOrWhiteSpace(order.OrderSource))
        {
            message.SetField(
                new StringField(30004, order.OrderSource));
        }

        // 12. Optional TimeInForce (59)
        if (!string.IsNullOrWhiteSpace(order.TimeInForce))
        {
            if (order.TimeInForce.Length != 1)
            {
                throw new ArgumentException(
                    "TimeInForce must contain a single FIX code.");
            }

            message.Set(
                new TimeInForce(order.TimeInForce[0]));
        }

        _logger.LogInformation(
            "NewOrderSingle message constructed. ClOrdID: {ClOrdId}, Symbol: {Symbol}",
            order.ClOrdId,
            order.Symbol);

        return message;
    }

    public string PreviewNewOrderSingle(CreateOrderDto order)
    {
        var message = BuildNewOrderSingle(order);

        return message.ToString();
    }
}
