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
