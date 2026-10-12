
using SubiektNexoConnector.Core.Materialization;

namespace SubiektNexoConnector.Core.Application.Products
{
    public sealed class GetProductsHandler
    {
        private readonly IProductReadModelReader _productReadModelReader;
        private readonly IMaterializationCheckpointStore _materializationCheckpointStore;
        private readonly IMaterializationPolicyStore _materializationPolicyStore;
        public GetProductsHandler(IProductReadModelReader productReadModelReader, IMaterializationCheckpointStore materializationCheckpointStore, IMaterializationPolicyStore materializationPolicyStore)
        {
            _productReadModelReader = productReadModelReader;
            _materializationCheckpointStore = materializationCheckpointStore;
            _materializationPolicyStore = materializationPolicyStore;
        }

        public async Task<MaterializedResult<IReadOnlyCollection<ProductBasicDto>?>> HandleAsync(GetProductsQuery query, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);

            var definition = MaterializationPolicyDefinitions.ProductsNew;
            var checkpoint = await _materializationCheckpointStore.GetOrCreateAsync(definition.Id, cancellationToken);

            if (checkpoint.LastCycleCompletedAtUtc is null)
                return new MaterializedResult<IReadOnlyCollection<ProductBasicDto>?>(null, new MaterializationMetadata(MaterializationStatus.Unavailable, null));

            var policy = await _materializationPolicyStore.GetOrCreateAsync(definition, cancellationToken);
            var metadata = MaterializationMetadata.Evaluate(checkpoint.LastCycleCompletedAtUtc, policy.Lifetime, policy.RefreshAhead, DateTime.UtcNow);
            var products = await _productReadModelReader.GetAllAsync(query, cancellationToken);
            return new MaterializedResult<IReadOnlyCollection<ProductBasicDto>?>(products, metadata);
        }
    }
}
