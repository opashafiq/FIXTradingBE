
using System.Globalization;
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
        CreateOrderDto order,
        string chseFirmId,
        string chseWorkstationId)
    {
        // 1. Validate firm and workstation identities

        if (string.IsNullOrWhiteSpace(chseFirmId))
            throw new ArgumentException("CHSE Firm ID is required.");

        if (string.IsNullOrWhiteSpace(chseWorkstationId))
            throw new ArgumentException("CHSE Workstation ID is required.");

        // 2. Validate mandatory order fields

        if (string.IsNullOrWhiteSpace(order.ClOrdId))
            throw new ArgumentException("ClOrdID is required.");

        if (string.IsNullOrWhiteSpace(order.Symbol))
            throw new ArgumentException("Symbol is required.");

        int orderBook = order.OrderBook ?? 1;

        int[] validOrderBooks = { 1, 3, 4, 7, 100, 102 };

        if (!validOrderBooks.Contains(orderBook))
        {
            throw new ArgumentException(
                $"Invalid OrderBook: {orderBook}. Supported values are 1, 3, 4, 7, 100, 102.");
        }

        if (order.OrderQty <= 0)
            throw new ArgumentException(
                "OrderQty must be greater than zero.");

        if (order.Side != "1" && order.Side != "2")
            throw new ArgumentException(
                "Side must be 1 (Buy) or 2 (Sell).");

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

        // 4. Validate OrderSource

        if (!OrderSourceConstants.IsValid(order.OrderSource))
        {
            throw new ArgumentException(
                $"Invalid OrderSource: {order.OrderSource}");
        }

        // 5. Validate TimeInForce

        string[] validTimeInForceValues =
        {
            "0", "1", "2", "3", "4", "6", "7", "9"
        };

        if (!string.IsNullOrWhiteSpace(order.TimeInForce) &&
            !validTimeInForceValues.Contains(order.TimeInForce))
        {
            throw new ArgumentException(
                "Invalid TimeInForce. Supported values are 0, 1, 2, 3, 4, 6, 7, 9.");
        }

        bool isGtd = order.TimeInForce == "6";

        if (isGtd && !order.ExpireDate.HasValue)
            throw new ArgumentException(
                "ExpireDate is required when TimeInForce is GTD (6).");

        if (!isGtd && order.ExpireDate.HasValue)
            throw new ArgumentException(
                "ExpireDate can only be supplied when TimeInForce is GTD (6).");

        // 6. Construct FIX 5.0 SP2 NewOrderSingle

        var message = new QuickFix.FIX50SP2.NewOrderSingle();

        message.Set(new ClOrdID(order.ClOrdId));

        // 7. CHSE Parties repeating group (453)

        // Party 1: Firm (452=1)

        var firmParty =
            new QuickFix.FIX50SP2.NewOrderSingle.NoPartyIDsGroup();

        firmParty.Set(new PartyID(chseFirmId));
        firmParty.Set(new PartyIDSource('D'));
        firmParty.Set(new PartyRole(1));

        message.AddGroup(firmParty);

        // Party 2: Workstation (452=53)

        var workstationParty =
            new QuickFix.FIX50SP2.NewOrderSingle.NoPartyIDsGroup();

        workstationParty.Set(new PartyID(chseWorkstationId));
        workstationParty.Set(new PartyIDSource('D'));
        workstationParty.Set(new PartyRole(53));

        message.AddGroup(workstationParty);

        var partyCount = message.GetInt(Tags.NoPartyIDs);

        _logger.LogInformation(
            "Parties count in constructed message: {PartyCount}",
            partyCount);

        // 8. Standard order fields

        message.Set(new Symbol(order.Symbol));
        message.Set(new Side(order.Side[0]));
        message.Set(new TransactTime(DateTime.UtcNow));
        message.Set(new OrdType(order.OrdType[0]));
        message.Set(new OrderQty(order.OrderQty));

        // 9. CHSE custom field: OrderBook (30001)

        message.SetField(new IntField(30001, orderBook));

        // 10. Price (44)

        if (requiresPrice)
        {
            message.Set(new Price(order.Price!.Value));
        }

        // 11. StopPx (99)

        if (requiresStopPx)
        {
            message.Set(new StopPx(order.StopPx!.Value));
        }

        // 12. Client Code / Account (1)

        if (!string.IsNullOrWhiteSpace(order.ClientCode))
        {
            message.Set(new Account(order.ClientCode));
        }

        // 13. AccountType (581)

        if (order.AccountType.HasValue)
        {
            message.SetField(
                new IntField(581, order.AccountType.Value));
        }

        // 14. OrderSource (30004)

        if (!string.IsNullOrWhiteSpace(order.OrderSource))
        {
            message.SetField(
                new StringField(30004, order.OrderSource));
        }

        // 15. NoTradeKey (7714)

        if (!string.IsNullOrWhiteSpace(order.NoTradeKey))
        {
            message.SetField(
                new StringField(7714, order.NoTradeKey));
        }

        // 16. BO ID / StreamText (40056)

        if (!string.IsNullOrWhiteSpace(order.BoId))
        {
            message.SetField(
                new StringField(40056, order.BoId));
        }

        // 17. TimeInForce (59)

        if (!string.IsNullOrWhiteSpace(order.TimeInForce))
        {
            message.Set(
                new TimeInForce(order.TimeInForce[0]));
        }

        // 18. ExpireDate (432) - Required for GTD

        if (isGtd)
        {
            string expireDate = order.ExpireDate!.Value.ToString(
                "yyyyMMdd",
                CultureInfo.InvariantCulture);

            message.SetField(
                new StringField(432, expireDate));
        }

        _logger.LogInformation(
            "NewOrderSingle preview constructed. ClOrdID: {ClOrdId}, Symbol: {Symbol}, TIF: {TimeInForce}",
            order.ClOrdId,
            order.Symbol,
            order.TimeInForce);

                _logger.LogInformation(
        "FIX message before serialization: {Message}",
        message.ToString());

        _logger.LogInformation(
            "NewOrderSingle FieldOrder: {FieldOrder}",
            string.Join(",", message.FieldOrder));

        _logger.LogInformation(
            "NewOrderSingle GroupTags: {GroupTags}",
            string.Join(",", message.GetGroupTags()));

        return message;
    }


public QuickFix.FIX50SP2.OrderCancelRequest BuildOrderCancelRequest(
    string clOrdId,
    string? origClOrdId,
    string? orderId,
    string symbol,
    string side,
    int? orderBook,
    string chseWorkstationId)
{
    // 1. Validate mandatory fields.

    if (string.IsNullOrWhiteSpace(clOrdId))
        throw new ArgumentException("Cancellation ClOrdID is required.");

    if (string.IsNullOrWhiteSpace(origClOrdId) &&
        string.IsNullOrWhiteSpace(orderId))
    {
        throw new ArgumentException(
            "Either OrigClOrdID or OrderID must be supplied.");
    }

    if (string.IsNullOrWhiteSpace(symbol))
        throw new ArgumentException("Symbol is required.");

    if (side != "1" && side != "2")
        throw new ArgumentException(
            "Side must be 1 (Buy) or 2 (Sell).");

    if (string.IsNullOrWhiteSpace(chseWorkstationId))
        throw new ArgumentException(
            "CHSE Workstation ID is required.");

    // 2. Validate OrderBook, when supplied.

    int[] validOrderBooks = { 1, 3, 4, 7, 100, 102 };

    if (orderBook.HasValue &&
        !validOrderBooks.Contains(orderBook.Value))
    {
        throw new ArgumentException(
            $"Invalid OrderBook: {orderBook.Value}.");
    }

    // 3. Construct FIX 5.0 SP2 OrderCancelRequest (35=F).

    var message = new QuickFix.FIX50SP2.OrderCancelRequest();

    message.Set(new ClOrdID(clOrdId));

    if (!string.IsNullOrWhiteSpace(origClOrdId))
        message.Set(new OrigClOrdID(origClOrdId));

    if (!string.IsNullOrWhiteSpace(orderId))
        message.Set(new OrderID(orderId));

    message.Set(new Symbol(symbol));
    message.Set(new Side(side[0]));
    message.Set(new TransactTime(DateTime.UtcNow));

    // 4. CHSE Trading Mnemonic party (PartyRole=53).

    var workstationParty =
        new QuickFix.FIX50SP2.OrderCancelRequest.NoPartyIDsGroup();

    workstationParty.Set(new PartyID(chseWorkstationId));
    workstationParty.Set(new PartyIDSource('D'));
    workstationParty.Set(new PartyRole(53));

    message.AddGroup(workstationParty);

    // 5. CHSE custom field: OrderBook (30001).

    if (orderBook.HasValue)
    {
        message.SetField(
            new IntField(30001, orderBook.Value));
    }

    _logger.LogInformation(
        "OrderCancelRequest constructed. " +
        "ClOrdID: {ClOrdId}, OrigClOrdID: {OrigClOrdId}, " +
        "OrderID: {OrderId}, Symbol: {Symbol}",
        clOrdId,
        origClOrdId,
        orderId,
        symbol);

    _logger.LogInformation(
        "OrderCancelRequest FIX message: {Message}",
        message.ToString());

    return message;
}


public QuickFix.FIX50SP2.OrderCancelReplaceRequest
    BuildOrderCancelReplaceRequest(
        string clOrdId,
        string? origClOrdId,
        string? orderId,
        string symbol,
        string side,
        int? orderBook,
        string ordType,
        decimal orderQty,
        decimal? price,
        decimal? stopPx,
        string? timeInForce,
        DateTime? expireDate,
        string chseWorkstationId)
{
    // 1. Validate mandatory identifiers.

    if (string.IsNullOrWhiteSpace(clOrdId))
        throw new ArgumentException("Replacement ClOrdID is required.");

    if (string.IsNullOrWhiteSpace(origClOrdId) &&
        string.IsNullOrWhiteSpace(orderId))
    {
        throw new ArgumentException(
            "Either OrigClOrdID or OrderID must be supplied.");
    }

    if (string.IsNullOrWhiteSpace(symbol))
        throw new ArgumentException("Symbol is required.");

    if (side != "1" && side != "2")
        throw new ArgumentException(
            "Side must be 1 (Buy) or 2 (Sell).");

    if (string.IsNullOrWhiteSpace(chseWorkstationId))
        throw new ArgumentException(
            "CHSE Workstation ID is required.");

    // 2. Validate OrderBook.

    int effectiveOrderBook = orderBook ?? 1;

    int[] validOrderBooks = { 1, 3, 4, 7, 100, 102 };

    if (!validOrderBooks.Contains(effectiveOrderBook))
        throw new ArgumentException(
            $"Invalid OrderBook: {effectiveOrderBook}.");

    // 3. Validate order quantity.

    if (orderQty <= 0)
        throw new ArgumentException(
            "OrderQty must be greater than zero.");

    // 4. Validate original order type.
    // OrdType is retained from the existing order.

    string[] validOrderTypes = { "1", "2", "3", "4", "K", "J" };

    if (string.IsNullOrWhiteSpace(ordType) ||
        !validOrderTypes.Contains(ordType))
    {
        throw new ArgumentException("Invalid OrdType.");
    }

    bool requiresPrice = ordType == "2" || ordType == "4";

    bool requiresStopPx =
        ordType == "3" || ordType == "4" || ordType == "J";

    if (requiresPrice && (!price.HasValue || price.Value <= 0))
        throw new ArgumentException(
            "Price is required and must be greater than zero for this order type.");

    if (requiresStopPx && (!stopPx.HasValue || stopPx.Value <= 0))
        throw new ArgumentException(
            "StopPx is required and must be greater than zero for this order type.");

    // 5. Validate TimeInForce.

    string[] validTimeInForceValues =
        { "0", "1", "2", "3", "4", "6", "7", "9" };

    if (!string.IsNullOrWhiteSpace(timeInForce) &&
        !validTimeInForceValues.Contains(timeInForce))
    {
        throw new ArgumentException(
            "Invalid TimeInForce. Supported values are 0, 1, 2, 3, 4, 6, 7, 9.");
    }

    bool isGtd = timeInForce == "6";

    if (isGtd && !expireDate.HasValue)
        throw new ArgumentException(
            "ExpireDate is required when TimeInForce is GTD (6).");

    if (!isGtd && expireDate.HasValue)
        throw new ArgumentException(
            "ExpireDate can only be supplied when TimeInForce is GTD (6).");

    // 6. Construct FIX 5.0 SP2 OrderCancelReplaceRequest (35=G).

    var message =
        new QuickFix.FIX50SP2.OrderCancelReplaceRequest();

    message.Set(new ClOrdID(clOrdId));

    if (!string.IsNullOrWhiteSpace(origClOrdId))
        message.Set(new OrigClOrdID(origClOrdId));

    if (!string.IsNullOrWhiteSpace(orderId))
        message.Set(new OrderID(orderId));

    // 7. CHSE Trading Mnemonic party (PartyRole=53).

    var workstationParty =
        new QuickFix.FIX50SP2.OrderCancelReplaceRequest.NoPartyIDsGroup();

    workstationParty.Set(new PartyID(chseWorkstationId));
    workstationParty.Set(new PartyIDSource('D'));
    workstationParty.Set(new PartyRole(53));

    message.AddGroup(workstationParty);

    // 8. Standard order fields.

    message.Set(new Symbol(symbol));
    message.Set(new Side(side[0]));
    message.Set(new OrdType(ordType[0]));
    message.Set(new OrderQty(orderQty));
    message.Set(new TransactTime(DateTime.UtcNow));

    // 9. CHSE custom field: OrderBook (30001).

    message.SetField(
        new IntField(30001, effectiveOrderBook));

    // 10. Price (44).

    if (price.HasValue)
        message.Set(new Price(price.Value));

    // 11. StopPx (99).

    if (stopPx.HasValue)
        message.Set(new StopPx(stopPx.Value));

    // 12. TimeInForce (59).

    if (!string.IsNullOrWhiteSpace(timeInForce))
        message.Set(new TimeInForce(timeInForce[0]));

    // 13. ExpireDate (432).

    if (isGtd)
    {
        string formattedExpireDate = expireDate!.Value.ToString(
            "yyyyMMdd",
            CultureInfo.InvariantCulture);

        message.SetField(
            new StringField(432, formattedExpireDate));
    }

    // 14. Logging.

    _logger.LogInformation(
        "OrderCancelReplaceRequest constructed. " +
        "ClOrdID: {ClOrdId}, OrigClOrdID: {OrigClOrdId}, " +
        "OrderID: {OrderId}, Symbol: {Symbol}, OrderQty: {OrderQty}",
        clOrdId,
        origClOrdId,
        orderId,
        symbol,
        orderQty);

    _logger.LogInformation(
        "OrderCancelReplaceRequest FIX message: {Message}",
        message.ToString());

    return message;
}

    public void SubmitOrderCancelRequest(
        string clOrdId,
        string? origClOrdId,
        string? orderId,
        string symbol,
        string side,
        int? orderBook,
        string chseWorkstationId)
    {
        // 1. Build and validate the FIX message.
        var message = BuildOrderCancelRequest(
            clOrdId,
            origClOrdId,
            orderId,
            symbol,
            side,
            orderBook,
            chseWorkstationId);

        _logger.LogInformation(
            "Submitting OrderCancelRequest to CHSE. " +
            "ClOrdID: {ClOrdId}, OrigClOrdID: {OrigClOrdId}, OrderID: {OrderId}",
            clOrdId,
            origClOrdId,
            orderId);

        // 2. Submit through the active FIX session.
        _fixApplication.SendToExchange(message);

        _logger.LogInformation(
            "OrderCancelRequest successfully submitted to QuickFIX/n. " +
            "ClOrdID: {ClOrdId}",
            clOrdId);
    }

public void SubmitOrderCancelReplaceRequest(
    string clOrdId,
    string? origClOrdId,
    string? orderId,
    string symbol,
    string side,
    int? orderBook,
    string ordType,
    decimal orderQty,
    decimal? price,
    decimal? stopPx,
    string? timeInForce,
    DateTime? expireDate,
    string chseWorkstationId)
{
    // 1. Build and validate the FIX message.
    var message = BuildOrderCancelReplaceRequest(
        clOrdId,
        origClOrdId,
        orderId,
        symbol,
        side,
        orderBook,
        ordType,
        orderQty,
        price,
        stopPx,
        timeInForce,
        expireDate,
        chseWorkstationId);

    _logger.LogInformation(
        "Submitting OrderCancelReplaceRequest to CHSE. " +
        "ClOrdID: {ClOrdId}, OrigClOrdID: {OrigClOrdId}, " +
        "OrderID: {OrderId}",
        clOrdId,
        origClOrdId,
        orderId);

    // 2. Submit through the active FIX session.
    _fixApplication.SendToExchange(message);

    _logger.LogInformation(
        "OrderCancelReplaceRequest successfully submitted to QuickFIX/n. " +
        "ClOrdID: {ClOrdId}",
        clOrdId);
}

    public string PreviewNewOrderSingle(
        CreateOrderDto order,
        string chseFirmId,
        string chseWorkstationId)
    {
        var message = BuildNewOrderSingle(
            order,
            chseFirmId,
            chseWorkstationId);

        return message.ToString();
    }

    public void SubmitNewOrderSingle(
        CreateOrderDto order,
        string chseFirmId,
        string chseWorkstationId)
    {
        // 1. Build and validate the FIX message
        var message = BuildNewOrderSingle(
            order,
            chseFirmId,
            chseWorkstationId);

        _logger.LogInformation(
            "Submitting NewOrderSingle to CHSE. ClOrdID: {ClOrdId}, Symbol: {Symbol}",
            order.ClOrdId,
            order.Symbol);

        // 2. Send through the active FIX session
        _fixApplication.SendToExchange(message);

        _logger.LogInformation(
            "NewOrderSingle successfully submitted to QuickFIX/n. ClOrdID: {ClOrdId}",
            order.ClOrdId);
    }    
}
