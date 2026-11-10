using Microsoft.Extensions.Logging.Abstractions;
using TradingBackend.Api.DTOs.Orders;
using TradingBackend.Api.Fix;

namespace TradingBackend.Api.Fix;

public static class NewOrderSingleBuilderTest
{
    public static void Run(FixApplication fixApplication)
    {
        var service = new FixOrderSubmissionService(
            fixApplication,
            NullLogger<FixOrderSubmissionService>.Instance);

        var dto = new CreateOrderDto
        {
            ClOrdId = "TEST-ORDER-001",
            Symbol = "ABC",
            Side = "1",
            OrdType = "2",
            OrderQty = 100,
            Price = 25.50m,
            OrderBook = 1,
            OrderSource = "WEB"
        };

        string preview = service.PreviewNewOrderSingle(dto);

        Console.WriteLine("========== NEW ORDER SINGLE PREVIEW ==========");
        Console.WriteLine(preview.Replace('\u0001', '|'));
        Console.WriteLine("==============================================");
    }
}