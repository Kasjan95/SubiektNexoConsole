
using SubiektNexoConnector.Core.Materialization;

namespace SubiektNexoConnector.Core.Application.Products
{
    public sealed class GetProductsHandler
    {
        private readonly IProductReadModelReader _productReadModelReader;
        private readonly IMaterializationCheckpointStore _materializationCheckpointStore;
        private readonly IMaterializationFreshnessEvaluator _freshnessEvaluator;
        public GetProductsHandler(IProductReadModelReader productReadModelReader, IMaterializationCheckpointStore materializationCheckpointStore, IMaterializationFreshnessEvaluator freshnessEvaluator)
        {
            _productReadModelReader = productReadModelReader;
            _materializationCheckpointStore = materializationCheckpointStore;
            _freshnessEvaluator = freshnessEvaluator;
        }

        public async Task<MaterializedResult<IReadOnlyCollection<ProductBasicDto>?>> HandleAsync(GetProductsQuery query, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);

            var definition = MaterializationPolicyDefinitions.ProductsNew;
            var checkpoint = await _materializationCheckpointStore.GetOrCreateAsync(definition.Id, cancellationToken);

            if (checkpoint.LastCycleCompletedAtUtc is null)
                return new MaterializedResult<IReadOnlyCollection<ProductBasicDto>?>(null, new MaterializationMetadata(MaterializationStatus.Unavailable, null));

            var metadata = await _freshnessEvaluator.EvaluateAsync(definition, checkpoint.LastCycleCompletedAtUtc, cancellationToken);
            var products = await _productReadModelReader.GetAllAsync(query, cancellationToken);
            return new MaterializedResult<IReadOnlyCollection<ProductBasicDto>?>(products, metadata);
        }
    }
}
