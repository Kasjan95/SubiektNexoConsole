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
        public async Task<ProductDetailsDto?> HandleAsync(GetProductDetailsQuery query, CancellationToken cancellationToken = default)
        {
            var materializedProduct = await _reader.GetDetailsAsync(query.ProductSymbol, cancellationToken);
            if (materializedProduct is not null)
            {
                var refreshDueAtUtc = await _freshnessPolicy.GetRefreshDueAtUtcAsync(materializedProduct.Product, materializedProduct.RefreshedAtUtc, cancellationToken);
                if (refreshDueAtUtc > DateTime.UtcNow)
                    return materializedProduct.Product;
            }
            await _refresher.RefreshOneAsync(query.ProductSymbol, cancellationToken);
            return (await _reader.GetDetailsAsync(query.ProductSymbol, cancellationToken))?.Product;
        }
    }
}
