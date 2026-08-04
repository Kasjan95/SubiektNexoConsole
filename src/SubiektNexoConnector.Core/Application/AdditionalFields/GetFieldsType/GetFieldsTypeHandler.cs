using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;

namespace SubiektNexoConnector.Core.Application.AdditionalFields.GetFieldsType
{
    public sealed class GetFieldsTypeHandler
    {
        private readonly IAdditionalFieldRepository _repository;

        public GetFieldsTypeHandler(IAdditionalFieldRepository repository)
        {
            _repository = repository;
        }

        public AdditionalFieldsDefinitionDto Handle(GetFieldsTypeQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);

            return _repository.GetFieldsType(query);
        }
    }
}
