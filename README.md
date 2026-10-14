# NewErp

Нова ERP на .NET 10 для швейного виробництва та інтернет-магазину одягу. Вона поступово замінить облікову частину старого сайту на OpenCart (PHP).
Аналіз старої системи: [docs/old-erp-analysis.md](docs/old-erp-analysis.md).

Проєкт працює з тією самою структурою бази MySQL, що й старий сайт, тому модулі можна переносити по одному, поки старий сайт працює далі.

## Що вже вміє

| Модуль | Можливості | Ендпоінти |
|---|---|---|
| **Замовлення** | Список з фільтрами (статус, джерело, менеджер, період, пошук за телефоном, ім'ям, email чи номером) і пагінацією. Картка з товарами та історією статусів. Довідники статусів і джерел | `GET /orders`, `GET /orders/{id}`, `GET /orders/statuses`, `GET /orders/sources` |
| **Дії із замовленням** | Зміна статусу із записом у журнал (хто, коли, з якого на який). Призначення відповідального менеджера. Перевірка, що статус існує, а користувач активний | `PUT /orders/{id}/status`, `PUT /orders/{id}/manager` |
| **Бонуси менеджерів** | Розрахунок за період: кількість замовлень, виручка, бонус. Відсоток і статуси, що зараховуються, задаються в `appsettings.json` | `GET /staff/bonuses?from=&to=` |
| **Вхід і ролі** | Вхід через cookie, ролі Admin, Manager, Storekeeper. Без входу API повертає 401, без потрібної ролі 403. Замовлення бачать Admin і Manager, склад Admin і Storekeeper, бонуси лише Admin | `POST /auth/login`, `POST /auth/logout`, `GET /auth/me` |
| **Склад: розхідники** | Категорії, постачальники, позиції з мінімальним залишком. Прихід і витрата з журналом руху. Список того, що треба дозамовити. Залишок не може піти в мінус | `/stock/categories`, `/stock/suppliers`, `/stock/items`, `/stock/items/{id}/adjustments`, `/stock/items/{id}/movements` |

Повний список з описами доступний в інтерактивній документації `/scalar` після запуску. Готові запити для Visual Studio лежать у [NewErp.Api.http](src/NewErp.Api/NewErp.Api.http).

## Архітектура

Чиста архітектура (Clean Architecture): залежності спрямовані лише всередину, `Api → Infrastructure → Application → Domain`. Порушення цього правила ловить тест `LayerDependencyTests`.

```
src/
  NewErp.Domain          бізнес-правила без залежностей: зміна статусу, правило бонусу, залишок складу
  NewErp.Application     сценарії (OrderService, BonusService, StockService) та інтерфейси доступу до даних
  NewErp.Infrastructure  EF Core + MySQL: ErpDbContext, запити й репозиторії
  NewErp.Api             ASP.NET Core Minimal API, обробка помилок (ProblemDetails), OpenAPI + Scalar
tests/
  NewErp.UnitTests         домен і сценарії з підробленими (fake) сховищами, без бази
  NewErp.IntegrationTests  справжній API через WebApplicationFactory на тимчасовій базі MySQL
tools/
  NewErp.DevSeed         наповнює локальну базу вигаданими даними (Bogus)
db/
  schema.sql             таблиці OpenCart (dd_*), відновлені з install/opencart.sql старого сайту
  reconstructed.sql      власні таблиці старої ERP, відновлені за PHP-кодом (наша реконструкція)
  erp.sql                нові таблиці NewErp (erp_*)
```

Ключові рішення:

- **Database-first для старих таблиць.** Класи для `dd_*` згенеровано командою `dotnet ef dbcontext scaffold` у `Infrastructure/Persistence/Legacy`. Міграціями ці таблиці не змінюються.
- **Нові дані в окремих таблицях `erp_*`** (InnoDB, із зовнішніми ключами). Наприклад, менеджер замовлення зберігається в `erp_order_manager`, а не новою колонкою в `dd_order`.
- **Доменні класи складу зберігаються без посередників.** EF Core зіставляє `StockItem` з таблицею через Fluent API (`ErpDbContext.Stock.cs`), тож у домені немає атрибутів бази даних, а залишок змінюється лише методом `Adjust`, який завжди створює запис журналу.
- **Читання й запис розділено.** Списки й картки (`IOrderQueries`) одразу проєктуються в DTO через LINQ. Зміни проходять через сценарії Application і правила Domain.
- **Помилки бізнес-логіки** (`DomainException` → 400, `NotFoundException` → 404) перетворюються на відповіді у форматі ProblemDetails в одному місці (`ErrorHandler`).
- **Паралельні зміни залишку** не губляться: `quantity` позначено як маркер паралельного доступу (optimistic concurrency), тож при конфлікті API повертає 409, а не перезаписує чужу зміну.
- **Час** береться з `TimeProvider`, тому тести можуть його підмінити.
- **Провайдер MySQL:** `MySql.EntityFrameworkCore` (Oracle). Pomelo поки не має версії під EF Core 10.

## Запуск на Windows

Потрібні .NET SDK 10, MySQL 8 (MySQL Installer) і Visual Studio 2026 (або лише командний рядок).

1. Створіть порожню базу й користувача (у MySQL Workbench або `mysql -u root -p`):
   ```sql
   CREATE DATABASE newerp_dev CHARACTER SET utf8mb4;
   CREATE USER 'newerp'@'localhost' IDENTIFIED BY 'придумайте_пароль';
   GRANT ALL ON newerp_dev.* TO 'newerp'@'localhost';
   -- лише для інтеграційних тестів: дозволяє створювати й видаляти тимчасові бази newerp_test_*
   GRANT ALL ON `newerp_test%`.* TO 'newerp'@'localhost';
   GRANT CREATE ON *.* TO 'newerp'@'localhost';
   ```
2. Застосуйте структуру з кореня репозиторію (PowerShell):
   ```
   mysql -u root -p newerp_dev -e "source db/schema.sql; source db/reconstructed.sql; source db/erp.sql"
   ```
3. Збережіть рядок підключення в user-secrets (він лишається лише на вашому комп'ютері й не потрапляє в git):
   ```
   dotnet user-secrets set "ConnectionStrings:ErpDb" "server=localhost;database=newerp_dev;user=newerp;password=придумайте_пароль" --project src/NewErp.Api
   ```
4. Наповніть базу вигаданими даними (5 менеджерів, 400 замовлень, склад):
   ```
   dotnet run --project tools/NewErp.DevSeed
   ```
5. Запустіть API й відкрийте http://localhost:5092/scalar. Спершу виконайте `POST /auth/login` з `{ "username": "admin", "password": "dev" }`: браузер збереже cookie, і решта запитів у Scalar працюватиме. Інші тестові логіни DevSeed виводить після заповнення бази.
   ```
   dotnet run --project src/NewErp.Api
   ```

У Visual Studio: відкрийте `NewErp.slnx`, зробіть `NewErp.Api` стартовим проєктом і натисніть F5.

## Тести

```
dotnet test
```

Юніт-тести не потребують бази. Інтеграційні тести створюють тимчасову базу `newerp_test_*`, проганяють справжні HTTP-запити й видаляють базу. Без змінної середовища вони пропускаються. Щоб їх увімкнути (PowerShell, права з кроку 1 мають бути видані):

```
$env:NEWERP_TEST_DB = "server=localhost;user=newerp;password=придумайте_пароль"
dotnet test
```

## Обмеження та що звірити

- **Справжньої бази немає.** Структура `dd_*` відновлена зі старого коду. Таблиці з `reconstructed.sql` і колонка `dd_order.order_source_id` є нашою реконструкцією, їх треба звірити з реальною базою, коли буде доступ.
- **Таблиці OpenCart мають рушій MyISAM**, який не підтримує транзакцій. Тому зміна статусу й запис у журнал не атомарні.
- **Входу в систему ще немає.** Id користувача, який виконує дію, передається в тілі запиту. Наступний крок: автентифікація та ролі.
- **Правило бонусу спрощене** (відсоток від суми замовлень у вибраних статусах). Формулу старого модуля `user/bonus` треба звірити.
- **Мова довідників** задається в `Erp:LanguageId` і має збігатися з `dd_language` старого сайту.

## Далі

Автентифікація й ролі, виробництво та специфікації (BOM), синхронізація залишків, ТТН Нової Пошти, каса Checkbox, зіставлення банківських платежів, веб-інтерфейс.
