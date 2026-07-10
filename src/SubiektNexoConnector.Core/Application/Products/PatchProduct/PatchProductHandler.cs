using SubiektNexoConnector.Core.Application.Common;

namespace SubiektNexoConnector.Core.Application.Products
{
    public sealed class PatchProductHandler
    {
        private readonly IProductRepository _repository;

        public PatchProductHandler(IProductRepository repository)
        {
            _repository = repository;
        }

        public string? Handle(PatchProductCommand command)
        {
            if (!command.Name.HasValue && !command.SKU.HasValue && !command.EAN.HasValue)
                throw new InvalidOperationException("At least one field must be provided.");

            var normalizedName = NormalizeRequiredField(command.Name, "Name");
            var normalizedSku = NormalizeRequiredField(command.SKU, "SKU");
            var normalizedEan = NormalizeOptionalEan(command.EAN);

            return _repository.Patch(new PatchProductCommand(
                command.ProductSku,
                normalizedName,
                normalizedSku,
                normalizedEan));
        }

        private static Optional<string> NormalizeRequiredField(Optional<string> field, string fieldName)
        {
            if (!field.HasValue)
                return field;

            if (field.Value is null)
                throw new InvalidOperationException($"{fieldName} cannot be null.");

            var normalizedValue = field.Value.Trim();
            if (normalizedValue.Length == 0)
                throw new InvalidOperationException($"{fieldName} cannot be empty.");

            return normalizedValue;
        }

        private static Optional<string?> NormalizeOptionalEan(Optional<string?> field)
        {
            if (!field.HasValue)
                return field;

            if (string.IsNullOrWhiteSpace(field.Value))
                return new Optional<string?>(null);

            return field.Value.Trim();
        }
    }
}
