using NSubstitute;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Core.Application.Common;
using SubiektNexoConnector.Core.Application.Products;

namespace SubiektNexoConnector.Core.Tests.Application.Products
{
    public class PatchProductHandlerTests
    {
        [Fact]
        public void Handle_ReturnsUpdatedSkuFromRepository()
        {
            var repository = Substitute.For<IProductRepository>();
            var command = new PatchProductCommand(
                "PROD-001",
                new Optional<string>("Updated product"),
                default,
                default);
            repository.Patch(Arg.Any<PatchProductCommand>()).Returns("PROD-001");
            var handler = new PatchProductHandler(repository);

            var result = handler.Handle(command);

            Assert.Equal("PROD-001", result);
            repository.Received(1).Patch(Arg.Is<PatchProductCommand>(patchedCommand =>
                patchedCommand.ProductSku == "PROD-001"
                && patchedCommand.Name.HasValue
                && patchedCommand.Name.Value == "Updated product"
                && !patchedCommand.SKU.HasValue
                && !patchedCommand.EAN.HasValue));
        }

        [Fact]
        public void Handle_ReturnsNull_WhenProductDoesNotExist()
        {
            var repository = Substitute.For<IProductRepository>();
            var command = new PatchProductCommand(
                "MISSING",
                default,
                new Optional<string>("NEW-SKU"),
                default);
            repository.Patch(Arg.Any<PatchProductCommand>()).Returns((string?)null);
            var handler = new PatchProductHandler(repository);

            var result = handler.Handle(command);

            Assert.Null(result);
            repository.Received(1).Patch(Arg.Any<PatchProductCommand>());
        }

        [Fact]
        public void Handle_Throws_WhenNoFieldWasProvided()
        {
            var repository = Substitute.For<IProductRepository>();
            var command = new PatchProductCommand("PROD-001", default, default, default);
            var handler = new PatchProductHandler(repository);

            var exception = Assert.Throws<InvalidOperationException>(() => handler.Handle(command));

            Assert.Equal("At least one field must be provided.", exception.Message);
            repository.DidNotReceive().Patch(Arg.Any<PatchProductCommand>());
        }

        [Fact]
        public void Handle_Throws_WhenNameWasProvidedAsNull()
        {
            var repository = Substitute.For<IProductRepository>();
            var command = new PatchProductCommand(
                "PROD-001",
                new Optional<string>(null),
                default,
                default);
            var handler = new PatchProductHandler(repository);

            var exception = Assert.Throws<InvalidOperationException>(() => handler.Handle(command));

            Assert.Equal("Name cannot be null.", exception.Message);
            repository.DidNotReceive().Patch(Arg.Any<PatchProductCommand>());
        }

        [Fact]
        public void Handle_Throws_WhenSkuWasProvidedAsWhitespace()
        {
            var repository = Substitute.For<IProductRepository>();
            var command = new PatchProductCommand(
                "PROD-001",
                default,
                new Optional<string>("   "),
                default);
            var handler = new PatchProductHandler(repository);

            var exception = Assert.Throws<InvalidOperationException>(() => handler.Handle(command));

            Assert.Equal("SKU cannot be empty.", exception.Message);
            repository.DidNotReceive().Patch(Arg.Any<PatchProductCommand>());
        }

        [Fact]
        public void Handle_NormalizesNullEan_WhenWhitespaceWasProvided()
        {
            var repository = Substitute.For<IProductRepository>();
            var command = new PatchProductCommand(
                "PROD-001",
                default,
                default,
                new Optional<string?>("   "));
            repository.Patch(Arg.Any<PatchProductCommand>()).Returns("PROD-001");
            var handler = new PatchProductHandler(repository);

            handler.Handle(command);

            repository.Received(1).Patch(Arg.Is<PatchProductCommand>(patchedCommand =>
                patchedCommand.EAN.HasValue
                && patchedCommand.EAN.Value == null));
        }

        [Fact]
        public void Handle_ForwardsBasicAndAdvancedFieldUpdates()
        {
            var repository = Substitute.For<IProductRepository>();
            var basicFields = new AdditionalFieldValueDto[]
            {
                new("PoleWlasne1", "120")
            };
            var advancedFields = new AdditionalFieldValueDto[]
            {
                new("weight", 42.5m)
            };
            var command = new PatchProductCommand(
                "PROD-001",
                default,
                default,
                default,
                new Optional<IReadOnlyCollection<AdditionalFieldValueDto>>(basicFields),
                new Optional<IReadOnlyCollection<AdditionalFieldValueDto>>(advancedFields));
            repository.Patch(Arg.Any<PatchProductCommand>()).Returns("PROD-001");

            new PatchProductHandler(repository).Handle(command);

            repository.Received(1).Patch(Arg.Is<PatchProductCommand>(patchedCommand =>
                patchedCommand.BasicFields.HasValue
                && patchedCommand.BasicFields.Value!.Single().Id == "PoleWlasne1"
                && Equals(patchedCommand.BasicFields.Value!.Single().Value, "120")
                && patchedCommand.AdvancedFields.HasValue
                && patchedCommand.AdvancedFields.Value!.Single().Id == "weight"
                && (decimal)patchedCommand.AdvancedFields.Value!.Single().Value! == 42.5m));
        }

        [Fact]
        public void Handle_ForwardsNormalizedFlagAssignment()
        {
            var repository = Substitute.For<IProductRepository>();
            var command = new PatchProductCommand(
                "PROD-001",
                default,
                default,
                default,
                default,
                default,
                new Optional<FlagAssignmentDto?>(new FlagAssignmentDto(12, "  Require confirmation  ")));
            repository.Patch(Arg.Any<PatchProductCommand>()).Returns("PROD-001");

            new PatchProductHandler(repository).Handle(command);

            repository.Received(1).Patch(Arg.Is<PatchProductCommand>(patchedCommand =>
                patchedCommand.Flag.HasValue
                && patchedCommand.Flag.Value!.Id == 12
                && patchedCommand.Flag.Value.Comment == "Require confirmation"));
        }

        [Fact]
        public void Handle_ForwardsNullFlagToRemoveAssignment()
        {
            var repository = Substitute.For<IProductRepository>();
            var command = new PatchProductCommand(
                "PROD-001",
                default,
                default,
                default,
                default,
                default,
                new Optional<FlagAssignmentDto?>(null));
            repository.Patch(Arg.Any<PatchProductCommand>()).Returns("PROD-001");

            new PatchProductHandler(repository).Handle(command);

            repository.Received(1).Patch(Arg.Is<PatchProductCommand>(patchedCommand =>
                patchedCommand.Flag.HasValue
                && patchedCommand.Flag.Value == null));
        }
    }
}
