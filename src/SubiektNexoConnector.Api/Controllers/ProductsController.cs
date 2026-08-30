using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using SubiektNexoConnector.Api.Configuration;
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
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public ActionResult<IReadOnlyCollection<ProductBasicDto>> GetAll(
        [FromServices] GetProductsHandler handler,
        [FromQuery] string? search = null,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromServices] PaginationOptions? paginationOptions = null)
    {
        var pagination = paginationOptions ?? new PaginationOptions();
        if (!pagination.TryResolve(page, pageSize, out var parameters, out var errors))
            return ValidationProblem(new ValidationProblemDetails(errors));

        var result = handler.Handle(new GetProductsQuery
        {
            Search = search,
            Page = parameters.Page,
            PageSize = parameters.PageSize
        });
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

    [HttpPatch("{sku}")]
    [ProducesResponseType(typeof(PatchProductResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<PatchProductResponseDto> Patch(
        string sku,
        [FromBody] PatchProductRequestDto request,
        [FromServices] PatchProductHandler handler)
    {
        var command = new PatchProductCommand(
            sku,
            request.Name,
            request.SKU,
            request.EAN,
            request.BasicFields,
            request.AdvancedFields,
            request.Flag);

        var updatedSku = handler.Handle(command);
        if (updatedSku is null)
            return NotFound();

        return Ok(new PatchProductResponseDto(updatedSku));
    }

    [HttpDelete("{sku}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public IActionResult Delete(
        string sku,
        [FromServices] DeleteProductHandler handler)
    {
        var result = handler.Handle(new DeleteProductCommand(sku));

        if (result == DeleteProductResult.NotFound)
            return NotFound();
        if (result == DeleteProductResult.Blocked)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Product cannot be deleted",
                Detail = "Product was used in documents and cannot be removed."
            });
        }

        return NoContent();
    }
}
