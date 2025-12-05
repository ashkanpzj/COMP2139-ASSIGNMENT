using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Assignment1.Models;
using Assignment1.Services;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Assignment1.Tests;

public class CartServiceTests
{
    private CartService CreateService(ISession session)
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext { Session = session });
        return new CartService(accessor.Object);
    }

    [Fact]
    public void AddToCart_Adds_New_Item_And_Increments_Count()
    {
        var session = new TestSession();
        var service = CreateService(session);

        service.AddToCart(new CartItem
        {
            EventId = 1,
            EventTitle = "Event A",
            UnitPrice = 10m,
            Quantity = 2,
            AvailableTickets = 10
        });

        var cart = service.GetCart();
        Assert.Single(cart.Items);
        Assert.Equal(2, cart.Items[0].Quantity);
        Assert.Equal(20m, cart.Items[0].TotalPrice);
        Assert.Equal(2, service.GetCartItemCount());
    }

    [Fact]
    public void AddToCart_Same_Item_Increments_Quantity_Respects_Available()
    {
        var session = new TestSession();
        var service = CreateService(session);

        service.AddToCart(new CartItem
        {
            EventId = 1,
            EventTitle = "Event A",
            UnitPrice = 10m,
            Quantity = 1,
            AvailableTickets = 3
        });
        service.AddToCart(new CartItem
        {
            EventId = 1,
            EventTitle = "Event A",
            UnitPrice = 10m,
            Quantity = 5,
            AvailableTickets = 3
        });

        var cart = service.GetCart();
        Assert.Single(cart.Items);
        Assert.Equal(3, cart.Items[0].Quantity); // capped at AvailableTickets
        Assert.Equal(30m, cart.Items[0].TotalPrice);
        Assert.Equal(3, service.GetCartItemCount());
    }

    [Fact]
    public void UpdateQuantity_Changes_Quantity_And_Removes_When_Zero()
    {
        var session = new TestSession();
        var service = CreateService(session);

        service.AddToCart(new CartItem
        {
            EventId = 1,
            EventTitle = "Event A",
            UnitPrice = 10m,
            Quantity = 1,
            AvailableTickets = 10
        });
        service.UpdateQuantity(1, 5);

        var cart = service.GetCart();
        Assert.Equal(5, cart.Items[0].Quantity);
        Assert.Equal(50m, cart.Items[0].TotalPrice);
        Assert.Equal(5, service.GetCartItemCount());

        service.UpdateQuantity(1, 0);
        Assert.Empty(service.GetCart().Items);
        Assert.Equal(0, service.GetCartItemCount());
    }

    private class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new();

        public bool IsAvailable => true;
        public string Id { get; } = "TestSession";
        public IEnumerable<string> Keys => _store.Keys;

        public void Clear() => _store.Clear();

        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Remove(string key) => _store.Remove(key);
        public void Set(string key, byte[] value) => _store[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
    }
}

