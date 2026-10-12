using NSubstitute;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Core.Application.Common;
using SubiektNexoConnector.Core.Application.Products;

namespace SubiektNexoConnector.Core.Tests.Application.Products;

public class PatchProductHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsUpdatedProductAndUpdatesReadModel()
    {
        var repository = Substitute.For<IProductRepository>();
        var synchronizer = Substitute.For<IProductReadModelSynchronizer>();
        var command = new PatchProductCommand("PROD-001", new Optional<string>("Updated product"), default, default);
        var product = CreateProduct() with { Name = "Updated product" };
        repository.Patch(Arg.Any<PatchProductCommand>()).Returns(product);
        synchronizer.TryUpsertAsync(product, Arg.Any<CancellationToken>()).Returns(true);
        var handler = new PatchProductHandler(repository, synchronizer);

        var result = await handler.HandleAsync(command);

        Assert.Equal(product, result);
        repository.Received(1).Patch(Arg.Is<PatchProductCommand>(patchedCommand =>
            patchedCommand.ProductSku == "PROD-001"
            && patchedCommand.Name.HasValue
            && patchedCommand.Name.Value == "Updated product"
            && !patchedCommand.SKU.HasValue
            && !patchedCommand.EAN.HasValue));
        await synchronizer.Received(1).TryUpsertAsync(product, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ReturnsNullWithoutUpdatingReadModel_WhenProductDoesNotExist()
    {
        var repository = Substitute.For<IProductRepository>();
        var synchronizer = Substitute.For<IProductReadModelSynchronizer>();
        var command = new PatchProductCommand("MISSING", default, new Optional<string>("NEW-SKU"), default);
        repository.Patch(Arg.Any<PatchProductCommand>()).Returns((ProductDetailsDto?)null);
        var handler = new PatchProductHandler(repository, synchronizer);

        var result = await handler.HandleAsync(command);

        Assert.Null(result);
        repository.Received(1).Patch(Arg.Any<PatchProductCommand>());
        await synchronizer.DidNotReceive().TryUpsertAsync(Arg.Any<ProductDetailsDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenNoFieldWasProvided()
    {
        var repository = Substitute.For<IProductRepository>();
        var handler = new PatchProductHandler(repository, Substitute.For<IProductReadModelSynchronizer>());
        var command = new PatchProductCommand("PROD-001", default, default, default);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(command));

        Assert.Equal("At least one field must be provided.", exception.Message);
        repository.DidNotReceive().Patch(Arg.Any<PatchProductCommand>());
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenNameWasProvidedAsNull()
    {
        var repository = Substitute.For<IProductRepository>();
        var handler = new PatchProductHandler(repository, Substitute.For<IProductReadModelSynchronizer>());
        var command = new PatchProductCommand("PROD-001", new Optional<string>(null), default, default);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(command));

        Assert.Equal("Name cannot be null.", exception.Message);
        repository.DidNotReceive().Patch(Arg.Any<PatchProductCommand>());
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenSkuWasProvidedAsWhitespace()
    {
        var repository = Substitute.For<IProductRepository>();
        var handler = new PatchProductHandler(repository, Substitute.For<IProductReadModelSynchronizer>());
        var command = new PatchProductCommand("PROD-001", default, new Optional<string>("   "), default);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(command));

        Assert.Equal("SKU cannot be empty.", exception.Message);
        repository.DidNotReceive().Patch(Arg.Any<PatchProductCommand>());
    }

    [Fact]
    public async Task HandleAsync_NormalizesNullEan_WhenWhitespaceWasProvided()
    {
        var repository = Substitute.For<IProductRepository>();
        var command = new PatchProductCommand("PROD-001", default, default, new Optional<string?>("   "));
        repository.Patch(Arg.Any<PatchProductCommand>()).Returns(CreateProduct());
        var handler = new PatchProductHandler(repository, Substitute.For<IProductReadModelSynchronizer>());

        await handler.HandleAsync(command);

        repository.Received(1).Patch(Arg.Is<PatchProductCommand>(patchedCommand => patchedCommand.EAN.HasValue && patchedCommand.EAN.Value == null));
    }

    [Fact]
    public async Task HandleAsync_ForwardsBasicAndAdvancedFieldUpdates()
    {
        var repository = Substitute.For<IProductRepository>();
        var basicFields = new AdditionalFieldValueDto[] { new("PoleWlasne1", "120") };
        var advancedFields = new AdditionalFieldValueDto[] { new("weight", 42.5m) };
        var command = new PatchProductCommand("PROD-001", default, default, default, new Optional<IReadOnlyCollection<AdditionalFieldValueDto>>(basicFields), new Optional<IReadOnlyCollection<AdditionalFieldValueDto>>(advancedFields));
        repository.Patch(Arg.Any<PatchProductCommand>()).Returns(CreateProduct());
        var handler = new PatchProductHandler(repository, Substitute.For<IProductReadModelSynchronizer>());

        await handler.HandleAsync(command);

        repository.Received(1).Patch(Arg.Is<PatchProductCommand>(patchedCommand =>
            patchedCommand.BasicFields.HasValue
            && patchedCommand.BasicFields.Value!.Single().Id == "PoleWlasne1"
            && Equals(patchedCommand.BasicFields.Value!.Single().Value, "120")
            && patchedCommand.AdvancedFields.HasValue
            && patchedCommand.AdvancedFields.Value!.Single().Id == "weight"
            && (decimal)patchedCommand.AdvancedFields.Value!.Single().Value! == 42.5m));
    }

    [Fact]
    public async Task HandleAsync_ForwardsNormalizedFlagAssignment()
    {
        var repository = Substitute.For<IProductRepository>();
        var command = new PatchProductCommand("PROD-001", default, default, default, default, default, new Optional<FlagAssignmentDto?>(new FlagAssignmentDto(12, "  Require confirmation  ")));
        repository.Patch(Arg.Any<PatchProductCommand>()).Returns(CreateProduct());
        var handler = new PatchProductHandler(repository, Substitute.For<IProductReadModelSynchronizer>());

        await handler.HandleAsync(command);

        repository.Received(1).Patch(Arg.Is<PatchProductCommand>(patchedCommand => patchedCommand.Flag.HasValue && patchedCommand.Flag.Value!.Id == 12 && patchedCommand.Flag.Value.Comment == "Require confirmation"));
    }

    [Fact]
    public async Task HandleAsync_ForwardsNullFlagToRemoveAssignment()
    {
        var repository = Substitute.For<IProductRepository>();
        var command = new PatchProductCommand("PROD-001", default, default, default, default, default, new Optional<FlagAssignmentDto?>(null));
        repository.Patch(Arg.Any<PatchProductCommand>()).Returns(CreateProduct());
        var handler = new PatchProductHandler(repository, Substitute.For<IProductReadModelSynchronizer>());

        await handler.HandleAsync(command);

        repository.Received(1).Patch(Arg.Is<PatchProductCommand>(patchedCommand => patchedCommand.Flag.HasValue && patchedCommand.Flag.Value == null));
    }

    private static ProductDetailsDto CreateProduct() => new(1, "PROD-001", "Test product", null, new ProductTypeDto("GOODS", "Goods"), true, null, null, [], [], [], [], []);
}
