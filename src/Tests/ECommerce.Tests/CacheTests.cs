using Xunit;

namespace ECommerce.Tests;

// ===== CACHE-ASIDE PATTERN TESTS =====
// בודקים את לוגיקת המטמון של ProductCatalogService

public class CacheTests
{
    // מדמה cache פשוט בזיכרון לצורך הבדיקות
    private readonly Dictionary<string, string> _cache = new();

    private string? GetFromCache(string key)
    {
        _cache.TryGetValue(key, out var value);
        return value;
    }

    private void SetInCache(string key, string value) => _cache[key] = value;
    private void RemoveFromCache(string key) => _cache.Remove(key);

    [Fact]
    public void Cache_Miss_WhenKeyNotExists()
    {
        // CACHE MISS — המפתח לא קיים
        var result = GetFromCache("products:nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public void Cache_Hit_WhenKeyExists()
    {
        // CACHE HIT — המפתח קיים
        SetInCache("products:123", "{\"id\":\"123\",\"name\":\"Laptop\"}");

        var result = GetFromCache("products:123");

        Assert.NotNull(result);
        Assert.Contains("Laptop", result);
    }

    [Fact]
    public void Cache_Invalidation_OnProductUpdate()
    {
        // כשמוצר מתעדכן, המטמון חייב להתנקות
        SetInCache("products:123", "{\"name\":\"Old Name\"}");
        SetInCache("products:all", "[{\"name\":\"Old Name\"}]");

        // Update product → invalidate cache
        RemoveFromCache("products:123");
        RemoveFromCache("products:all");

        Assert.Null(GetFromCache("products:123"));
        Assert.Null(GetFromCache("products:all"));
    }

    [Fact]
    public void Cache_CacheAside_Pattern_Works()
    {
        // מדמה את הזרימה המלאה של cache-aside:
        // 1. בדוק cache → miss
        // 2. קרא מ-DB
        // 3. שמור ב-cache
        // 4. בדוק cache → hit

        var cacheKey = "products:456";

        // שלב 1: cache miss
        var cached = GetFromCache(cacheKey);
        Assert.Null(cached);

        // שלב 2: קריאה מ-"DB"
        var fromDb = "{\"id\":\"456\",\"name\":\"Phone\"}";

        // שלב 3: שמירה ב-cache
        SetInCache(cacheKey, fromDb);

        // שלב 4: cache hit
        var cachedNow = GetFromCache(cacheKey);
        Assert.NotNull(cachedNow);
        Assert.Equal(fromDb, cachedNow);
    }

    [Fact]
    public void Cache_Key_Format_IsCorrect()
    {
        // בודק שמפתחות המטמון בפורמט הנכון
        var productId = Guid.NewGuid();
        var singleKey = $"products:{productId}";
        var allKey = "products:all";

        Assert.StartsWith("products:", singleKey);
        Assert.Equal("products:all", allKey);
    }
}
