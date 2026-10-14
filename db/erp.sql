-- Власні таблиці NewErp (префікс erp_). Їх немає в старій системі.
-- InnoDB, щоб працювали транзакції й зовнішні ключі.
-- Застосовувати після db/schema.sql і db/reconstructed.sql.

-- Менеджер, відповідальний за замовлення.
-- Окрема таблиця, щоб не додавати колонок у dd_order, структуру яких ми не знаємо.
CREATE TABLE `erp_order_manager` (
  `order_id` int NOT NULL,
  `user_id` int NOT NULL,
  `assigned_by_user_id` int NOT NULL,
  `assigned_at` datetime NOT NULL,
  PRIMARY KEY (`order_id`),
  KEY `ix_erp_order_manager_user` (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Склад: розхідники (аналог StockTrack зі старого сайту, структура наша).
CREATE TABLE `erp_stock_category` (
  `id` int NOT NULL AUTO_INCREMENT,
  `name` varchar(100) NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `ux_erp_stock_category_name` (`name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `erp_stock_supplier` (
  `id` int NOT NULL AUTO_INCREMENT,
  `name` varchar(150) NOT NULL,
  `phone` varchar(32) NULL,
  `note` varchar(500) NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `erp_stock_item` (
  `id` int NOT NULL AUTO_INCREMENT,
  `category_id` int NOT NULL,
  `supplier_id` int NULL,
  `name` varchar(150) NOT NULL,
  `unit` varchar(16) NOT NULL,
  `quantity` decimal(12,3) NOT NULL DEFAULT 0,
  `min_quantity` decimal(12,3) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  KEY `ix_erp_stock_item_category` (`category_id`),
  CONSTRAINT `fk_erp_stock_item_category` FOREIGN KEY (`category_id`) REFERENCES `erp_stock_category` (`id`),
  CONSTRAINT `fk_erp_stock_item_supplier` FOREIGN KEY (`supplier_id`) REFERENCES `erp_stock_supplier` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Журнал руху: кожна зміна залишку записується сюди.
CREATE TABLE `erp_stock_movement` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `item_id` int NOT NULL,
  `delta` decimal(12,3) NOT NULL,
  `quantity_after` decimal(12,3) NOT NULL,
  `reason` varchar(255) NOT NULL,
  `user_id` int NOT NULL,
  `created_at` datetime NOT NULL,
  PRIMARY KEY (`id`),
  KEY `ix_erp_stock_movement_item` (`item_id`, `created_at`),
  CONSTRAINT `fk_erp_stock_movement_item` FOREIGN KEY (`item_id`) REFERENCES `erp_stock_item` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
