using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using SubiektNexoConnector.Core.Application.Products;

namespace SubiektNexoConnector.Api.Controllers;

[ApiController]
[Route("/[controller]")]
[Produces(MediaTypeNames.Application.Json)]
[Tags("Products")]
public class ProductsController : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(CreateProductResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public ActionResult<CreateProductResponseDto> Create(
        [FromBody] CreateProductRequestDto request,
        [FromServices] CreateProductHandler handler)
    {
        var command = new CreateProductCommand(
            request.Name,
            request.SKU,
            request.EAN
        );

        var sku = handler.Handle(command);

        return CreatedAtAction(
            nameof(GetDetails),
            new { sku },
            new CreateProductResponseDto(sku));
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<ProductBasicDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyCollection<ProductBasicDto>> GetAll(
        [FromServices] GetProductsHandler handler)
    {
        var result = handler.Handle(new GetProductsQuery());
        return Ok(result);
    }

    [HttpGet("{sku}")]
    [ProducesResponseType(typeof(ProductDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ProductDetailsDto> GetDetails(
        string sku,
        [FromServices] GetProductDetailsHandler handler)
    {
        var result = handler.Handle(new GetProductDetailsQuery(sku));

        if (result is null)
            return NotFound();

        return Ok(result);
    }
}
