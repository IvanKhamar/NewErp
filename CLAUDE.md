# NewErp

Нова ERP на .NET 10, яка замінить облікову частину старого сайту (OpenCart).
Аналіз старої системи лежить у `docs/old-erp-analysis.md`. Читай його лише тоді, коли задача стосується перенесення логіки.

## Синхронно з Java-версією

Є друга версія цієї ERP на Java: `IvanKhamar/NewErpJava` (локально `F:\PROJECTS\NewErpJava\NewErpJava`). Обидві розвиваємо
синхронно (рішення від 2026-10-25):

- Однакові функції, база й контракт API: шляхи, назви полів JSON, коди статусів і тексти помилок. Тоді Blazor UI і
  Vaadin UI працюють з будь-яким із двох API.
- Одна функція — по PR у кожному репозиторії. Наступну функцію не починаємо, поки попередня не є в обох.
- Переносячи функцію з Java, беремо її тести як специфікацію й переносимо в xUnit.
- `db/schema.sql`, `db/reconstructed.sql`, `db/erp.sql` однакові в обох репозиторіях; зміну схеми робимо в обох.
- Що ще не перенесено, записано у фазі 16 плану проєкту.

## Структура та правила коду

- Шари: `Domain ← Application ← Infrastructure ← Api`. Залежності спрямовані лише всередину, це перевіряє `tests/NewErp.UnitTests/LayerDependencyTests.cs`.
- Веб-інтерфейс `NewErp.Web` (Blazor WebAssembly + MudBlazor) живе під `/app/`, роздається з `NewErp.Api` і говорить з сервером лише через HTTP API; з `NewErp.Application` бере тільки record-и запитів і відповідей. Адреси API пишемо з `/` на початку (`/stock/items`), бо `<base href="/app/">`. Ролі розділів — `Auth/Access.cs`, у парі з політиками в `NewErp.Api/Auth/AuthSetup.cs`.
- Бізнес-модулі (Orders, Stock, Cashbox, Delivery…) — це папки всередині кожного шару, а не окремі проєкти.
- Імена мають вигляд `NewErp.*`, акронім пишемо `Erp`, а не `ERP`.
- Спільні налаштування збірки задано в `Directory.Build.props`, попередження там вважаються помилками.
- Мертвий код старої ERP (OpenBay, іноземні платіжні шлюзи) не переносимо.

## Команди

```
dotnet build
dotnet test
dotnet run --project src/NewErp.Api   # /scalar — документація API
dotnet run --project tools/NewErp.DevSeed   # вигадані дані в порожню базу
dotnet run --project tools/NewErp.SchemaCheck -- --database newerp_shop_copy   # звірити базу зі схемою NewErp (фаза 14)
dotnet tool restore && dotnet ef ...  # dotnet-ef закріплено в dotnet-tools.json
```

Інтеграційні тести вмикаються змінною `NEWERP_TEST_DB` (див. README), без неї пропускаються.

## База даних і навчання

- Справжньої бази OpenCart немає, і персональних даних клієнтів не використовуємо. Працюємо з локальною MySQL 8 `newerp_dev`, створеною з `db/schema.sql` (структура з `install/opencart.sql` старого сайту, префікс `dd_`), `db/reconstructed.sql` (наша реконструкція власних таблиць старої ERP) і `db/erp.sql` (нові таблиці `erp_*`). Дані лише вигадані, з `tools/NewErp.DevSeed`.
- Провайдер EF Core: `MySql.EntityFrameworkCore` (Oracle), бо Pomelo ще не підтримує EF Core 10. Класи `dd_*` згенеровано scaffold у `Infrastructure/Persistence/Legacy`, руками їх не правимо; таблиці `erp_*` і реконструйовані `dd_stock_*` описано вручну в `ErpDbContext.<Модуль>.cs` (Stock, Production, Auth).
- Де старий сайт уже має таблиці для модуля (наприклад, StockTrack: `dd_stock_*`), пишемо в них, а не в нові `erp_*`. Структуру відновлюємо за PHP-кодом старого сайту (`F:\PROJECTS\VIDRO\Vidro_OpenCart_WebSite`, лише читаємо, `config.php` не відкриваємо) і кладемо в `db/reconstructed.sql` з позначкою «звірити».
- Працюємо database-first (`dotnet ef dbcontext scaffold`), таблиці OpenCart міграціями не змінюємо.
- Таблиць і колонок, яких немає в `db/schema.sql`, не вигадуємо мовчки. Кожну реконструкцію позначаємо як нашу.
- Нові власні таблиці створюємо лише з префіксом `erp_`.
- Паролі й рядки підключення зберігаємо лише в `dotnet user-secrets`, ніколи в `appsettings.json`.
- Схему й дані `newerp_dev` дивимось через MCP `dbhub` (користувач `claude_ro`, лише SELECT, налаштування в `F:\PROJECTS\NewErp\dbhub.toml`), а не через `mysql` у Bash.

## Git

- Репозиторій: github.com/IvanKhamar/NewErp, гілка `main`.

Загальні правила (мова, ощадлива робота, git, секрети, особливості цього комп'ютера) лежать у `~/.claude/CLAUDE.md`. Проєкт малий, тож субагенти й широкий пошук тут майже ніколи не потрібні.
