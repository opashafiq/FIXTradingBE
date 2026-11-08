using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradingBackend.Api.Data;
using TradingBackend.Api.DTOs.Orders;
using TradingBackend.Api.Entities;

namespace TradingBackend.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly TradingDbContext _dbContext;

    public OrdersController(TradingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // GET: api/orders
    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderResponseDto>>> GetOrders()
    {
        var orders = await _dbContext.Orders
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new OrderResponseDto
            {
                Id = x.Id,
                ClOrdId = x.ClOrdId,
                OrigClOrdId = x.OrigClOrdId,
                ExchangeOrderId = x.ExchangeOrderId,
                Symbol = x.Symbol,
                OrderBook = x.OrderBook,
                Side = x.Side,
                OrdType = x.OrdType,
                OrderQty = x.OrderQty,
                Price = x.Price,
                TimeInForce = x.TimeInForce,
                OrdStatus = x.OrdStatus,
                LeavesQty = x.LeavesQty,
                CumQty = x.CumQty,
                AvgPx = x.AvgPx,
                Account = x.Account,
                AccountType = x.AccountType,
                OrderSource = x.OrderSource,
                CreatedAtUtc = x.CreatedAtUtc,
                UpdatedAtUtc = x.UpdatedAtUtc
            })
            .ToListAsync();

        return Ok(orders);
    }

    // GET: api/orders/1
    [HttpGet("{id:long}")]
    public async Task<ActionResult<OrderResponseDto>> GetOrder(long id)
    {
        var order = await _dbContext.Orders
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new OrderResponseDto
            {
                Id = x.Id,
                ClOrdId = x.ClOrdId,
                OrigClOrdId = x.OrigClOrdId,
                ExchangeOrderId = x.ExchangeOrderId,
                Symbol = x.Symbol,
                OrderBook = x.OrderBook,
                Side = x.Side,
                OrdType = x.OrdType,
                OrderQty = x.OrderQty,
                Price = x.Price,
                TimeInForce = x.TimeInForce,
                OrdStatus = x.OrdStatus,
                LeavesQty = x.LeavesQty,
                CumQty = x.CumQty,
                AvgPx = x.AvgPx,
                Account = x.Account,
                AccountType = x.AccountType,
                OrderSource = x.OrderSource,
                CreatedAtUtc = x.CreatedAtUtc,
                UpdatedAtUtc = x.UpdatedAtUtc
            })
            .FirstOrDefaultAsync();

        if (order == null)
            return NotFound(new { Message = "Order not found." });

        return Ok(order);
    }

    // POST: api/orders
    [HttpPost]
    public async Task<ActionResult<OrderResponseDto>> CreateOrder(
        CreateOrderDto dto)
    {
        var order = new TradingOrder
        {
            ClOrdId = dto.ClOrdId,
            Symbol = dto.Symbol,
            OrderBook = dto.OrderBook,
            Side = dto.Side,
            OrdType = dto.OrdType,
            OrderQty = dto.OrderQty,
            Price = dto.Price,
            TimeInForce = dto.TimeInForce,
            Account = dto.Account,
            AccountType = dto.AccountType,
            OrderSource = dto.OrderSource,
            OrigClOrdId = dto.OrigClOrdId,
            OrdStatus = "PendingNew",
            LeavesQty = dto.OrderQty,
            CumQty = 0
        };

        _dbContext.Orders.Add(order);

        await _dbContext.SaveChangesAsync();

        var response = new OrderResponseDto
        {
            Id = order.Id,
            ClOrdId = order.ClOrdId,
            OrigClOrdId = order.OrigClOrdId,
            ExchangeOrderId = order.ExchangeOrderId,
            Symbol = order.Symbol,
            OrderBook = order.OrderBook,
            Side = order.Side,
            OrdType = order.OrdType,
            OrderQty = order.OrderQty,
            Price = order.Price,
            TimeInForce = order.TimeInForce,
            OrdStatus = order.OrdStatus,
            LeavesQty = order.LeavesQty,
            CumQty = order.CumQty,
            AvgPx = order.AvgPx,
            Account = order.Account,
            AccountType = order.AccountType,
            OrderSource = order.OrderSource,
            CreatedAtUtc = order.CreatedAtUtc,
            UpdatedAtUtc = order.UpdatedAtUtc
        };

        return CreatedAtAction(
            nameof(GetOrder),
            new { id = order.Id },
            response);
    }

    // PUT: api/orders/1
    [HttpPut("{id:long}")]
    public async Task<IActionResult> UpdateOrder(
        long id,
        UpdateOrderDto dto)
    {
        var order = await _dbContext.Orders.FindAsync(id);

        if (order == null)
            return NotFound(new { Message = "Order not found." });

        order.OrdStatus = dto.OrdStatus ?? order.OrdStatus;
        order.ExchangeOrderId = dto.ExchangeOrderId ?? order.ExchangeOrderId;
        order.LeavesQty = dto.LeavesQty ?? order.LeavesQty;
        order.CumQty = dto.CumQty ?? order.CumQty;
        order.AvgPx = dto.AvgPx ?? order.AvgPx;
        order.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync();

        return NoContent();
    }

    // DELETE: api/orders/1
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteOrder(long id)
    {
        var order = await _dbContext.Orders
            .Include(x => x.Executions)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (order == null)
            return NotFound(new { Message = "Order not found." });

        if (order.Executions.Count > 0)
        {
            return Conflict(new
            {
                Message = "Cannot delete an order that has execution records."
            });
        }

        _dbContext.Orders.Remove(order);

        await _dbContext.SaveChangesAsync();

        return NoContent();
    }
}