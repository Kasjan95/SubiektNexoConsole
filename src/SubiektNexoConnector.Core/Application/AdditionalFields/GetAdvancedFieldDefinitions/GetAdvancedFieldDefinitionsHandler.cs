using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;

namespace SubiektNexoConnector.Core.Application.AdditionalFields.GetAdvancedFieldDefinitions;

public sealed class GetAdvancedFieldDefinitionsHandler
{
    private readonly IAdditionalFieldDefinitionRepository _repository;

    public GetAdvancedFieldDefinitionsHandler(IAdditionalFieldDefinitionRepository repository)
    {
        _repository = repository;
    }

    public AdvancedFieldDefinitionsDto Handle(GetAdvancedFieldDefinitionsQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return _repository.GetAdvancedFieldDefinitions(query);
    }
}
