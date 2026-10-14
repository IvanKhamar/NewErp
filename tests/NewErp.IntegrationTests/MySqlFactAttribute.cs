namespace NewErp.IntegrationTests;

/// <summary>
/// Тест, якому потрібен справжній MySQL. Якщо змінну середовища NEWERP_TEST_DB не задано, тест пропускається,
/// тож dotnet test проходить і на машині без бази.
/// </summary>
public sealed class MySqlFactAttribute : FactAttribute
{
    public MySqlFactAttribute()
    {
        if (string.IsNullOrEmpty(TestDatabase.ServerConnectionString))
            Skip = $"Задайте {TestDatabase.EnvVar}, щоб запустити інтеграційні тести (див. README).";
    }
}
/// <summary>Те саме для тестів з параметрами ([InlineData]).</summary>
public sealed class MySqlTheoryAttribute : TheoryAttribute
{
    public MySqlTheoryAttribute()
    {
        if (string.IsNullOrEmpty(TestDatabase.ServerConnectionString))
            Skip = $"Задайте {TestDatabase.EnvVar}, щоб запустити інтеграційні тести (див. README).";
    }
}
