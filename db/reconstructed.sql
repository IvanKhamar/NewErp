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
