using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Assignment1.Models;
using Xunit;

namespace Assignment1.Tests;

public class ModelsValidationTests
{
    [Fact]
    public void Event_Requires_Title()
    {
        var ev = new Event
        {
            Title = "", // invalid
            AvailableTickets = 10,
            Price = 20
        };

        var results = Validate(ev);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(Event.Title)));
    }

    [Fact]
    public void Event_AvailableTickets_Must_Be_NonNegative()
    {
        var ev = new Event
        {
            Title = "Sample",
            AvailableTickets = -1, // invalid
            Price = 10
        };

        var results = Validate(ev);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(Event.AvailableTickets)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void EventRating_Rating_Must_Be_Within_1_To_5(int rating)
    {
        var ratingModel = new EventRating
        {
            EventId = 1,
            Rating = rating
        };

        var results = Validate(ratingModel);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(EventRating.Rating)));
    }

    private static IList<ValidationResult> Validate(object model)
    {
        var ctx = new ValidationContext(model, null, null);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, ctx, results, true);
        return results;
    }
}




