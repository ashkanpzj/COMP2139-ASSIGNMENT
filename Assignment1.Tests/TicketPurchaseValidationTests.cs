using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Assignment1.Models;
using Xunit;

namespace Assignment1.Tests;

public class TicketPurchaseValidationTests
{
    [Fact]
    public void Quantity_Must_Be_At_Least_One()
    {
        var purchase = new TicketPurchase
        {
            EventId = 1,
            Quantity = 0, // invalid
            UnitPrice = 50,
            TotalPrice = 0
        };

        var results = Validate(purchase);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(TicketPurchase.Quantity)));
    }

    [Fact]
    public void TotalPrice_Must_Be_Non_Negative()
    {
        var purchase = new TicketPurchase
        {
            EventId = 1,
            Quantity = 2,
            UnitPrice = 25,
            TotalPrice = -1 // invalid
        };

        var results = Validate(purchase);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(TicketPurchase.TotalPrice)));
    }

    private static IList<ValidationResult> Validate(object model)
    {
        var ctx = new ValidationContext(model, null, null);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, ctx, results, true);
        return results;
    }
}

