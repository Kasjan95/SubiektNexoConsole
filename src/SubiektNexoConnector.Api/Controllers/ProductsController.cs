using Microsoft.AspNetCore.Mvc;
using SubiektNexoConnector.Core.Application.Products;

namespace SubiektNexoConnector.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<ProductBasicDto>> GetAll(
        [FromServices] GetProductsHandler handler)
    {
        var result = handler.Handle(new GetProductsQuery());
        return Ok(result);
    }

    [HttpGet("by-symbol/{symbol}")]
    public ActionResult<ProductDetailsDto> GetDetails(
        String symbol,
        [FromQuery] String warehouseSymbol,
        [FromServices] GetProductDetailsHandler handler)
    {
        var result = handler.Handle(new GetProductDetailsQuery(symbol, warehouseSymbol));

        if (result is null)
            return NotFound();

        return Ok(result);
    }
}