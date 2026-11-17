using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradingBackend.Api.Data;
using TradingBackend.Api.DTOs.Orders;
using TradingBackend.Api.Entities;
using TradingBackend.Api.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using TradingBackend.Api.Identity;
using TradingBackend.Api.Fix;


namespace TradingBackend.Api.Controllers;

[Authorize(Roles = RoleConstants.FirmAdmin + "," + RoleConstants.Trader)]
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly TradingDbContext _dbContext;

private readonly UserManager<ApplicationUser> _userManager;
private readonly FixOrderSubmissionService _fixOrderSubmissionService;
private readonly OrderCancellationService _orderCancellationService;
private readonly OrderReplacementService _orderReplacementService;


    public OrdersController(
    TradingDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    FixOrderSubmissionService fixOrderSubmissionService,
    OrderCancellationService orderCancellationService,
    OrderReplacementService orderReplacementService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _fixOrderSubmissionService = fixOrderSubmissionService;
        _orderCancellationService = orderCancellationService;
        _orderReplacementService = orderReplacementService;
    }

    // GET: api/orders
    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderResponseDto>>> GetOrders()
    {
        var firmId = await GetCurrentFirmIdAsync();

        if (firmId == null)
            return Forbid();
        var orders = await _dbContext.Orders
            .AsNoTracking()
            .Where(x => x.FirmId == firmId)
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
        var firmId = await GetCurrentFirmIdAsync();

        if (firmId == null)
            return Forbid();
        var order = await _dbContext.Orders
            .AsNoTracking()
            .Where(x => x.Id == id && x.FirmId == firmId)
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
[Authorize(Roles = RoleConstants.Trader)]
public async Task<ActionResult<OrderResponseDto>> CreateOrder(
    CreateOrderDto dto)
{
    // 1. Resolve the authenticated trader.
    var currentUser = await _userManager.GetUserAsync(User);

    if (currentUser == null || !currentUser.IsActive)
        return Unauthorized();

    if (!currentUser.FirmId.HasValue)
        return BadRequest("Trader is not associated with a firm.");

    if (string.IsNullOrWhiteSpace(currentUser.CHSEWorkstationId))
        return BadRequest(
            "CHSE workstation is not configured for this trader.");

    // 2. Resolve the CHSE firm identifier.
    var firm = await _dbContext.Set<Firm>()
        .AsNoTracking()
        .FirstOrDefaultAsync(f =>
            f.Id == currentUser.FirmId.Value &&
            f.IsActive);

    if (firm == null)
        return BadRequest("Trader's firm is not found or inactive.");

    if (string.IsNullOrWhiteSpace(firm.CHSEFirmId))
        return BadRequest("CHSE firm identifier is not configured.");

    var firmId = firm.Id;
    var chseFirmId = firm.CHSEFirmId;
    var chseWorkstationId = currentUser.CHSEWorkstationId;

    // 3. Validate OrderSource.
    if (!OrderSourceConstants.IsValid(dto.OrderSource))
    {
        return BadRequest(new
        {
            Message = $"Invalid OrderSource '{dto.OrderSource}'.",
            AllowedValues = OrderSourceConstants.ValidValues
        });
    }

    // 4. Check FIX connectivity.
    var activeSession = _fixOrderSubmissionService.GetActiveSession();

    if (activeSession == null)
    {
        return StatusCode(
            StatusCodes.Status503ServiceUnavailable,
            new { Message = "CHSE FIX session is not connected." });
    }

    var sessionId = activeSession.ToString();

    // 5. Build the FIX message first.
    // This performs the existing FIX field and value validations
    // before we save the order.
    try
    {
        _fixOrderSubmissionService.BuildNewOrderSingle(
            dto,
            chseFirmId,
            chseWorkstationId);
    }
    catch (ArgumentException ex)
    {
        return BadRequest(new { Message = ex.Message });
    }

    // 6. Create the database order.
    var order = new TradingOrder
    {
        FirmId = firmId,
        SessionId = sessionId,

        ClOrdId = dto.ClOrdId,
        OrigClOrdId = dto.OrigClOrdId,

        Symbol = dto.Symbol,
        OrderBook = dto.OrderBook,

        Side = dto.Side,
        OrdType = dto.OrdType,
        OrderQty = dto.OrderQty,
        Price = dto.Price,
        StopPx = dto.StopPx,
        TimeInForce = dto.TimeInForce,
        ExpireDate = dto.ExpireDate,

        Account = dto.ClientCode,
        AccountType = dto.AccountType,
        OrderSource = dto.OrderSource,

        // This is a local submission state, not exchange OrdStatus.
        SubmissionStatus = "PendingSubmission",

        // Actual exchange order status will be populated
        // from the incoming ExecutionReport.
        OrdStatus = null,

        LeavesQty = dto.OrderQty,
        CumQty = 0
    };

    await using var transaction =
        await _dbContext.Database.BeginTransactionAsync();

    _dbContext.Orders.Add(order);

    await _dbContext.SaveChangesAsync();

    // 7. Register the initial FIX client order identifier.
    var orderIdentifier = new OrderIdentifier
    {
        TradingOrderId = order.Id,
        SessionId = sessionId,
        ClOrdId = order.ClOrdId,
        OrigClOrdId = order.OrigClOrdId,
        CreatedAtUtc = DateTimeOffset.UtcNow
    };

    _dbContext.OrderIdentifiers.Add(orderIdentifier);

    await _dbContext.SaveChangesAsync();

    await transaction.CommitAsync();

    // 8. Submit the NewOrderSingle to QuickFIX/n.
    try
    {
        _fixOrderSubmissionService.SubmitNewOrderSingle(
            dto,
            chseFirmId,
            chseWorkstationId);

        order.SubmissionStatus = "SubmittedToFix";

        await _dbContext.SaveChangesAsync();
    }
    catch (Exception)
    {
        // Do not automatically retry here.
        // The outcome may be uncertain if an exception occurred
        // during transmission.

        order.SubmissionStatus = "SubmissionUnknown";

        await _dbContext.SaveChangesAsync();

        return StatusCode(
            StatusCodes.Status503ServiceUnavailable,
            new
            {
                Message =
                    "The order was saved, but FIX submission could not be confirmed. Please check the order status before attempting another submission.",
                OrderId = order.Id,
                ClOrdId = order.ClOrdId,
                SubmissionStatus = order.SubmissionStatus
            });
    }

    // 9. Build the existing API response.
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


// POST: api/orders/{id}/cancel
[HttpPost("{id:long}/cancel")]
[Authorize(Roles = RoleConstants.Trader)]
public async Task<IActionResult> CancelOrder(
    long id,
    CancellationToken cancellationToken)
    {
        var currentUser = await _userManager.GetUserAsync(User);

        if (currentUser == null || !currentUser.IsActive)
            return Unauthorized();

        if (!currentUser.FirmId.HasValue)
            return BadRequest("Trader is not associated with a firm.");

        if (string.IsNullOrWhiteSpace(currentUser.CHSEWorkstationId))
            return BadRequest(
                "CHSE workstation is not configured for this trader.");

        try
        {
            var cancellationRequest =
                await _orderCancellationService.CancelOrderAsync(
                    id,
                    currentUser.FirmId.Value,
                    currentUser.CHSEWorkstationId,
                    cancellationToken);

            return Ok(new
            {
                cancellationRequest.Id,
                cancellationRequest.TradingOrderId,
                cancellationRequest.SessionId,
                cancellationRequest.ClOrdId,
                cancellationRequest.OrigClOrdId,
                cancellationRequest.Status,
                cancellationRequest.SubmissionError,
                cancellationRequest.CreatedAtUtc,
                cancellationRequest.UpdatedAtUtc
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { Message = ex.Message });
        }

        
    }


    // POST: api/orders/{id}/replace
    [HttpPost("{id:long}/replace")]
    [Authorize(Roles = RoleConstants.Trader)]
    public async Task<IActionResult> ReplaceOrder(
        long id,
        ReplaceOrderDto dto,
        CancellationToken cancellationToken)
    {
        var currentUser = await _userManager.GetUserAsync(User);

        if (currentUser == null || !currentUser.IsActive)
            return Unauthorized();

        if (!currentUser.FirmId.HasValue)
            return BadRequest("Trader is not associated with a firm.");

        if (string.IsNullOrWhiteSpace(currentUser.CHSEWorkstationId))
            return BadRequest(
                "CHSE workstation is not configured for this trader.");

        try
        {
            var replacementRequest =
                await _orderReplacementService.ReplaceOrderAsync(
                    id,
                    dto,
                    currentUser.FirmId.Value,
                    currentUser.CHSEWorkstationId,
                    cancellationToken);

            return Ok(new
            {
                replacementRequest.Id,
                replacementRequest.TradingOrderId,
                replacementRequest.SessionId,
                replacementRequest.ClOrdId,
                replacementRequest.OrigClOrdId,
                replacementRequest.OrderQty,
                replacementRequest.Price,
                replacementRequest.StopPx,
                replacementRequest.TimeInForce,
                replacementRequest.ExpireDate,
                replacementRequest.Status,
                replacementRequest.SubmissionError,
                replacementRequest.CreatedAtUtc,
                replacementRequest.UpdatedAtUtc
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { Message = ex.Message });
        }
    }

    // PUT: api/orders/1
    [HttpPut("{id:long}")]
    public IActionResult UpdateOrder(long id)
    {
        return StatusCode(
            StatusCodes.Status405MethodNotAllowed,
            new
            {
                Message =
                    "Direct order updates are disabled. " +
                    "Use the order replacement endpoint for supported order changes. " +
                    "Exchange-managed order fields are updated through FIX responses."
            });
    }

    // DELETE: api/orders/1
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteOrder(long id)
    {
        var firmId = await GetCurrentFirmIdAsync();

        if (firmId == null)
            return Forbid();

        var order = await _dbContext.Orders
            .Include(x => x.Executions)
            .FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId);

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

    private async Task<int?> GetCurrentFirmIdAsync()
    {
        var user = await _userManager.GetUserAsync(User);

        return user?.FirmId;
    }
}