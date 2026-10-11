using SubiektNexoConnector.Core.Materialization;

namespace SubiektNexoConnector.Core.Application.Products
{
    public sealed class GetProductDetailsHandler
    {
        private readonly IProductReadModelReader _reader;
        private readonly IProductMaterializationRefresher _refresher;
        private readonly IProductFreshnessPolicy _freshnessPolicy;

        public GetProductDetailsHandler(IProductReadModelReader reader, IProductMaterializationRefresher refresher, IProductFreshnessPolicy freshnessPolicy)
        {
            _reader = reader;
            _refresher = refresher;
            _freshnessPolicy = freshnessPolicy;
        }

        public async Task<MaterializedResult<ProductDetailsDto?>> HandleAsync(GetProductDetailsQuery query, CancellationToken cancellationToken = default)
        {
            var materializedProduct = await _reader.GetDetailsAsync(query.ProductSymbol, cancellationToken);
            if (materializedProduct is not null)
            {
                var metadata = await _freshnessPolicy.GetMetadataAsync(materializedProduct.Product, materializedProduct.RefreshedAtUtc, cancellationToken);
                if (metadata.Status is MaterializationStatus.Fresh or MaterializationStatus.RefreshDue)
                    return new MaterializedResult<ProductDetailsDto?>(materializedProduct.Product, metadata);
            }

            try
            {
                await _refresher.RefreshOneAsync(query.ProductSymbol, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch when (materializedProduct is not null)
            {
                return new MaterializedResult<ProductDetailsDto?>(materializedProduct.Product, new MaterializationMetadata(MaterializationStatus.Stale, materializedProduct.RefreshedAtUtc));
            }

            var refreshedProduct = await _reader.GetDetailsAsync(query.ProductSymbol, cancellationToken);
            return refreshedProduct is null
                ? new MaterializedResult<ProductDetailsDto?>(null, new MaterializationMetadata(MaterializationStatus.Fresh, DateTime.UtcNow))
                : new MaterializedResult<ProductDetailsDto?>(refreshedProduct.Product, new MaterializationMetadata(MaterializationStatus.Fresh, refreshedProduct.RefreshedAtUtc));
        }
    }
}
