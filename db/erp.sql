-- Власні таблиці NewErp (префікс erp_). Їх немає в старій системі.
-- InnoDB, щоб працювали транзакції й зовнішні ключі.
-- Застосовувати після db/schema.sql і db/reconstructed.sql.
-- Склад і виробництво живуть у таблицях старого сайту dd_stock_* (див. reconstructed.sql),
-- щоб NewErp і старий сайт працювали з одними даними, поки модулі переносяться.

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

-- Вхід у NewErp: хеш пароля й роль користувача (Admin, Manager, Storekeeper).
-- Сам користувач (логін, ім'я, активність) лишається в dd_user, тут лише те, чого там немає.
-- Зовнішнього ключа на dd_user немає, бо та таблиця MyISAM і ключів не підтримує.
CREATE TABLE `erp_user_login` (
  `user_id` int NOT NULL,
  `password_hash` varchar(255) NOT NULL,
  `role` varchar(32) NOT NULL,
  `last_login_at` datetime NULL,
  PRIMARY KEY (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
