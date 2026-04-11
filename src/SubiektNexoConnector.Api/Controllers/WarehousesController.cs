using Microsoft.AspNetCore.Mvc;
using SubiektNexoConnector.Core.Application.Warehouses;
using SubiektNexoConnector.Core.Application.Products;

namespace SubiektNexoConnector.Api.Controllers;

[ApiController]
[Route("/[controller]")]
[Tags("Warehouses")]
public class WarehousesController : ControllerBase
{

    [HttpGet()]
    public ActionResult<WarehouseDto> GetDetails(
        [FromServices] GetWarehousesHandler handler)
    {
        var result = handler.Handle(new GetWarehousesQuery());

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpGet("/[controller]/{symbol}/products/{sku}")]
    public ActionResult<ProductFromWarehouseDto> GetDetails(
        String symbol,
        String sku,
        [FromServices] GetProductFromWarehouseHandler handler)
    {
        var result = handler.Handle(new GetProductFromWarehouseQuery(sku, symbol));

        if (result is null)
            return NotFound();

        return Ok(result);
    }
}
