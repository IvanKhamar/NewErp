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

---

## Earlier Python-first items (23–34)

| # | Feature | Python commit | Status |
|---|---|---|---|
| 23 | Nova Poshta courier delivery to an address: `toDoor`, `streetRef`, `building`, `flat`; `GET /delivery/streets` | a958344 | DONE: `feat/py-order-delivery`, 2026-11-05 |
| 24 | Sale prices (`dd_product_special`, `percent`) in the product search of a new order: `price`, `regularPrice` | b660c33 | DONE: `feat/py-order-delivery`, 2026-11-05 |
| 25 | Coupon admin `/marketing/coupons` with products/categories and their `*_exclude` tables, usage history | 0d88a5b | DONE: `feat/py-coupons`, 2026-11-05 |
| 26 | Low-stock alerts: a daily SMS (`stock.alertPhones`, `alertTime`), `POST /stock/alerts/low-stock`, home panel | ab43938 | TODO |
| 27 | Consumables for the sewing plan `GET /production/plan/materials?scope=orders|minimum|all` | f672a8c | DONE here (Python ported it from .NET); only `scope=minimum|all` is new: TODO |
| 28 | Automatic bank payment matching after import and `POST /payments/bank/auto-match`; `autoLinked` | ddbc1c9 | TODO |
| 29 | A read-only check of the schema and the integrations against the real database and keys | 412e69a | TODO |
| 30 | UTM report `GET /reports/utm`, `GET /orders/{id}/remarketing` (dd_remarketing_orders) | 2435c3c | TODO |
| 31 | Reports `/reports/online`, `/product-views`, `/rewards`, `/taxes` | 6015add | TODO |
| 32 | "Different customers from one IP" (`sharedIp`) in the customer risk check | 6761c0f | DONE: `feat/py-security`, 2026-11-05 |
| 33 | The login throttle shared between instances (Redis); client IP only from trusted proxies | 167f2a3 | DONE: `feat/py-security`, 2026-11-05; `ConnectionStrings:Redis`, `TrustedProxies` |
| 34 | Login CSRF: a double-submit token on the login form | 167f2a3 | DONE: `feat/py-security`, 2026-11-05; no token here: login takes only JSON, which another site cannot send without CORS (test `Login_TakesOnlyJson`) |

## 35–42. Orders: the old admin's order page tools — Python 67f29fb

- **Status:** TODO
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

- **Status:** TODO
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
