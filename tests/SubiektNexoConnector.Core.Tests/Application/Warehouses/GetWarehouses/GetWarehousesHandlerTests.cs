using NSubstitute;
using SubiektNexoConnector.Core.Application.Warehouses;

namespace SubiektNexoConnector.Core.Tests.Application.Warehouses
{
    public class GetWarehousesHandlerTests
    {
        [Fact]
        public void Handle_ReturnsWarehousesFromRepository()
        {
            var repository = Substitute.For<IWarehouseRepository>();
            var expectedWarehouses = new List<WarehouseDto>
            {
                new WarehouseDto("MAIN", "Main warehouse"),
                new WarehouseDto("OUTLET", "Outlet warehouse")
            };
            repository.GetAll().Returns(expectedWarehouses);
            var handler = new GetWarehousesHandler(repository);
            var query = new GetWarehousesQuery();

            var result = handler.Handle(query);

            Assert.Equal(expectedWarehouses, result);
            repository.Received(1).GetAll();
        }

        [Fact]
        public void Handle_ReturnsEmptyCollection_WhenRepositoryReturnsNoWarehouses()
        {
            var repository = Substitute.For<IWarehouseRepository>();
            repository.GetAll().Returns(new List<WarehouseDto>());
            var handler = new GetWarehousesHandler(repository);
            var query = new GetWarehousesQuery();

            var result = handler.Handle(query);

            Assert.Empty(result);
            repository.Received(1).GetAll();
        }
    }
}
