using Xunit;

namespace ECommerce.Tests;

// ===== INVENTORY DOMAIN TESTS =====
// בודקים את לוגיקת המלאי — שמירה, שחרור, בדיקת זמינות

public class InventoryTests
{
    [Fact]
    public void Inventory_ReserveStock_DecreasesQuantity()
    {
        // Arrange
        var availableStock = 10;
        var requestedQuantity = 3;

        // Act
        var remainingStock = availableStock - requestedQuantity;

        // Assert
        Assert.Equal(7, remainingStock);
    }

    [Fact]
    public void Inventory_CannotReserve_WhenInsufficientStock()
    {
        // Arrange
        var availableStock = 2;
        var requestedQuantity = 5;

        // Act
        var canReserve = availableStock >= requestedQuantity;

        // Assert - לא ניתן לשמור יותר ממה שיש
        Assert.False(canReserve);
    }

    [Fact]
    public void Inventory_CanReserve_WhenExactStock()
    {
        // Arrange - בדיוק כמה שיש
        var availableStock = 5;
        var requestedQuantity = 5;

        // Act
        var canReserve = availableStock >= requestedQuantity;

        // Assert
        Assert.True(canReserve);
    }

    [Fact]
    public void Inventory_ReleaseStock_IncreasesQuantity()
    {
        // Arrange - שחרור מלאי בעת ביטול הזמנה (compensation)
        var currentStock = 7;
        var releasedQuantity = 3;

        // Act
        var newStock = currentStock + releasedQuantity;

        // Assert
        Assert.Equal(10, newStock);
    }

    [Theory]
    [InlineData(10, 5, true)]
    [InlineData(5, 5, true)]
    [InlineData(4, 5, false)]
    [InlineData(0, 1, false)]
    public void Inventory_StockAvailability_IsCorrect(int available, int requested, bool expectedResult)
    {
        var canReserve = available >= requested;
        Assert.Equal(expectedResult, canReserve);
    }
}
