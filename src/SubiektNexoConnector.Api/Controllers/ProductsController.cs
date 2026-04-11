using Microsoft.AspNetCore.Mvc;
using SubiektNexoConnector.Core.Application.Products;

namespace SubiektNexoConnector.Api.Controllers;

[ApiController]
[Route("/[controller]")]
[Tags("Products")]
public class ProductsController : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<ProductBasicDto>> GetAll(
        [FromServices] GetProductsHandler handler)
    {
        var result = handler.Handle(new GetProductsQuery());
        return Ok(result);
    }

    [HttpGet("{sku}")]
    public ActionResult<ProductDetailsDto> GetDetails(
        String sku,
        [FromServices] GetProductDetailsHandler handler)
    {
        var result = handler.Handle(new GetProductDetailsQuery(sku));

        if (result is null)
            return NotFound();

        return Ok(result);
    }
}
