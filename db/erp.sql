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
  -- Змінюється при зміні ролі чи пароля; cookie зі старим значенням одразу перестає діяти.
  `security_stamp` varchar(36) NOT NULL,
  -- Пароль видав адмін (тимчасовий), користувач має змінити його сам.
  `must_change_password` tinyint(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Замовлення постачальнику розхідників. У старій системі такого немає, тож таблиці наші.
-- status: draft (чернетка), ordered (надіслано постачальнику), received (усе отримано), cancelled.
-- supplier_id і item_id посилаються на dd_stock_suppliers / dd_stock_items старого сайту; зовнішніх ключів немає,
-- бо рушій тих таблиць на справжньому сайті невідомий.
CREATE TABLE `erp_purchase_order` (
  `id` int NOT NULL AUTO_INCREMENT,
  `supplier_id` int NOT NULL,
  `status` varchar(16) NOT NULL,
  `comment` varchar(500) NOT NULL DEFAULT '',
  `created_by_user_id` int NOT NULL,
  `created_at` datetime NOT NULL,
  `ordered_at` datetime NULL,
  `closed_at` datetime NULL,
  PRIMARY KEY (`id`),
  KEY `ix_erp_purchase_order_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Рядок замовлення: скільки замовлено, за якою ціною і скільки вже отримано (приймати можна частинами).
CREATE TABLE `erp_purchase_order_line` (
  `id` int NOT NULL AUTO_INCREMENT,
  `order_id` int NOT NULL,
  `item_id` int NOT NULL,
  `quantity` decimal(12,3) NOT NULL,
  `price` decimal(15,4) NOT NULL DEFAULT 0,
  `received_quantity` decimal(12,3) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  KEY `ix_erp_purchase_order_line_order` (`order_id`),
  KEY `ix_erp_purchase_order_line_item` (`item_id`),
  CONSTRAINT `fk_erp_purchase_order_line_order` FOREIGN KEY (`order_id`) REFERENCES `erp_purchase_order` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Хто й від якого відправника створив ТТН у NewErp. Старий сайт пише в dd_novaposhta_document сам ключ API відправника;
-- NewErp ключів у базі не зберігає, а пише сюди id відправника з налаштувань (NovaPoshta:Senders), за яким потім бере ключ
-- для друку й видалення. shipment_id посилається на dd_novaposhta_document.id (зовнішнього ключа немає: таблиця реконструйована).
CREATE TABLE `erp_shipment_sender` (
  `shipment_id` int NOT NULL,
  `sender_id` varchar(32) NOT NULL,
  `created_by_user_id` int NOT NULL,
  `created_at` datetime NOT NULL,
  PRIMARY KEY (`shipment_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Залишок розхідника на окремому складі (склади — довідник dd_warehouse старої адмінки).
-- dd_stock_items.stock лишається загальним залишком: з ним працюють старий сайт, замовлення, виробництво й інвентаризація.
-- Основний склад — це решта: загальний залишок мінус усе, що лежить на складах цієї таблиці, тому окремого рядка не має.
CREATE TABLE `erp_stock_location` (
  `id` int NOT NULL AUTO_INCREMENT,
  `item_id` int NOT NULL,
  `warehouse_id` int NOT NULL,
  `quantity` decimal(12,3) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  UNIQUE KEY `ux_erp_stock_location_item_warehouse` (`item_id`, `warehouse_id`),
  KEY `ix_erp_stock_location_warehouse` (`warehouse_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Переміщення розхідника між складами. NULL у from/to — основний склад. Загальний залишок не змінюється,
-- тому переміщення не пишуться в dd_stock_log.
CREATE TABLE `erp_stock_transfer` (
  `id` int NOT NULL AUTO_INCREMENT,
  `item_id` int NOT NULL,
  `from_warehouse_id` int NULL,
  `to_warehouse_id` int NULL,
  `quantity` decimal(12,3) NOT NULL,
  `comment` varchar(255) NOT NULL DEFAULT '',
  `user_id` int NOT NULL,
  `username` varchar(20) NOT NULL DEFAULT '',
  `created_at` datetime NOT NULL,
  PRIMARY KEY (`id`),
  KEY `ix_erp_stock_transfer_item` (`item_id`, `created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
