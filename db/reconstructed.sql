-- РЕКОНСТРУКЦІЯ (наша), а не копія зі старого сайту.
-- Власні таблиці й колонки старої ERP, для яких DDL у коді не знайдено.
-- Структуру відновлено з того, які поля пише та читає PHP-код старого сайту.
-- Застосовувати після db/schema.sql.

-- Журнал змін статусів замовлення (у старій системі вже існувала).
CREATE TABLE `dd_order_status_log` (
  `order_status_log_id` int(11) NOT NULL AUTO_INCREMENT,
  `order_id` int(11) NOT NULL,
  `user_id` int(11) NOT NULL,
  `old_status_id` int(11) NOT NULL,
  `new_status_id` int(11) NOT NULL,
  `date_added` datetime NOT NULL,
  PRIMARY KEY (`order_status_log_id`),
  KEY `order_id` (`order_id`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

-- Довідник джерел замовлення (сайт, Instagram, дзвінок...), багатомовний як order_status.
CREATE TABLE `dd_order_source` (
  `order_source_id` int(11) NOT NULL AUTO_INCREMENT,
  `language_id` int(11) NOT NULL,
  `name` varchar(64) NOT NULL,
  PRIMARY KEY (`order_source_id`,`language_id`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

-- Власна колонка замовлення: джерело. 0 = не вказано.
ALTER TABLE `dd_order`
  ADD COLUMN `order_source_id` int(11) NOT NULL DEFAULT '0';

-- ═══ Склад розхідників (модуль StockTrack старого сайту) ═══
-- Колонки взято із запитів admin/model/tool/stocktrack.php. Типи, довжини й рушій вгадані:
-- DDL модуля в коді немає, тож їх треба звірити зі справжньою базою (SHOW CREATE TABLE).
-- InnoDB обрано, бо модуль створював таблиці сам, а MySQL 5.5+ без явного ENGINE створює InnoDB.

CREATE TABLE `dd_stock_categories` (
  `category_id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(100) NOT NULL,
  `sort_order` int(11) NOT NULL DEFAULT '0',
  PRIMARY KEY (`category_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `dd_stock_suppliers` (
  `supplier_id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(150) NOT NULL,
  `phone` varchar(32) NOT NULL DEFAULT '',
  `email` varchar(96) NOT NULL DEFAULT '',
  `categories` varchar(255) NOT NULL DEFAULT '',  -- вільний текст: що постачає
  `notes` text NOT NULL,
  PRIMARY KEY (`supplier_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- status = 0 означає «видалено»: старий модуль не видаляє позиції, а приховує.
CREATE TABLE `dd_stock_items` (
  `item_id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(150) NOT NULL,
  `category_id` int(11) NULL,
  `unit` varchar(16) NOT NULL DEFAULT 'шт.',
  `stock` decimal(12,3) NOT NULL DEFAULT '0.000',
  `min_stock` decimal(12,3) NOT NULL DEFAULT '0.000',
  `used_for` varchar(255) NOT NULL DEFAULT '',
  `supplier_id` int(11) NULL,
  `price` decimal(15,4) NOT NULL DEFAULT '0.0000',
  `status` tinyint(1) NOT NULL DEFAULT '1',
  PRIMARY KEY (`item_id`),
  KEY `category_id` (`category_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Журнал руху розхідників. action: 'use' (витрата) або 'restock' (прихід); quantity завжди додатна.
-- date_added старий модуль не задає явно, тож у справжній таблиці, ймовірно, DEFAULT CURRENT_TIMESTAMP.
CREATE TABLE `dd_stock_log` (
  `log_id` int(11) NOT NULL AUTO_INCREMENT,
  `item_id` int(11) NOT NULL,
  `item_name` varchar(150) NOT NULL,
  `user_id` int(11) NOT NULL DEFAULT '0',
  `username` varchar(20) NOT NULL DEFAULT '',
  `action` varchar(16) NOT NULL,
  `quantity` decimal(12,3) NOT NULL,
  `stock_before` decimal(12,3) NOT NULL,
  `stock_after` decimal(12,3) NOT NULL,
  `comment` varchar(255) NOT NULL DEFAULT '',
  `date_added` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`log_id`),
  KEY `item_id` (`item_id`, `date_added`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ═══ Виробництво (модуль StockTrack старого сайту) ═══
-- Так само відновлено із запитів admin/model/tool/stocktrack.php і catalog/controller/extension/event/stocktrack.php.
-- Типи, рушій і первинні ключі вгадані, звірити зі справжньою базою.

-- Модель виробу. type: 'clothing' (власний пошив) або 'ready_made' (готовий товар від постачальника).
-- min_total: якщо пошитих разом менше, модель потрапляє в список «треба шити». status = 0 означає «видалено».
CREATE TABLE `dd_stock_production` (
  `production_id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(150) NOT NULL,
  `type` varchar(32) NOT NULL DEFAULT 'clothing',
  `min_total` int(11) NOT NULL DEFAULT '50',
  `sort_order` int(11) NOT NULL DEFAULT '0',
  `status` tinyint(1) NOT NULL DEFAULT '1',
  PRIMARY KEY (`production_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Розмір моделі: скільки розкроєно (cut_stock) і пошито (sewn_stock).
-- oc_option_value_id зв'язує розмір з опцією товару OpenCart (dd_option_value).
CREATE TABLE `dd_stock_production_sizes` (
  `size_id` int(11) NOT NULL AUTO_INCREMENT,
  `production_id` int(11) NOT NULL,
  `size_name` varchar(32) NOT NULL,
  `percent` decimal(5,2) NOT NULL DEFAULT '0.00',
  `cut_stock` decimal(12,3) NOT NULL DEFAULT '0.000',
  `sewn_stock` decimal(12,3) NOT NULL DEFAULT '0.000',
  `sort_order` int(11) NOT NULL DEFAULT '0',
  `oc_option_value_id` int(11) NULL,
  PRIMARY KEY (`size_id`),
  KEY `production_id` (`production_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Товари OpenCart, прив'язані до моделі напряму.
CREATE TABLE `dd_stock_production_products` (
  `production_id` int(11) NOT NULL,
  `product_id` int(11) NOT NULL,
  PRIMARY KEY (`production_id`, `product_id`),
  KEY `product_id` (`product_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Прив'язка через категорію OpenCart: усі товари категорії, назва яких містить name_filter
-- (або точно дорівнює йому, якщо match_exact = 1). Порожній фільтр — уся категорія.
CREATE TABLE `dd_stock_production_categories` (
  `production_id` int(11) NOT NULL,
  `oc_category_id` int(11) NOT NULL,
  `name_filter` varchar(255) NOT NULL DEFAULT '',
  `match_exact` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`production_id`, `oc_category_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Компонент крою (деталь: перед, спинка, рукав...).
CREATE TABLE `dd_stock_production_components` (
  `component_id` int(11) NOT NULL AUTO_INCREMENT,
  `production_id` int(11) NOT NULL,
  `name` varchar(100) NOT NULL,
  `sort_order` int(11) NOT NULL DEFAULT '0',
  PRIMARY KEY (`component_id`),
  KEY `production_id` (`production_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Запас розкроєних деталей за розміром.
CREATE TABLE `dd_stock_production_component_stock` (
  `component_id` int(11) NOT NULL,
  `size_id` int(11) NOT NULL,
  `stock` decimal(12,3) NOT NULL DEFAULT '0.000',
  PRIMARY KEY (`component_id`, `size_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Специфікація крою: скільки розхідника йде на одну деталь.
-- size_id NULL — для всіх розмірів; рядок із конкретним size_id перекриває його для цього розміру.
-- bom_id — НАШ ключ: старий модуль ним не користується (пише через DELETE + INSERT за іменами полів),
-- тож якщо в справжній таблиці його немає, його можна додати без шкоди для старого сайту.
CREATE TABLE `dd_stock_production_component_bom` (
  `bom_id` int(11) NOT NULL AUTO_INCREMENT,
  `component_id` int(11) NOT NULL,
  `size_id` int(11) NULL,
  `item_id` int(11) NOT NULL,
  `qty_per_unit` decimal(12,4) NOT NULL,
  PRIMARY KEY (`bom_id`),
  KEY `component_id` (`component_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Специфікація пошиву: на один пошитий виріб ідуть деталі крою (component_id) і прямі розхідники (item_id: нитки, бірки).
-- Перекриття за size_id і ключ bom_id — як у специфікації крою.
CREATE TABLE `dd_stock_production_sewn_bom` (
  `bom_id` int(11) NOT NULL AUTO_INCREMENT,
  `production_id` int(11) NOT NULL,
  `size_id` int(11) NULL,
  `component_id` int(11) NULL,
  `item_id` int(11) NULL,
  `qty_per_unit` decimal(12,4) NOT NULL,
  PRIMARY KEY (`bom_id`),
  KEY `production_id` (`production_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Журнал виробництва: кожна зміна залишку крою (stage = 'cut') чи пошиву ('sewn') за розміром.
-- direction: 'in' (+Покроєно, +Пошито) або 'out' (В пошив, Відвантаження, замовлення).
-- Замовлення старий сайт пише з user_id = 0 і username = 'system'.
CREATE TABLE `dd_stock_production_log` (
  `log_id` int(11) NOT NULL AUTO_INCREMENT,
  `production_id` int(11) NOT NULL,
  `size_id` int(11) NOT NULL,
  `size_name` varchar(32) NOT NULL,
  `user_id` int(11) NOT NULL DEFAULT '0',
  `username` varchar(20) NOT NULL DEFAULT '',
  `stage` varchar(8) NOT NULL,
  `direction` varchar(8) NOT NULL,
  `quantity` decimal(12,3) NOT NULL,
  `stock_before` decimal(12,3) NOT NULL,
  `stock_after` decimal(12,3) NOT NULL,
  `comment` varchar(255) NOT NULL DEFAULT '',
  `date_added` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`log_id`),
  KEY `production_id` (`production_id`, `date_added`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ═══ Журнал SMS ═══
-- Відновлено з admin/model/tool/log.php (addSMSLog). pre_order_id — для передзамовлень, 0 якщо немає.
-- response — сирий текст відповіді SMS-сервісу. Типи вгадані, звірити.
CREATE TABLE `dd_sms_log` (
  `message_id` int(11) NOT NULL AUTO_INCREMENT,
  `order_id` int(11) NOT NULL DEFAULT '0',
  `pre_order_id` int(11) NOT NULL DEFAULT '0',
  `phone` varchar(32) NOT NULL,
  `text` text NOT NULL,
  `response` text NOT NULL,
  `date` datetime NOT NULL,
  PRIMARY KEY (`message_id`),
  KEY `order_id` (`order_id`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

-- Власна колонка статусу замовлення: колір у списку замовлень (#rrggbb). Є в справжній базі (видно в phpMyAdmin),
-- у стандартному OpenCart її немає. Тип вгаданий.
ALTER TABLE `dd_order_status`
  ADD COLUMN `color` varchar(7) NOT NULL DEFAULT '';
