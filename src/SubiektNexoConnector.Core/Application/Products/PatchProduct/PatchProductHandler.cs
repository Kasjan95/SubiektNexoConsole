using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Core.Application.Common;

namespace SubiektNexoConnector.Core.Application.Products
{
    public sealed class PatchProductHandler
    {
        private readonly IProductRepository _repository;
        private readonly IProductReadModelSynchronizer _readModelSynchronizer;

        public PatchProductHandler(IProductRepository repository, IProductReadModelSynchronizer readModelSynchronizer)
        {
            _repository = repository;
            _readModelSynchronizer = readModelSynchronizer;
        }

        public async Task<ProductDetailsDto?> HandleAsync(PatchProductCommand command, CancellationToken cancellationToken = default)
        {
            if (!command.Name.HasValue
                && !command.SKU.HasValue
                && !command.EAN.HasValue
                && !command.BasicFields.HasValue
                && !command.AdvancedFields.HasValue
                && !command.Flag.HasValue)
                throw new InvalidOperationException("At least one field must be provided.");

            var normalizedName = OptionalPatchNormalizer.RequiredText(command.Name, "Name");
            var normalizedSku = OptionalPatchNormalizer.RequiredText(command.SKU, "SKU");
            var normalizedEan = OptionalPatchNormalizer.OptionalText(command.EAN);
            var basicFields = OptionalPatchNormalizer.AdditionalFields(command.BasicFields, "BasicFields");
            var advancedFields = OptionalPatchNormalizer.AdditionalFields(command.AdvancedFields, "AdvancedFields");
            var flag = OptionalPatchNormalizer.Flag(command.Flag);

            var product = _repository.Patch(new PatchProductCommand(
                command.ProductSku,
                normalizedName,
                normalizedSku,
                normalizedEan,
                basicFields,
                advancedFields,
                flag));

            if (product is null)
                return null;

            await _readModelSynchronizer.TryUpsertAsync(product, cancellationToken);
            return product;
        }

    }
}
