using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;

namespace SubiektNexoConnector.Core.Application.AdditionalFields.GetBasicFieldDefinitions;

public sealed class GetBasicFieldDefinitionsHandler
{
    private readonly IAdditionalFieldDefinitionRepository _repository;

    public GetBasicFieldDefinitionsHandler(IAdditionalFieldDefinitionRepository repository)
    {
        _repository = repository;
    }

    public BasicFieldDefinitionsDto Handle(GetBasicFieldDefinitionsQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return _repository.GetBasicFieldDefinitions(query);
    }
}
