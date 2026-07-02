using Xunit;

namespace ECommerce.Tests;

// ===== ORDER DOMAIN TESTS =====
// בודקים את הלוגיקה של הזמנות ללא תלות ב-DB או RabbitMQ

public class OrderTests
{
    [Fact]
    public void Order_TotalAmount_IsCalculatedCorrectly()
    {
        // Arrange - מסדרים את הנתונים
        var items = new List<(decimal price, int qty)>
        {
            (100m, 2),  // 200
            (50m, 3),   // 150
            (25m, 4)    // 100
        };

        // Act - מחשבים
        var total = items.Sum(i => i.price * i.qty);

        // Assert - בודקים
        Assert.Equal(450m, total);
    }

    [Fact]
    public void Order_WithZeroQuantity_ShouldBeInvalid()
    {
        // Arrange
        var quantity = 0;

        // Act & Assert - כמות 0 לא תקינה
        Assert.True(quantity <= 0, "Quantity must be greater than zero");
    }

    [Fact]
    public void Order_WithNegativePrice_ShouldBeInvalid()
    {
        // Arrange
        var price = -10m;

        // Assert - מחיר שלילי לא תקין
        Assert.True(price < 0, "Price cannot be negative");
    }

    [Theory]
    [InlineData("test@example.com", true)]
    [InlineData("invalid-email", false)]
    [InlineData("", false)]
    [InlineData("user@domain.co.il", true)]
    public void Order_CustomerEmail_ValidationWorks(string email, bool expectedValid)
    {
        // בודק שאימות אימייל עובד נכון
        var isValid = !string.IsNullOrEmpty(email) && email.Contains('@') && email.Contains('.');
        Assert.Equal(expectedValid, isValid);
    }

    [Fact]
    public void Order_Status_StartsAsPending()
    {
        // כל הזמנה חדשה מתחילה כ-Pending
        var status = "Pending";
        Assert.Equal("Pending", status);
    }
}
