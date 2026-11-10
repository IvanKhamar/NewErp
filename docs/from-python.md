# Features built in Python first, to port here

The Python version (`IvanKhamar/NewErpPython`, locally `F:\PROJECTS\NewErpPython\NewErpPython`) got these features
before this repo: items 23–77 of its `docs/plan.md`. Most come from a direct comparison with the old PHP admin
(`F:\PROJECTS\VIDRO\Vidro_OpenCart_WebSite`, every custom `admin/controller/*` action and the admin menu).

How to use this file:

- **Porting an entry:** read the Python code and tests it names (service in `src/newerp/application`, SQL in
  `src/newerp/infrastructure`, endpoints in `src/newerp/api`, tests in `tests/`), keep the contract exactly (paths,
  JSON field names, status codes, Ukrainian texts, settings and their defaults), and port the tests. One entry = one PR.
- **When it is merged here:** change its status to `DONE` with the PR number and date. Do not delete entries.
- **Tables:** the new `dd_*` tables and columns are in the Python repo's `db/reconstructed.sql` (sections named after
  each feature); copy those sections into this repo's `db/reconstructed.sql`. No new `erp_*` table was added.
- **Security rules** written under each entry are part of the feature: an entry without them is not done.
  General rules for all of them: deny by default (every endpoint has a role), every input validated on the server,
  rich text cleaned with an allowlist before saving (Python: nh3; .NET: HtmlSanitizer; Java: OWASP Java HTML
  Sanitizer), money and access changes written to an audit log (who, what; personal data not logged), secrets only in
  settings/env, never shown or edited in pages.

## Port order and what is left (stopped here on 2026-11-05)

API, tests and Blazor pages for each group, one branch and one PR per group. Items can share files, so groups go one after another.

| Group | Branch | Items | State |
|---|---|---|---|
| 1 | `feat/py-security` | 32, 33, 34, audit log | merged |
| 2 | `feat/py-order-delivery` | 23, 24 | merged |
| 3 | `feat/py-coupons` | 25 | merged |
| 4 | `feat/py-alerts-matching` | 26, 27, 28 | merged |
| 5 | `feat/py-reports` | 29, 30, 31, 71 | merged |
| 6 | `feat/py-order-tools` | 35–42 | merged (#41) |
| 7 | `feat/py-product-form` | 43–45, 75, 76 | TODO |
| 8 | `feat/py-catalog-lists` | 46–52 | TODO |
| 9 | `feat/py-customers` | 53, 54 | TODO |
| 10 | `feat/py-monobank` | 55, 56 | TODO |
| 11 | `feat/py-marketing` | 57–62, 77 | TODO |
| 12 | `feat/py-admin` | 63–68, 69, 70 | TODO |

Things to know when continuing:

- **Running the tests:** a throwaway `mysql:8.4` on port 3317 and `redis:7` on 6389, with `NEWERP_TEST_DB=server=127.0.0.1;port=3317;user=root` and `NEWERP_TEST_REDIS=localhost:6389`. Check that nothing was skipped.
- **`dd_customer_transaction`** is already in `db/reconstructed.sql` and mapped (group 5), for groups 6 and 9.
- **Local `newerp_dev`** needs the sections added by groups 2, 3 and 5 of `db/reconstructed.sql`, run by hand: `dd_product_special`, the coupon tables and the report tables.
- **Not tried against the real services:** Nova Poshta courier delivery, `tools/NewErp.SchemaCheck --integrations`, and the shapes of the reconstructed tables against the real database.
- **Daily timer:** `LowStockAlertWorker` has no test of its own.

---

## Earlier Python-first items (23–34)

| # | Feature | Python commit | Status |
|---|---|---|---|
| 23 | Nova Poshta courier delivery to an address: `toDoor`, `streetRef`, `building`, `flat`; `GET /delivery/streets` | a958344 | DONE: `feat/py-order-delivery`, 2026-11-05 |
| 24 | Sale prices (`dd_product_special`, `percent`) in the product search of a new order: `price`, `regularPrice` | b660c33 | DONE: `feat/py-order-delivery`, 2026-11-05 |
| 25 | Coupon admin `/marketing/coupons` with products/categories and their `*_exclude` tables, usage history | 0d88a5b | DONE: `feat/py-coupons`, 2026-11-05 |
| 26 | Low-stock alerts: a daily SMS (`stock.alertPhones`, `alertTime`), `POST /stock/alerts/low-stock`, home panel | ab43938 | DONE: `feat/py-alerts-matching`, 2026-11-05; home panel `LowStockPanel` |
| 27 | Consumables for the sewing plan `GET /production/plan/materials?scope=orders|minimum|all` | f672a8c | DONE here (Python ported it from .NET); `scope=minimum|all` DONE: `feat/py-alerts-matching`, 2026-11-05 |
| 28 | Automatic bank payment matching after import and `POST /payments/bank/auto-match`; `autoLinked` | ddbc1c9 | DONE: `feat/py-alerts-matching`, 2026-11-05 |
| 29 | A read-only check of the schema and the integrations against the real database and keys | 412e69a | DONE: `feat/py-reports`, 2026-11-05; `tools/NewErp.SchemaCheck --integrations` (the schema part was already there) |
| 30 | UTM report `GET /reports/utm`, `GET /orders/{id}/remarketing` (dd_remarketing_orders) | 2435c3c | DONE: `feat/py-reports`, 2026-11-05 |
| 31 | Reports `/reports/online`, `/product-views`, `/rewards`, `/taxes` | 6015add | DONE: `feat/py-reports`, 2026-11-05 |
| 32 | "Different customers from one IP" (`sharedIp`) in the customer risk check | 6761c0f | DONE: `feat/py-security`, 2026-11-05 |
| 33 | The login throttle shared between instances (Redis); client IP only from trusted proxies | 167f2a3 | DONE: `feat/py-security`, 2026-11-05; `ConnectionStrings:Redis`, `TrustedProxies` |
| 34 | Login CSRF: a double-submit token on the login form | 167f2a3 | DONE: `feat/py-security`, 2026-11-05; no token here: login takes only JSON, which another site cannot send without CORS (test `Login_TakesOnlyJson`) |

## 35–42. Orders: the old admin's order page tools — Python 67f29fb

- **Status:** DONE (NewErp #41, 2026-11-10; also `GET /orders/print?ids=` for the Blazor print pages, recorded for Java and Python)
- **Contract:** `GET/PUT /orders/{id}/customer` (customer, address, methods; the rules of a new order);
  `GET /orders/{id}/shipping-check?statusId=` (before 21 "Чекає на відправку" and 22 "Відправлено": waybill, SMS with it
  to the order phone); `GET /orders/discrepancies?days=`; `PUT /orders/{id}/shipping-date` (`dd_order.date_shipping`,
  zero dates read as null); `POST /orders/{id}/invoice` (next number per prefix, in one UPDATE); print pages for the
  invoice and the shipping list; `GET /orders/{id}/credits`, `POST|DELETE /orders/{id}/reward|commission`;
  `POST /orders/new/shipping-cost` (Nova Poshta getDocumentPrice; weight × quantity); `DELETE /orders/{id}`.
- **Security:** delete is admin only and refused while a waybill, cheque, bank payment, return or gift voucher points
  to the order; the status handlers give the stock back first; reward, commission, delete and customer edits audited.
- **Tables:** `dd_order.date_shipping`, `dd_customer_transaction`.

## 43–52. Catalog — Python 9e1df9d, 157844e, 4669ad9, f644db7, dc27a5a, 9ba10d9

- **Status:** TODO
- **Contract:** `POST /catalog/products`, `GET /catalog/products/{id}/edit`, `PUT /catalog/products/{id}`
  (dateModified, 409), `POST /catalog/products/{id}/copy`, `POST /catalog/products/remove-new`,
  `GET /catalog/products/form-lookups`; `POST /catalog/products/import/preview` and `/import` (CSV);
  `POST /catalog/prices/preview` and `/apply`; `POST /catalog/prices/size-filters`; `/catalog/labels`,
  `/catalog/search-presets`, `/catalog/categories` (+ `/repair`), `/catalog/references/{filters|attributes|options}`
  with `/values`, `/catalog/manufacturers`, `/catalog/downloads`, `/catalog/information`, `/catalog/faq`,
  `/catalog/blog/categories|posts`, `/catalog/blocks/about|nft`, `DELETE /catalog/content/{kind}/{id}`,
  `/catalog/reviews`, `/catalog/questions`.
- **Security:** HTML cleaned; picture paths only `catalog/…` images, SEO addresses `[a-z0-9-_]` and unique, colours
  only `#rrggbb`, links only the shop or https; the CSV import only updates (no creating, no stock, no picture
  downloads), UTF-8, at most 1 MB and 5000 rows; mass prices, import, category delete and size filters admin only;
  nothing in use can be deleted (category with subcategories, a used filter/attribute/option value); a size with
  stock cannot be removed from a product; a copy is disabled, without stock and SEO address.
- **Tables:** product description own columns, `dd_seo_url`, `dd_product_related(_color, _variant)`,
  `dd_product_color`, category own columns, `dd_category_path`, `dd_category_to_store`, `dd_category_filter`,
  `dd_search_preset`, filter/attribute/option/manufacturer/download tables, information, FAQ, blog, review tables,
  own `dd_question` columns.

## 53–54. Customers — Python 92deeb8

- **Status:** TODO
- **Contract:** `GET /customers/{id}/edit`, `PUT /customers/{id}` (with addresses, never the password),
  `GET /customers/{id}/account`, `POST /customers/{id}/transactions|rewards|unlock|login-link`;
  `/customers/groups` (+ `/details`), `/customers/approvals/{id}/approve|deny`, `/customers/custom-fields`.
- **Security:** e-mail unique; balance and points audited; the login link is admin only, one-time, its token carries
  its creation time and a scheduled task removes unused ones after `customers.loginLinkMinutes` (5);
  groups and custom fields admin only; the default group and groups with customers cannot be deleted.
- **Tables:** `dd_customer_ip`, `dd_customer_login`, `dd_customer_approval`, `dd_customer_group`, custom field tables.

## 55–56. Payments — Python 825fbcb

- **Status:** TODO
- **Contract:** `POST /orders/{id}/mono-link`, `GET /orders/{id}/mono-invoices`, `POST /payments/monobank/check` and
  a scheduled check every `monobank.checkIntervalMinutes` (30) of the last `checkDays` (3).
- **Security:** the token only in settings and only in the `X-Token` header; TLS checks and a timeout (the old module
  turned both off); without a token test invoices on a `.invalid` address. Item 56 (PrivatBank business cards) is
  only more accounts in the PrivatBank settings: check that every account is read with its own keys.
- **Tables:** `dd_mono_orders`.

## 57–62. Marketing and integrations — Python b15a814, 89baf8d, be5e998

- **Status:** TODO
- **Contract:** `/marketing/tracking` (clicks, orders, sum, storefront link); `POST /marketing/vouchers/{id}/email`
  (SMTP settings), `/marketing/vouchers/themes`; `GET /marketing/klaviyo/export/{customers|subscribers|unsubscribers}.csv`,
  `GET|PUT /marketing/klaviyo/settings`; `GET /feeds/tiktok.csv` (token in `X-Feed-Token` or `?token=`);
  `POST /marketing/instagram/update` and a scheduled task; `GET|PUT /marketing/remarketing/settings`.
- **Security:** no header injection in e-mail; CSV cells starting with `= + - @` get an apostrophe; the feed token is
  compared in constant time; Instagram pictures only from its CDN over https, at most 5 MB, numeric names; module
  settings editor: secrets (token, password, key…) only "set/not set", JavaScript counters, SQL and routes read-only.
- **Tables:** `dd_marketing`, `dd_instagram`.

## 63–68. Administration — Python 560233c

- **Status:** TODO
- **Contract:** `GET|PUT /admin/settings`, `/admin/user-groups` (+ `/routes`), `/admin/api-users` (+ `/{id}/key`),
  `GET|DELETE /admin/error-log` (+ `/download`), `GET /admin/backup/tables`, `GET /admin/backup.sql.gz?tables=`,
  `/admin/uploads` (+ `/{id}/file`).
- **Security:** admin only; secrets of `config` hidden, analytics code and file allowlists read-only; the admin's own
  OpenCart group keeps `user/user_permission`; an API key is made by the server, shown once, its change ends sessions;
  the log is masked; the backup only dd_/erp_ tables, logged, no restore from the web; an upload path outside the
  folder is refused; downloads with nosniff and an RFC 5987 file name.
- **Tables:** `dd_user_group`, `dd_api`, `dd_api_ip`, `dd_api_session`, `dd_upload`.

## 69–71. Status lists, banners and SEO addresses, standard reports — Python e3b53bd

- **Status:** 71 DONE: `feat/py-reports`, 2026-11-05. 69, 70 TODO
- **Contract:** `/admin/status-lists/{order-statuses|return-statuses|return-reasons|return-actions|stock-statuses}`,
  `/catalog/banners`, `/catalog/seo-urls`, `GET /reports/standard/{kind}?from=&to=&group=day|week|month`.
- **Security:** a used status can only be renamed; colours `#rrggbb`; banner links only the shop or https; SEO
  addresses unique and only for known page kinds; reports at most 366 days and 500 rows.
- **Tables:** `dd_stock_status`, `dd_banner`, `dd_banner_image`, `dd_customer_activity`, `dd_customer_search`,
  `dd_statistics`.

## 75. Picture upload — Python 87f8bd3

- **Status:** TODO
- **Contract:** `POST /catalog/pictures/{kind}` (multipart field `file`; `kind`: products, categories, banners, vouchers) returns 201 `{"path": "catalog/<kind>/<16 hex>.<ext>"}`; a file picker next to each picture path field of the product, category, banner and voucher theme forms. Setting `NEWERP_PICTURES__IMAGE_DIR` (the storefront's image folder on a shared volume); empty: 400 with a text naming the setting.
- **Security:** the browser's file name and type are not used; the type comes from the first bytes (JPEG, PNG, WebP, GIF only; SVG is refused because it can carry scripts); at most 5 MB; the server makes the name; an existing file is never overwritten; the path can't leave the folder; audited.

## 76. Options other than sizes in the product form — Python df5af90

- **Status:** TODO
- **Contract:** `options` next to `sizes` in `GET /catalog/products/{id}/edit`, `POST` and `PUT`: `{optionId, name, required, value, values: [{optionValueId, name, price, pricePrefix, quantity}]}`; `GET /catalog/products/form-lookups` gets `options: [{id, name, type, hasValues, values}]`, without the size option. `select`, `radio`, `checkbox` and `image` options have values; the others hold a text in `value`.
- **Rules:** keep sizes and other options apart when reading (the Python form first read every option value as a size, so saving refused the product or dropped its colours). Rows are changed in place, because the lines of old orders point to them. A new non-size value has `subtract = 0`, because production keeps stock only by size and the storefront would hide it at zero. A value with stock can't be removed. An option with nothing left is removed with its `dd_product_option` row.
- **Security:** an option or value must exist and belong together; a list option needs at least one value; `<` and `>` are refused in a text value.

## 77. TikTok catalog sync and the Klaviyo log — Python ef9592d

- **Status:** TODO
- **Contract:** `POST /marketing/tiktok/sync` returns `{configured, uploaded, failed}` and a scheduled task every `tiktokSync.intervalHours` (0: only by hand); `GET /marketing/klaviyo/log` returns `{enabled, size, text, cut}`. Settings: TikTok access token, `bcId`, `catalogId`; the Klaviyo log path.
- **Rules:** batches of 1000 to `https://business-api.tiktok.com/open_api/v1.3/catalog/product/upload/`, accepted when the answer's `message` is `OK`. `sku_id` is the product id, as in the feed, because many products share a print code. Disabled products go as `OUT_OF_STOCK`.
- **Security:** the token only in settings and only in the `Access-Token` header, never logged; a refused batch is counted, not an error; the log is masked and admin only. Python tried it only against a fake TikTok, so check the first real run.

## Python fixes of 2026-11-05 to keep in mind

- **The audit log must really be written.** Python's default level dropped every `audit.info` line. Have a test that checks the line through the app's normal logging settings, without setting a level in the test.
- **Tests that depend on today's date.** A web test with a fixed payment date and a default window of the last 31 days started failing a month later. Give such tests a date relative to today.

## 72. Reconstructed tables checked against the old site's scripts — Python IvanKhamar/NewErpPython#6

- **Status:** TODO
- **Source:** the user's SQL scripts in `F:\PROJECTS\VIDRO\Vidro_DB` hold the original CREATE statements of tables we had guessed. Copy these sections from the Python repo's `db/reconstructed.sql` (after the PR is merged): "Склад розхідників" (`dd_stock_categories`, `dd_stock_suppliers`, `dd_stock_items`, `dd_stock_log`), "Партнери-перепродавці" (`dd_partner*`) and "Порядок товарів за продажами" (`dd_crosssell_*`, `dd_product_order_snapshot*`, `dd_product_manual_pin`). When items 43–52 are ported: `dd_product_description.model_info varchar(255) DEFAULT NULL` and `dd_post.date_available datetime NOT NULL DEFAULT CURRENT_TIMESTAMP`. The scripts also hold real API keys and names: copy only their DDL, never their INSERT and UPDATE lines.
- **Contract changes:** consumables (`dd_stock_items`, `dd_stock_log` and the stock placement, stocktaking and purchase order rules that feed them) keep **2 decimals**, not 3. The existing texts become "не більше 2 знаків після коми" (the `Scale` constant of stock stocktaking and `StockPlacement`). A manual adjustment with 3 decimals gets 400 "Зміна залишку: не більше 2 знаків після коми.". A production write-off (norm × pieces) is rounded half up to 2 decimals before it is taken. Production keeps 3 decimals, because its own tables are still guesses. Longer limits: item name 255, unit 32, stock category 128, supplier 255, partner brand ("Бренд") 128, partner product article ("Артикул партнера") 255.
- **Mapping changes:** `stock`/`min_stock`/`quantity`/`stock_before`/`stock_after` are `decimal(10,2)` and `price` is `decimal(15,2)`; `dd_product_order_snapshot_item` has its own auto-increment `id`, so `(snapshot_id, product_id)` is no longer the key; a NULL snapshot `note` reads as "". Foreign keys run from items to categories and suppliers, `ON DELETE SET NULL`.
- **Tests to port:** `tests/test_reconstructed_schema.py` (8 tests: 2 decimals in the domain and through the API, the half-up write-off, the name lengths, the foreign key, partner lengths, a snapshot that restores the order). Update the existing tests that send 3-decimal quantities to consumables.
- **Python files:** `db/reconstructed.sql`, `src/newerp/domain/stock.py`, `src/newerp/domain/production.py`, `src/newerp/domain/partners.py`, `src/newerp/infrastructure/stock/mapping.py`, `src/newerp/infrastructure/models/catalog.py`, `src/newerp/infrastructure/catalog/sort.py`.

## 83. Customers' photos in reviews — Python IvanKhamar/NewErpPython#7

- **Status:** TODO (after item 51, reviews moderation)
- **Table:** copy the "Фото у відгуках покупців" section (`dd_review_image`) from the Python repo's `db/reconstructed.sql`. Its DDL comes from the user's script in `Vidro_DB`, which also holds real data: copy only the DDL.
- **Contract:**
  - `GET /catalog/reviews`: each review gets `images: [{id, path, status}]`, with `status` `pending`, `published` or `rejected` (DB 0, 1, 2), ordered by `sort_order` then `image_id`, every status listed.
  - `PUT /catalog/reviews/{reviewId}/images/{imageId}` with body `{"status": ...}` returns 204; an unknown status returns 400; a photo that is missing or belongs to another review returns 404 "Фото {imageId} у відгуку {reviewId} немає.".
  - `DELETE /catalog/reviews/{reviewId}/images/{imageId}` returns 204, with the same 404.
  - `DELETE /catalog/reviews/{id}` also deletes the review's photos.
  - Same roles as the review endpoints.
- **Rules:** a file is deleted only when the picture folder setting is set and the path is inside `review_photos/`: it starts with `review_photos/`, has no `..` and no backslash, and the resolved path stays in the folder. Missing file: fine. A failed delete only logs a warning. The rows go first.
- **Web:** the reviews moderation page shows each photo with "Опублікувати", "Відхилити" and "Видалити"; images come from the storefront's `image/` URL only when the store address is https, else the path is shown.
- **Security:** audit log for status changes and deletes; never a file outside `review_photos/`.
- **Tests to port:** `tests/test_review_photos.py` (18).
- **Python files:** `src/newerp/application/catalog/feedback.py`, `src/newerp/infrastructure/catalog/feedback.py`, `src/newerp/infrastructure/catalog/pictures.py`, `src/newerp/api/catalog.py`, `src/newerp/web/content.py`, `src/newerp/web/templates/content/_tab.html`.
- **Nova Poshta senders and drop-off points** (`dd_np_senders`, `dd_np_dropoff_points` in the same scripts): Python keeps them in settings (`NEWERP_NOVA_POSHTA__SENDERS`, `__DROPOFF_POINTS`), because a sender's API key is a secret. Check that this repo does the same; don't create those tables.
