
namespace SubiektNexoConnector.Core.Application.Products
{
    public interface IProductRepository
    {
        IReadOnlyCollection<ProductBasicDto> GetAll(GetProductsQuery querry);
        ProductFromWarehouseDto? GetDetailsFromWarehouse(string warehouseSymbol, string productSymbol);
        ProductDetailsDto? GetDetails(
            string ProductSymbol,
            CancellationToken cancellationToken = default);
        IReadOnlyCollection<ProductDetailsDto> GetDetailsBatch(
            IReadOnlyCollection<string> productSymbols,
            CancellationToken cancellationToken = default);

        string Create(CreateProductCommand command);
        string? Patch(PatchProductCommand command);
        DeleteProductResult Delete(DeleteProductCommand command);
        IReadOnlyCollection<ProductMaterializationKey> GetAllMaterializationCandidates(
            CancellationToken cancellationToken = default);
        IReadOnlyCollection<ProductBasicDto> GetBatchAfterId(
            int? afterId,
            int batchSize,
            CancellationToken cancellationToken = default);
    }
}
