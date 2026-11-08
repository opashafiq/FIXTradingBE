using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradingBackend.Api.Data;

namespace TradingBackend.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DatabaseController : ControllerBase
{
    private readonly TradingDbContext _dbContext;

    public DatabaseController(TradingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("health")]
    public async Task<IActionResult> Health()
    {
        try
        {
            bool canConnect = await _dbContext.Database.CanConnectAsync();

            if (!canConnect)
            {
                return StatusCode(503, new
                {
                    Connected = false,
                    Message = "Unable to connect to PostgreSQL."
                });
            }

            return Ok(new
            {
                Connected = true,
                Database = "PostgreSQL",
                Message = "Database connection successful."
            });
        }
        catch (Exception ex)
        {
            return StatusCode(503, new
            {
                Connected = false,
                Message = ex.Message
            });
        }
    }
}