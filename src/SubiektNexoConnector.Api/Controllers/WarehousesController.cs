using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using SubiektNexoConnector.Core.Application.Warehouses;
using SubiektNexoConnector.Core.Application.Products;

namespace SubiektNexoConnector.Api.Controllers;

[ApiController]
[Route("/[controller]")]
[Produces(MediaTypeNames.Application.Json)]
[Tags("Warehouses")]
public class WarehousesController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<WarehouseDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyCollection<WarehouseDto>> GetAll(
        [FromServices] GetWarehousesHandler handler)
    {
        var result = handler.Handle(new GetWarehousesQuery());

        return Ok(result);
    }

    [HttpGet("{symbol}/products/{sku}")]
    [ProducesResponseType(typeof(ProductFromWarehouseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ProductFromWarehouseDto> GetDetails(
        string symbol,
        string sku,
        [FromServices] GetProductFromWarehouseHandler handler)
    {
        var result = handler.Handle(new GetProductFromWarehouseQuery(sku, symbol));

        if (result is null)
            return NotFound();

        return Ok(result);
    }
}
