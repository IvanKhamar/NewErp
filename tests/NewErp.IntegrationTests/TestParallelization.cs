// Тестові класи запускаються по черзі, а не паралельно. Драйвер MySql.Data має гонку в StartSSLAsync
// (кілька одночасних SSL-підключень псують спільний Dictionary), і з кількома класами тести падали випадково.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
