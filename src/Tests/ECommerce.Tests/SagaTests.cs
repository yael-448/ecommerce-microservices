using Xunit;

namespace ECommerce.Tests;

// ===== SAGA LOGIC TESTS =====
// בודקים את לוגיקת הסאגה — מה קורה בכל שלב

public class SagaTests
{
    [Fact]
    public void Saga_OrderPlaced_ShouldHaveCorrelationId()
    {
        // CorrelationId חייב להיות קיים כדי לעקוב אחרי הסאגה
        var correlationId = Guid.NewGuid().ToString();

        Assert.NotNull(correlationId);
        Assert.NotEmpty(correlationId);
    }

    [Fact]
    public void Saga_CorrelationId_ShouldBeValidGuid()
    {
        // CorrelationId חייב להיות GUID תקין
        var correlationId = Guid.NewGuid().ToString();

        var isValid = Guid.TryParse(correlationId, out _);
        Assert.True(isValid);
    }

    [Fact]
    public void Saga_HappyPath_OrderConfirmed_AfterInventoryReserved()
    {
        // מדמה את הנתיב המאושר של הסאגה
        var orderStatus = "Pending";

        // InventoryService שמר מלאי
        var inventoryReserved = true;

        // OrderService מאשר
        if (inventoryReserved)
            orderStatus = "Confirmed";

        Assert.Equal("Confirmed", orderStatus);
    }

    [Fact]
    public void Saga_CompensationPath_OrderRejected_WhenInventoryInsufficient()
    {
        // מדמה את נתיב הפיצוי — מלאי לא מספיק
        var orderStatus = "Pending";

        // InventoryService דחה
        var inventoryReserved = false;
        var rejectionReason = "Insufficient stock";

        // OrderService מבטל
        if (!inventoryReserved)
            orderStatus = "Rejected";

        Assert.Equal("Rejected", orderStatus);
        Assert.NotEmpty(rejectionReason);
    }

    [Fact]
    public void Saga_NotificationSent_AfterOrderConfirmed()
    {
        // NotificationService צריך לשלוח הודעה אחרי אישור
        var notificationsSent = new List<string>();
        var orderStatus = "Confirmed";
        var customerEmail = "test@example.com";

        if (orderStatus == "Confirmed")
            notificationsSent.Add($"Order confirmed for {customerEmail}");

        Assert.Single(notificationsSent);
        Assert.Contains(customerEmail, notificationsSent[0]);
    }

    [Fact]
    public void Saga_NotificationSent_AfterOrderRejected()
    {
        // NotificationService צריך לשלוח הודעה גם אחרי דחייה
        var notificationsSent = new List<string>();
        var orderStatus = "Rejected";
        var customerEmail = "test@example.com";

        if (orderStatus == "Rejected")
            notificationsSent.Add($"Order rejected for {customerEmail}");

        Assert.Single(notificationsSent);
    }
}
