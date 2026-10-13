# NewErp

Нова ERP на .NET, яка замінить облікову частину старого сайту на OpenCart.
Аналіз старої системи: [docs/old-erp-analysis.md](docs/old-erp-analysis.md).

## Структура

```
src/
  NewErp.Domain          сутності та бізнес-правила, без залежностей
  NewErp.Application     сценарії використання; залежить лише від Domain
  NewErp.Infrastructure  БД, інтеграції (Checkbox, Нова пошта, банки); реалізує інтерфейси Application
  NewErp.Api             ASP.NET Core Web API, точка входу
tests/
  NewErp.UnitTests       юніт-тести та перевірка залежностей між шарами
```

Залежності спрямовані лише всередину: `Api → Infrastructure → Application → Domain`.
Бізнес-модулі (замовлення, склад, каса, доставка тощо) розкладаються по папках усередині кожного шару.

## Запуск

Потрібен .NET SDK 10.

```
dotnet build
dotnet test
dotnet run --project src/NewErp.Api
```

Перевірка, що API працює: `GET /health`.
