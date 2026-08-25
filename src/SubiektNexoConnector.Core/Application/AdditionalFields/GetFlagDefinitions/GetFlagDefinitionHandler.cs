using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;

namespace SubiektNexoConnector.Core.Application.AdditionalFields.GetFlagDefinitions
{
    public sealed class GetFlagDefinitionHandler
    {
        private readonly IAdditionalFieldDefinitionRepository _repository;
        public GetFlagDefinitionHandler(IAdditionalFieldDefinitionRepository repository)
        {
            _repository = repository;
        }
        public FlagDefinitionsDto Handle(GetFlagDefinitionQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);

            return _repository.GetFlagDefinitions(query);
        }
    }
}
