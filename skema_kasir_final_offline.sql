-- =========================================================================
-- SKEMA FINAL APLIKASI KASIR - EDISI FULL OFFLINE (SQLite >= 3.37)
-- =========================================================================
-- Aplikasi terpisah dari edisi online. Satu usaha, satu toko, satu database lokal,
-- tanpa server, tanpa sinkronisasi, tanpa gudang.
--
-- KONVENSI (PENTING - berbeda dari edisi online karena SQLite)
--  * id          : UUID string (TEXT), sama dengan edisi online agar data bisa diimpor.
--  * UANG        : INTEGER dalam rupiah penuh (tanpa sen). Contoh: Rp 103.230 -> 103230.
--                  Pembulatan (mis. PPN) dilakukan aplikasi sebelum disimpan.
--                  Alasan: tipe NUMERIC SQLite berupa floating point, rawan selisih pada SUM().
--  * KUANTITAS   : INTEGER dalam 1/1000 satuan. Contoh: 1 Pcs -> 1000, 2,5 Kg -> 2500.
--                  Berlaku untuk semua kolom quantity, stok, conversion_factor, threshold.
--  * TARIF       : REAL dalam persen (11.0 = 11%).
--  * WAKTU       : TEXT ISO-8601 UTC, mis. '2026-10-04T07:30:00.000Z'.
--  * TANGGAL     : TEXT 'YYYY-MM-DD' (tanggal lokal usaha), mis. expenses.expense_date.
--  * BOOLEAN     : INTEGER 0/1.
--  * Laporan harian: kelompokkan pakai waktu lokal, contoh
--      date(created_at, '+' || (SELECT utc_offset_minutes FROM app_settings) || ' minutes')
--  * updated_at  : otomatis diperbarui oleh trigger (bagian 7).
--  * Wajib dijalankan pada SETIAP koneksi: PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;
--  * Nama tabel dan kolom dibuat semirip mungkin dengan edisi online (lihat bagian 10: pemetaan migrasi).
--
-- FITUR EDISI INI: kasir (multi-satuan, harga bertingkat, jasa opsional, PPN opsional, poin),
-- master produk, kategori, pelanggan, supplier (opsional), shift + modal kembalian, stok dan
-- Update Stok (termasuk barang masuk sederhana), barang rusak, pengeluaran, laporan, user management,
-- role + hak akses per user, printer, backup/restore, lisensi,
-- penanda produk (baru / harga naik / harga turun).
-- TIDAK ADA: gudang, penerimaan barang berdokumen, transfer, expired/batch, multi-cabang,
-- langganan, sinkronisasi.
-- =========================================================================

PRAGMA journal_mode = WAL;   -- persisten di file database
PRAGMA foreign_keys = ON;    -- hanya berlaku untuk koneksi ini; aplikasi wajib mengulanginya


-- =========================================================================
-- 1. IDENTITAS USAHA, PENGATURAN, & LISENSI (masing-masing satu baris)
-- =========================================================================

CREATE TABLE app_meta
(
    key        TEXT NOT NULL PRIMARY KEY,   -- schema_version, edition, installation_id, last_backup_at, dst.
    value      TEXT,
    updated_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
) STRICT;

CREATE TABLE business_profile
(
    id             TEXT NOT NULL PRIMARY KEY CHECK (id = 'main'),
    name           TEXT NOT NULL,
    owner_name     TEXT NOT NULL,
    phone_number   TEXT,
    address        TEXT,
    tax_number     TEXT,   -- NPWP, ditampilkan di struk jika PPN aktif
    receipt_footer TEXT,
    logo_path      TEXT,
    created_at     TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at     TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
) STRICT;

CREATE TABLE app_settings
(
    id                   TEXT    NOT NULL PRIMARY KEY CHECK (id = 'main'),
    -- Pajak / PPN (opsional, dibebankan ke customer)
    tax_enabled          INTEGER NOT NULL DEFAULT 0 CHECK (tax_enabled IN (0, 1)),
    tax_name             TEXT    NOT NULL DEFAULT 'PPN',
    tax_rate             REAL    NOT NULL DEFAULT 11.0 CHECK (tax_rate >= 0 AND tax_rate <= 100),
    tax_inclusive        INTEGER NOT NULL DEFAULT 0 CHECK (tax_inclusive IN (0, 1)), -- 1: harga sudah termasuk pajak
    -- Jasa (opsional: kelontong OFF, bengkel ON)
    service_fee_enabled  INTEGER NOT NULL DEFAULT 0 CHECK (service_fee_enabled IN (0, 1)),
    allow_negative_stock INTEGER NOT NULL DEFAULT 0 CHECK (allow_negative_stock IN (0, 1)),
    cash_rounding        INTEGER NOT NULL DEFAULT 0 CHECK (cash_rounding >= 0),     -- mis. 100; 0 = tanpa pembulatan
    auto_print_receipt   INTEGER NOT NULL DEFAULT 1 CHECK (auto_print_receipt IN (0, 1)),
    -- Penanda (badge) produk di kasir/daftar produk
    new_product_badge_days INTEGER NOT NULL DEFAULT 7 CHECK (new_product_badge_days >= 0), -- 0 = nonaktif
    price_trend_badge_days INTEGER NOT NULL DEFAULT 7 CHECK (price_trend_badge_days >= 0), -- 0 = nonaktif
    invoice_prefix       TEXT    NOT NULL DEFAULT 'INV',
    timezone             TEXT    NOT NULL DEFAULT 'Asia/Jakarta',
    utc_offset_minutes   INTEGER NOT NULL DEFAULT 420,  -- WIB 420, WITA 480, WIT 540
    created_at           TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at           TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
) STRICT;

-- Lisensi untuk pelanggan yang membeli putus (tanpa langganan).
CREATE TABLE app_license
(
    id                 TEXT NOT NULL PRIMARY KEY CHECK (id = 'main'),
    license_key        TEXT,
    licensed_to        TEXT,
    edition            TEXT NOT NULL DEFAULT 'OFFLINE' CHECK (edition IN ('OFFLINE')),
    status             TEXT NOT NULL DEFAULT 'TRIAL' CHECK (status IN ('TRIAL', 'ACTIVE', 'EXPIRED', 'REVOKED')),
    device_fingerprint TEXT,
    activated_at       TEXT,
    expires_at         TEXT,   -- NULL = berlaku selamanya
    created_at         TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at         TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
) STRICT;


-- =========================================================================
-- 2. PENGGUNA & HAK AKSES (RBAC)
-- =========================================================================
-- Izin efektif user = role_permissions UNION user_permissions(is_granted=1)
--                     dikurangi user_permissions(is_granted=0)

CREATE TABLE permissions
(
    permission_key TEXT NOT NULL PRIMARY KEY,
    name           TEXT NOT NULL,
    module         TEXT NOT NULL,
    description    TEXT
) STRICT;

CREATE TABLE roles
(
    id          TEXT    NOT NULL PRIMARY KEY,
    name        TEXT    NOT NULL,
    level_tier  INTEGER NOT NULL,
    description TEXT,
    is_system   INTEGER NOT NULL DEFAULT 0 CHECK (is_system IN (0, 1)), -- Owner, Admin, Kasir (bisa dikustom)
    created_at  TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at  TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    deleted_at  TEXT
) STRICT;

CREATE TABLE users
(
    id            TEXT    NOT NULL PRIMARY KEY,
    role_id       TEXT    NOT NULL REFERENCES roles (id) ON DELETE RESTRICT,
    employee_code TEXT,
    name          TEXT    NOT NULL,
    username      TEXT    NOT NULL,   -- login offline; email boleh kosong
    email         TEXT,               -- wajib diisi saat migrasi ke edisi online
    password_hash TEXT    NOT NULL,
    pin_hash      TEXT,               -- PIN cepat untuk ganti shift / otorisasi
    phone_number  TEXT,
    address       TEXT,
    photo_path    TEXT,
    join_date     TEXT,
    notes         TEXT,
    is_active     INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0, 1)),
    last_login_at TEXT,
    created_at    TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at    TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    deleted_at    TEXT
) STRICT;

CREATE TABLE role_permissions
(
    role_id        TEXT NOT NULL REFERENCES roles (id) ON DELETE CASCADE,
    permission_key TEXT NOT NULL REFERENCES permissions (permission_key) ON DELETE CASCADE,
    updated_at     TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    deleted_at     TEXT,
    PRIMARY KEY (role_id, permission_key)
) STRICT, WITHOUT ROWID;

CREATE TABLE user_permissions
(
    user_id            TEXT    NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    permission_key     TEXT    NOT NULL REFERENCES permissions (permission_key) ON DELETE CASCADE,
    is_granted         INTEGER NOT NULL DEFAULT 1 CHECK (is_granted IN (0, 1)),
    granted_by_user_id TEXT REFERENCES users (id) ON DELETE SET NULL,
    updated_at         TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    deleted_at         TEXT,
    PRIMARY KEY (user_id, permission_key)
) STRICT, WITHOUT ROWID;


-- =========================================================================
-- 3. MASTER DATA
-- =========================================================================

CREATE TABLE categories
(
    id         TEXT NOT NULL PRIMARY KEY,
    name       TEXT NOT NULL,
    created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    deleted_at TEXT
) STRICT;

CREATE TABLE units
(
    id            TEXT    NOT NULL PRIMARY KEY,
    name          TEXT    NOT NULL,
    description   TEXT,
    allow_decimal INTEGER NOT NULL DEFAULT 0 CHECK (allow_decimal IN (0, 1)), -- 1 untuk Kg, Liter, Meter, dst.
    created_at    TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at    TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    deleted_at    TEXT
) STRICT;

CREATE TABLE products
(
    id                  TEXT    NOT NULL PRIMARY KEY,
    category_id         TEXT REFERENCES categories (id) ON DELETE SET NULL,
    base_unit_id        TEXT    NOT NULL REFERENCES units (id) ON DELETE RESTRICT,
    sku                 TEXT    NOT NULL,
    name                TEXT    NOT NULL,
    description         TEXT,
    image_path          TEXT,
    -- HPP per satuan dasar (rupiah). Rata-rata bergerak, diperbarui saat barang masuk dengan harga beli.
    -- Hanya tampil bagi yang punya izin product.view_cost_price.
    base_cost_price     INTEGER NOT NULL DEFAULT 0 CHECK (base_cost_price >= 0),
    -- Jasa default (mis. jasa pasang ban). NULL = tidak ada. Di kasir bisa diedit atau dihapus.
    default_service_fee INTEGER CHECK (default_service_fee IS NULL OR default_service_fee >= 0),
    track_stock         INTEGER NOT NULL DEFAULT 1 CHECK (track_stock IN (0, 1)),
    -- Penanda "produk baru di input" (cache). Diset 1 saat produk dibuat, lalu aplikasi
    -- menonaktifkannya sesuai app_settings.new_product_badge_days.
    is_new_product      INTEGER NOT NULL DEFAULT 1 CHECK (is_new_product IN (0, 1)),
    is_active           INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0, 1)),
    created_at          TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at          TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    deleted_at          TEXT
) STRICT;

-- Satuan jual per produk. conversion_factor (x1000) = isi per satuan dasar.
-- Contoh: dasar Pcs, Dus isi 12 -> 12000. Satuan dasar -> 1000.
CREATE TABLE product_units
(
    id                TEXT    NOT NULL PRIMARY KEY,
    product_id        TEXT    NOT NULL REFERENCES products (id) ON DELETE CASCADE,
    unit_id           TEXT    NOT NULL REFERENCES units (id) ON DELETE RESTRICT,
    barcode           TEXT,
    conversion_factor INTEGER NOT NULL DEFAULT 1000 CHECK (conversion_factor > 0),
    is_base_unit      INTEGER NOT NULL DEFAULT 0 CHECK (is_base_unit IN (0, 1)),
    created_at        TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at        TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    deleted_at        TEXT,
    UNIQUE (product_id, unit_id)
) STRICT;

-- Harga jual dasar per satuan (harga eceran / normal).
CREATE TABLE product_prices
(
    id              TEXT    NOT NULL PRIMARY KEY,
    product_unit_id TEXT    NOT NULL UNIQUE REFERENCES product_units (id) ON DELETE CASCADE,
    selling_price   INTEGER NOT NULL CHECK (selling_price >= 0),
    -- Tren harga jual terakhir (cache untuk badge di kasir/daftar produk).
    --   price_trend            : arah perubahan terakhir vs harga sebelumnya
    --   previous_selling_price : harga jual sebelum perubahan terakhir
    --   price_changed_at       : waktu perubahan terakhir; UI menampilkan badge bila
    --                            selisih waktu <= app_settings.price_trend_badge_days
    price_trend            TEXT    NOT NULL DEFAULT 'STABLE' CHECK (price_trend IN ('STABLE', 'UP', 'DOWN')),
    previous_selling_price INTEGER CHECK (previous_selling_price IS NULL OR previous_selling_price >= 0),
    price_changed_at       TEXT,
    created_at      TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at      TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    deleted_at      TEXT
) STRICT;

-- Harga bertingkat per range qty pada satuan yang sama.
-- Aturan: pakai tier dengan minimum_quantity terbesar yang masih <= qty. Di bawah tier terendah = harga dasar.
CREATE TABLE product_price_tiers
(
    id               TEXT    NOT NULL PRIMARY KEY,
    product_price_id TEXT    NOT NULL REFERENCES product_prices (id) ON DELETE CASCADE,
    tier_name        TEXT    NOT NULL,
    minimum_quantity INTEGER NOT NULL DEFAULT 1000 CHECK (minimum_quantity > 0),
    tier_price       INTEGER NOT NULL CHECK (tier_price >= 0),
    created_at       TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at       TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    deleted_at       TEXT
) STRICT;

-- Supplier (opsional, hanya data master). Sales yang menjual di supplier_contacts.
CREATE TABLE suppliers
(
    id           TEXT NOT NULL PRIMARY KEY,
    name         TEXT NOT NULL,   -- nama perusahaan
    phone_number TEXT NOT NULL,
    email        TEXT,
    address      TEXT,
    notes        TEXT,
    created_at   TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at   TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    deleted_at   TEXT
) STRICT;

CREATE TABLE supplier_contacts
(
    id           TEXT    NOT NULL PRIMARY KEY,
    supplier_id  TEXT    NOT NULL REFERENCES suppliers (id) ON DELETE CASCADE,
    name         TEXT    NOT NULL,   -- nama sales
    phone_number TEXT,
    position     TEXT,
    is_primary   INTEGER NOT NULL DEFAULT 0 CHECK (is_primary IN (0, 1)),
    created_at   TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at   TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    deleted_at   TEXT
) STRICT;

CREATE TABLE expense_categories
(
    id          TEXT    NOT NULL PRIMARY KEY,
    name        TEXT    NOT NULL,   -- Gaji, Listrik, Air, Bensin, Pembelian Stok, dst.
    description TEXT,
    is_system   INTEGER NOT NULL DEFAULT 0 CHECK (is_system IN (0, 1)),
    created_at  TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at  TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    deleted_at  TEXT
) STRICT;

CREATE TABLE customers
(
    id             TEXT    NOT NULL PRIMARY KEY,
    member_code    TEXT,
    name           TEXT    NOT NULL,
    phone_number   TEXT    NOT NULL,
    email          TEXT,
    address        TEXT,
    notes          TEXT,
    -- Saldo poin = cache dari customer_point_ledger. Sumber kebenaran tetap buku besar poin.
    loyalty_points INTEGER NOT NULL DEFAULT 0,
    created_at     TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at     TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    deleted_at     TEXT
) STRICT;

-- Pengaturan program poin (menu Pengaturan Poin). Satu aturan aktif.
--   Poin didapat : total belanja / amount_per_point (dibulatkan ke bawah), jika belanja >= min_transaction_amount
--   Nilai tukar  : 1 poin = point_value_in_cash rupiah
CREATE TABLE loyalty_point_rules
(
    id                     TEXT    NOT NULL PRIMARY KEY,
    min_transaction_amount INTEGER NOT NULL DEFAULT 0 CHECK (min_transaction_amount >= 0),
    amount_per_point       INTEGER NOT NULL DEFAULT 10000 CHECK (amount_per_point > 0),
    point_value_in_cash    INTEGER NOT NULL DEFAULT 100 CHECK (point_value_in_cash >= 0),
    min_redeem_points      INTEGER NOT NULL DEFAULT 0 CHECK (min_redeem_points >= 0),
    max_redeem_percent     REAL    NOT NULL DEFAULT 100.0 CHECK (max_redeem_percent >= 0 AND max_redeem_percent <= 100),
    is_active              INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0, 1)),
    created_at             TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at             TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    deleted_at             TEXT
) STRICT;

-- Barang penukar poin. Stok diambil dari stok produk (inventories) dan dicatat di stock_mutations (POINT_REWARD).
CREATE TABLE loyalty_reward_items
(
    id              TEXT    NOT NULL PRIMARY KEY,
    product_id      TEXT    NOT NULL REFERENCES products (id) ON DELETE CASCADE,
    product_unit_id TEXT REFERENCES product_units (id) ON DELETE SET NULL,
    reward_name     TEXT    NOT NULL,
    points_required INTEGER NOT NULL CHECK (points_required > 0),
    is_active       INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0, 1)),
    created_at      TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at      TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    deleted_at      TEXT
) STRICT;


-- =========================================================================
-- 4. STOK
-- =========================================================================

-- Satu baris per produk (hanya satu toko).
--   stock_quantity  : siap jual / didisplay (x1000, satuan dasar)
--   reject_quantity : rusak/reject yang masih di toko dan tidak didisplay
-- Saldo hanya boleh berubah bersamaan dengan satu baris stock_mutations (dalam satu transaksi database).
CREATE TABLE inventories
(
    id                  TEXT    NOT NULL PRIMARY KEY,
    product_id          TEXT    NOT NULL UNIQUE REFERENCES products (id) ON DELETE CASCADE,
    stock_quantity      INTEGER NOT NULL DEFAULT 0,   -- boleh negatif hanya jika app_settings.allow_negative_stock = 1
    reject_quantity     INTEGER NOT NULL DEFAULT 0 CHECK (reject_quantity >= 0),
    low_stock_threshold INTEGER NOT NULL DEFAULT 5000,
    last_mutation_at    TEXT,
    created_at          TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at          TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
) STRICT;

-- Menu "Update Stok": barang masuk sederhana (STOCK_IN) dan koreksi stok.
-- Karyawan memakai menu ini tanpa masuk Master Produk dan tanpa melihat HPP.
-- cost_price pada item hanya diisi oleh yang punya izin product.view_cost_price; jika diisi pada STOCK_IN,
-- aplikasi memperbarui HPP rata-rata bergerak produk.
CREATE TABLE stock_adjustments
(
    id                  TEXT NOT NULL PRIMARY KEY,
    user_id             TEXT NOT NULL REFERENCES users (id) ON DELETE RESTRICT,   -- yang menginput
    reason              TEXT NOT NULL DEFAULT 'STOCK_COUNT'
        CHECK (reason IN ('STOCK_IN', 'INPUT_ERROR', 'STOCK_COUNT', 'LOST', 'OTHER')),
    supplier_id         TEXT REFERENCES suppliers (id) ON DELETE SET NULL,           -- untuk STOCK_IN
    supplier_contact_id TEXT REFERENCES supplier_contacts (id) ON DELETE SET NULL,  -- sales yang mengantar
    received_by_user_id TEXT REFERENCES users (id) ON DELETE SET NULL,              -- penerima (default owner diatur aplikasi)
    reference_number    TEXT,                                                       -- no. surat jalan / nota
    notes               TEXT,
    created_at          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
) STRICT;

CREATE TABLE stock_adjustment_items
(
    id                  TEXT    NOT NULL PRIMARY KEY,
    stock_adjustment_id TEXT    NOT NULL REFERENCES stock_adjustments (id) ON DELETE CASCADE,
    product_id          TEXT    NOT NULL REFERENCES products (id) ON DELETE RESTRICT,
    product_unit_id     TEXT REFERENCES product_units (id) ON DELETE SET NULL,
    quantity_input      INTEGER,            -- qty yang diketik user pada satuan yang dipilih (x1000)
    quantity_before     INTEGER NOT NULL,   -- satuan dasar (x1000)
    quantity_after      INTEGER NOT NULL,
    difference          INTEGER NOT NULL,   -- quantity_after - quantity_before
    cost_price          INTEGER CHECK (cost_price IS NULL OR cost_price >= 0)       -- per satuan yang dipilih, opsional
) STRICT;

-- Barang rusak/reject. Alur status:
--   REJECTED_IN_STORE (masih di toko, tidak didisplay) -> RETURNED_TO_SUPPLIER / WRITTEN_OFF
CREATE TABLE product_reject_logs
(
    id                     TEXT    NOT NULL PRIMARY KEY,
    product_id             TEXT    NOT NULL REFERENCES products (id) ON DELETE RESTRICT,
    user_id                TEXT    NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    reject_reason          TEXT    NOT NULL,   -- RUSAK, CACAT, EXPIRED, HILANG, dst.
    status                 TEXT    NOT NULL DEFAULT 'REJECTED_IN_STORE'
        CHECK (status IN ('REJECTED_IN_STORE', 'RETURNED_TO_SUPPLIER', 'WRITTEN_OFF')),
    quantity               INTEGER NOT NULL CHECK (quantity > 0),   -- satuan dasar (x1000)
    cost_price_at_incident INTEGER NOT NULL,                        -- HPP per satuan dasar
    total_loss_amount      INTEGER NOT NULL,
    supplier_id            TEXT REFERENCES suppliers (id) ON DELETE SET NULL,
    resolved_at            TEXT,
    resolved_by_user_id    TEXT REFERENCES users (id) ON DELETE SET NULL,
    notes                  TEXT,
    created_at             TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at             TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
) STRICT;

-- Buku besar mutasi stok: siapa, kapan, berapa, stok sebelum/sesudah, dan dokumen sumbernya.
--   quantity      : selisih bertanda (+/-) dalam satuan dasar (x1000)
--   stock_bucket  : kolom saldo yang berubah (AVAILABLE = stock_quantity, REJECT = reject_quantity)
CREATE TABLE stock_mutations
(
    id              TEXT    NOT NULL PRIMARY KEY,
    product_id      TEXT    NOT NULL REFERENCES products (id) ON DELETE RESTRICT,
    product_unit_id TEXT REFERENCES product_units (id) ON DELETE SET NULL,
    supplier_id     TEXT REFERENCES suppliers (id) ON DELETE SET NULL,
    user_id         TEXT    NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    mutation_type   TEXT    NOT NULL CHECK (mutation_type IN
        ('SALE', 'SALE_VOID', 'STOCK_IN', 'ADJUSTMENT', 'REJECT', 'POINT_REWARD', 'OPENING')),
    stock_bucket    TEXT    NOT NULL DEFAULT 'AVAILABLE' CHECK (stock_bucket IN ('AVAILABLE', 'REJECT')),
    quantity        INTEGER NOT NULL,
    stock_before    INTEGER,
    stock_after     INTEGER,
    reference_type  TEXT CHECK (reference_type IN ('TRANSACTION', 'STOCK_ADJUSTMENT', 'REJECT_LOG', 'REDEMPTION')),
    reference_id    TEXT,
    notes           TEXT,
    created_at      TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
) STRICT;


-- =========================================================================
-- 5. PENJUALAN, KAS, POIN, & PENGELUARAN
-- =========================================================================

-- Ganti shift = tutup shift berjalan lalu buka shift baru (hanya satu shift OPEN pada satu waktu).
CREATE TABLE cashier_shifts
(
    id                       TEXT    NOT NULL PRIMARY KEY,
    user_id                  TEXT    NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    start_time               TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    end_time                 TEXT,
    cash_drawer_start        INTEGER NOT NULL DEFAULT 0 CHECK (cash_drawer_start >= 0),  -- modal uang kembalian
    cash_drawer_end_system   INTEGER DEFAULT 0,
    cash_drawer_end_physical INTEGER DEFAULT 0,
    discrepancy_amount       INTEGER DEFAULT 0,
    status                   TEXT    NOT NULL DEFAULT 'OPEN' CHECK (status IN ('OPEN', 'CLOSED')),
    notes                    TEXT,
    created_at               TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at               TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
) STRICT;

-- Rumus total:
--   total_gross_amount = items_amount + service_amount
--   total_net_amount   = total_gross_amount - discount_amount - point_discount_amount
--                        + (tax_amount jika pajak belum termasuk harga) + rounding_amount
-- Transaksi yang "ditahan" hanya disimpan di memori/antrean aplikasi dan baru ditulis saat dibayar.
CREATE TABLE transactions
(
    id                    TEXT    NOT NULL PRIMARY KEY,
    user_id               TEXT    NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    customer_id           TEXT REFERENCES customers (id) ON DELETE SET NULL,
    shift_id              TEXT    NOT NULL REFERENCES cashier_shifts (id) ON DELETE RESTRICT,
    invoice_number        TEXT    NOT NULL UNIQUE,   -- {invoice_prefix}-{yyyymmdd}-{urut}
    total_cost_price      INTEGER NOT NULL DEFAULT 0,
    items_amount          INTEGER NOT NULL DEFAULT 0,
    service_amount        INTEGER NOT NULL DEFAULT 0,
    total_gross_amount    INTEGER NOT NULL,
    discount_amount       INTEGER NOT NULL DEFAULT 0,
    point_discount_amount INTEGER NOT NULL DEFAULT 0,
    tax_rate              REAL    NOT NULL DEFAULT 0.0,
    tax_inclusive         INTEGER NOT NULL DEFAULT 0 CHECK (tax_inclusive IN (0, 1)),
    tax_amount            INTEGER NOT NULL DEFAULT 0,
    rounding_amount       INTEGER NOT NULL DEFAULT 0,
    total_net_amount      INTEGER NOT NULL,
    payment_method        TEXT    NOT NULL,
    payment_provider      TEXT,
    payment_reference     TEXT,
    amount_paid           INTEGER NOT NULL,
    amount_change         INTEGER NOT NULL DEFAULT 0,
    points_earned         INTEGER NOT NULL DEFAULT 0,
    points_used           INTEGER NOT NULL DEFAULT 0,
    status                TEXT    NOT NULL DEFAULT 'COMPLETED' CHECK (status IN ('COMPLETED', 'VOIDED', 'REFUNDED')),
    voided_at             TEXT,
    voided_by_user_id     TEXT REFERENCES users (id) ON DELETE SET NULL,
    void_reason           TEXT,
    notes                 TEXT,
    created_at            TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at            TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
) STRICT;

-- Detail transaksi.
--   PRODUCT : barang (satuan dan qty dipilih di kasir; harga mengikuti tier jika ada)
--   SERVICE : jasa (default dari products.default_service_fee, bebas diedit, tidak bergantung qty);
--             parent_item_id menunjuk baris barang yang dipasangkan jasanya
--   REWARD  : barang hasil penukaran poin (selling_price = 0)
CREATE TABLE transaction_items
(
    id                TEXT    NOT NULL PRIMARY KEY,
    transaction_id    TEXT    NOT NULL REFERENCES transactions (id) ON DELETE CASCADE,
    parent_item_id    TEXT REFERENCES transaction_items (id) ON DELETE CASCADE,
    item_type         TEXT    NOT NULL DEFAULT 'PRODUCT' CHECK (item_type IN ('PRODUCT', 'SERVICE', 'REWARD')),
    product_id        TEXT REFERENCES products (id) ON DELETE RESTRICT,
    product_unit_id   TEXT REFERENCES product_units (id) ON DELETE RESTRICT,
    description       TEXT,
    price_tier_id     TEXT REFERENCES product_price_tiers (id) ON DELETE SET NULL,
    quantity          INTEGER NOT NULL CHECK (quantity > 0),     -- x1000, pada satuan yang dijual
    conversion_factor INTEGER NOT NULL DEFAULT 1000,             -- snapshot, x1000
    quantity_base     INTEGER NOT NULL DEFAULT 0,                -- dalam satuan dasar, x1000
    cost_price        INTEGER NOT NULL DEFAULT 0,                -- HPP per satuan yang dijual (snapshot)
    selling_price     INTEGER NOT NULL,                          -- harga per satuan yang dijual (snapshot)
    discount_amount   INTEGER NOT NULL DEFAULT 0,
    subtotal_price    INTEGER NOT NULL,
    sort_order        INTEGER NOT NULL DEFAULT 0,
    CHECK (item_type = 'SERVICE' OR (product_id IS NOT NULL AND product_unit_id IS NOT NULL))
) STRICT;

CREATE TABLE customer_point_redemptions
(
    id                    TEXT    NOT NULL PRIMARY KEY,
    customer_id           TEXT    NOT NULL REFERENCES customers (id) ON DELETE RESTRICT,
    user_id               TEXT    NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    transaction_id        TEXT REFERENCES transactions (id) ON DELETE SET NULL,
    reward_item_id        TEXT REFERENCES loyalty_reward_items (id) ON DELETE SET NULL,
    redemption_type       TEXT    NOT NULL CHECK (redemption_type IN ('DISCOUNT', 'ITEM')),
    points_redeemed       INTEGER NOT NULL CHECK (points_redeemed > 0),   -- seluruh atau sebagian saldo
    equivalent_cash_value INTEGER NOT NULL DEFAULT 0,
    quantity_gifted       INTEGER NOT NULL DEFAULT 1,
    notes                 TEXT,
    created_at            TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
) STRICT;

-- Buku besar poin. points bertanda (+ didapat, - dipakai). user_id = pelaku (admin kasir).
CREATE TABLE customer_point_ledger
(
    id             TEXT    NOT NULL PRIMARY KEY,
    customer_id    TEXT    NOT NULL REFERENCES customers (id) ON DELETE RESTRICT,
    user_id        TEXT    NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    transaction_id TEXT REFERENCES transactions (id) ON DELETE SET NULL,
    redemption_id  TEXT REFERENCES customer_point_redemptions (id) ON DELETE SET NULL,
    entry_type     TEXT    NOT NULL CHECK (entry_type IN
        ('EARN', 'REDEEM_DISCOUNT', 'REDEEM_ITEM', 'ADJUST', 'EXPIRE', 'REVERSAL')),
    points         INTEGER NOT NULL CHECK (points <> 0),
    balance_after  INTEGER,
    notes          TEXT,
    created_at     TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
) STRICT;

CREATE TABLE expenses
(
    id                  TEXT    NOT NULL PRIMARY KEY,
    expense_category_id TEXT    NOT NULL REFERENCES expense_categories (id) ON DELETE RESTRICT,
    user_id             TEXT    NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    shift_id            TEXT REFERENCES cashier_shifts (id) ON DELETE SET NULL,
    supplier_id         TEXT REFERENCES suppliers (id) ON DELETE SET NULL,     -- jika beli stok
    employee_user_id    TEXT REFERENCES users (id) ON DELETE SET NULL,         -- jika gaji karyawan
    amount              INTEGER NOT NULL DEFAULT 0 CHECK (amount >= 0),
    expense_date        TEXT    NOT NULL DEFAULT (date('now', 'localtime')),
    payment_source      TEXT    NOT NULL CHECK (payment_source IN ('CASH_DRAWER', 'BANK', 'OWNER_FUNDS', 'OTHER')),
    receipt_image_path  TEXT,
    notes               TEXT,
    created_at          TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at          TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    deleted_at          TEXT
) STRICT;


-- =========================================================================
-- 6. AUDIT, PRINTER, BACKUP & RESTORE
-- =========================================================================

CREATE TABLE product_price_changelogs
(
    id              TEXT    NOT NULL PRIMARY KEY,
    product_id      TEXT REFERENCES products (id) ON DELETE CASCADE,
    product_unit_id TEXT REFERENCES product_units (id) ON DELETE CASCADE,
    user_id         TEXT    NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    price_type      TEXT    NOT NULL CHECK (price_type IN ('SELLING', 'TIER', 'COST', 'SERVICE_FEE')),
    old_price       INTEGER NOT NULL DEFAULT 0,
    new_price       INTEGER NOT NULL DEFAULT 0,
    notes           TEXT,
    created_at      TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
) STRICT;

CREATE TABLE activity_logs
(
    id          TEXT NOT NULL PRIMARY KEY,
    user_id     TEXT REFERENCES users (id) ON DELETE SET NULL,
    action_type TEXT NOT NULL,
    entity_type TEXT,
    entity_id   TEXT,
    description TEXT NOT NULL,
    payload     TEXT CHECK (payload IS NULL OR json_valid(payload)),
    created_at  TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
) STRICT;

CREATE TABLE printer_settings
(
    id               TEXT    NOT NULL PRIMARY KEY,
    name             TEXT    NOT NULL,
    printer_type     TEXT    NOT NULL DEFAULT 'THERMAL' CHECK (printer_type IN ('THERMAL', 'LASER', 'INKJET')),
    connection_type  TEXT    NOT NULL DEFAULT 'USB' CHECK (connection_type IN ('USB', 'NETWORK', 'BLUETOOTH', 'SYSTEM')),
    address          TEXT,
    paper_width_mm   INTEGER NOT NULL DEFAULT 80 CHECK (paper_width_mm IN (58, 80)),
    copies           INTEGER NOT NULL DEFAULT 1 CHECK (copies >= 1),
    auto_cut         INTEGER NOT NULL DEFAULT 1 CHECK (auto_cut IN (0, 1)),
    auto_open_drawer INTEGER NOT NULL DEFAULT 0 CHECK (auto_open_drawer IN (0, 1)),
    is_default       INTEGER NOT NULL DEFAULT 0 CHECK (is_default IN (0, 1)),
    created_at       TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at       TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    deleted_at       TEXT
) STRICT;

-- Jadwal backup otomatis (scheduler).
CREATE TABLE backup_schedules
(
    id               TEXT    NOT NULL PRIMARY KEY,
    frequency        TEXT    NOT NULL DEFAULT 'DAILY' CHECK (frequency IN ('HOURLY', 'DAILY', 'WEEKLY')),
    run_time         TEXT    NOT NULL DEFAULT '23:00',   -- HH:MM waktu lokal
    day_of_week      INTEGER CHECK (day_of_week BETWEEN 0 AND 6),
    retention_count  INTEGER NOT NULL DEFAULT 7 CHECK (retention_count >= 1),
    destination_path TEXT    NOT NULL,                   -- folder lokal / flashdisk / folder sinkron cloud
    is_active        INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0, 1)),
    last_run_at      TEXT,
    created_at       TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at       TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
) STRICT;

-- Riwayat backup (terjadwal maupun manual). Karena tidak ada server, backup adalah satu-satunya cadangan data.
CREATE TABLE backup_logs
(
    id                   TEXT NOT NULL PRIMARY KEY,
    schedule_id          TEXT REFERENCES backup_schedules (id) ON DELETE SET NULL,
    trigger_type         TEXT NOT NULL CHECK (trigger_type IN ('SCHEDULED', 'MANUAL')),
    triggered_by_user_id TEXT REFERENCES users (id) ON DELETE SET NULL,
    status               TEXT NOT NULL DEFAULT 'RUNNING' CHECK (status IN ('RUNNING', 'SUCCESS', 'FAILED')),
    file_name            TEXT,
    file_size_bytes      INTEGER,
    checksum_sha256      TEXT,
    destination          TEXT,
    error_message        TEXT,
    started_at           TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    finished_at          TEXT,
    created_at           TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
) STRICT;

CREATE TABLE restore_logs
(
    id                  TEXT NOT NULL PRIMARY KEY,
    user_id             TEXT REFERENCES users (id) ON DELETE SET NULL,
    source_file         TEXT NOT NULL,
    pre_restore_backup  TEXT,   -- nama file backup otomatis yang dibuat sebelum restore
    status              TEXT NOT NULL DEFAULT 'RUNNING' CHECK (status IN ('RUNNING', 'SUCCESS', 'FAILED')),
    error_message       TEXT,
    started_at          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    finished_at         TEXT
) STRICT;


-- =========================================================================
-- 7. TRIGGER: updated_at OTOMATIS
-- =========================================================================
-- @@TRIGGERS@@


-- =========================================================================
-- 8. PROTEKSI & INDEKS
-- =========================================================================

-- ---- Unique parsial (hanya data aktif) ----
CREATE UNIQUE INDEX idx_unique_username_active ON users (username) WHERE deleted_at IS NULL;
CREATE UNIQUE INDEX idx_unique_email_active ON users (email) WHERE deleted_at IS NULL AND email IS NOT NULL;
CREATE UNIQUE INDEX idx_unique_employee_code_active ON users (employee_code) WHERE deleted_at IS NULL AND employee_code IS NOT NULL;
CREATE UNIQUE INDEX idx_unique_role_name_active ON roles (name) WHERE deleted_at IS NULL;
CREATE UNIQUE INDEX idx_unique_sku_active ON products (sku) WHERE deleted_at IS NULL;
CREATE UNIQUE INDEX idx_unique_unit_active ON units (name) WHERE deleted_at IS NULL;
CREATE UNIQUE INDEX idx_unique_category_active ON categories (name) WHERE deleted_at IS NULL;
CREATE UNIQUE INDEX idx_unique_barcode_active ON product_units (barcode) WHERE deleted_at IS NULL AND barcode IS NOT NULL;
CREATE UNIQUE INDEX idx_unique_base_unit_per_product ON product_units (product_id) WHERE is_base_unit = 1 AND deleted_at IS NULL;
CREATE UNIQUE INDEX idx_unique_price_tier_qty ON product_price_tiers (product_price_id, minimum_quantity) WHERE deleted_at IS NULL;
CREATE UNIQUE INDEX idx_unique_customer_phone_active ON customers (phone_number) WHERE deleted_at IS NULL;
CREATE UNIQUE INDEX idx_unique_member_code_active ON customers (member_code) WHERE deleted_at IS NULL AND member_code IS NOT NULL;
CREATE UNIQUE INDEX idx_unique_supplier_active ON suppliers (name, phone_number) WHERE deleted_at IS NULL;
CREATE UNIQUE INDEX idx_unique_reward_item_active ON loyalty_reward_items (product_id) WHERE deleted_at IS NULL;
CREATE UNIQUE INDEX idx_unique_active_point_rule ON loyalty_point_rules (is_active) WHERE is_active = 1 AND deleted_at IS NULL;
CREATE UNIQUE INDEX idx_unique_open_shift ON cashier_shifts (status) WHERE status = 'OPEN';
CREATE UNIQUE INDEX idx_unique_default_printer ON printer_settings (is_default) WHERE is_default = 1 AND deleted_at IS NULL;

-- ---- Penanda produk (badge) ----
CREATE INDEX idx_products_new ON products (is_new_product, created_at) WHERE is_new_product = 1;
CREATE INDEX idx_product_prices_trend ON product_prices (price_trend, price_changed_at) WHERE price_trend <> 'STABLE';

-- ---- Master & relasi ----
CREATE INDEX idx_users_role ON users (role_id);
CREATE INDEX idx_products_category ON products (category_id);
CREATE INDEX idx_products_name ON products (name);
CREATE INDEX idx_product_units_product ON product_units (product_id);
CREATE INDEX idx_product_units_barcode ON product_units (barcode);
CREATE INDEX idx_price_tiers_lookup ON product_price_tiers (product_price_id, minimum_quantity);
CREATE INDEX idx_supplier_contacts_supplier ON supplier_contacts (supplier_id);
CREATE INDEX idx_customers_name ON customers (name);

-- ---- Stok ----
CREATE INDEX idx_adjustments_date ON stock_adjustments (created_at);
CREATE INDEX idx_adjustment_items_adjustment ON stock_adjustment_items (stock_adjustment_id);
CREATE INDEX idx_adjustment_items_product ON stock_adjustment_items (product_id);
CREATE INDEX idx_reject_logs_product ON product_reject_logs (product_id, reject_reason);
CREATE INDEX idx_reject_logs_status ON product_reject_logs (status, created_at);
CREATE INDEX idx_stock_mutations_product ON stock_mutations (product_id, created_at);
CREATE INDEX idx_stock_mutations_type ON stock_mutations (mutation_type, created_at);
CREATE INDEX idx_stock_mutations_user ON stock_mutations (user_id, created_at);
CREATE INDEX idx_stock_mutations_ref ON stock_mutations (reference_type, reference_id);

-- ---- Penjualan, kas, poin, pengeluaran ----
CREATE INDEX idx_transactions_date ON transactions (created_at);
CREATE INDEX idx_transactions_status_date ON transactions (status, created_at);
CREATE INDEX idx_transactions_customer ON transactions (customer_id);
CREATE INDEX idx_transactions_shift ON transactions (shift_id);
CREATE INDEX idx_transactions_user ON transactions (user_id, created_at);
CREATE INDEX idx_transaction_items_trx ON transaction_items (transaction_id);
CREATE INDEX idx_transaction_items_product ON transaction_items (product_id);
CREATE INDEX idx_transaction_items_parent ON transaction_items (parent_item_id);
CREATE INDEX idx_cashier_shifts_user ON cashier_shifts (user_id);
CREATE INDEX idx_point_redemptions_customer ON customer_point_redemptions (customer_id);
CREATE INDEX idx_point_ledger_customer ON customer_point_ledger (customer_id, created_at);
CREATE INDEX idx_expenses_date ON expenses (expense_date);
CREATE INDEX idx_expenses_category ON expenses (expense_category_id, expense_date);

-- ---- Audit & backup ----
CREATE INDEX idx_logs_action ON activity_logs (action_type, created_at);
CREATE INDEX idx_logs_entity ON activity_logs (entity_type, entity_id);
CREATE INDEX idx_price_changelogs_unit ON product_price_changelogs (product_unit_id, created_at);
CREATE INDEX idx_backup_logs_date ON backup_logs (started_at);


-- =========================================================================
-- 9. DATA AWAL (SEED)
-- =========================================================================
-- Role bawaan (Owner, Admin, Kasir) dibuat aplikasi saat setup awal lalu dipetakan ke permission_key
-- di bawah lewat role_permissions. Owner bebas mengubahnya dan mengatur izin per karyawan (user_permissions).

INSERT INTO app_settings (id) VALUES ('main');
INSERT INTO app_meta (key, value) VALUES ('schema_version', '1'), ('edition', 'OFFLINE');

INSERT INTO permissions (permission_key, name, module) VALUES
    -- Penjualan & kasir
    ('sales.create',            'Membuat transaksi penjualan',             'SALES'),
    ('sales.edit_price',        'Mengubah harga saat transaksi',           'SALES'),
    ('sales.edit_service_fee',  'Mengubah/menghapus biaya jasa',           'SALES'),
    ('sales.discount',          'Memberi diskon manual',                   'SALES'),
    ('sales.void',              'Membatalkan transaksi',                   'SALES'),
    ('sales.view_history',      'Melihat riwayat transaksi',               'SALES'),
    ('shift.open',              'Membuka shift',                           'SHIFT'),
    ('shift.close',             'Menutup shift',                           'SHIFT'),
    ('shift.view_all',          'Melihat semua shift',                     'SHIFT'),
    -- Produk
    ('product.view',            'Melihat master produk',                   'PRODUCT'),
    ('product.create',          'Menambah produk',                         'PRODUCT'),
    ('product.update',          'Mengubah produk',                         'PRODUCT'),
    ('product.delete',          'Menghapus produk',                        'PRODUCT'),
    ('product.manage_price',    'Mengatur harga jual dan harga bertingkat','PRODUCT'),
    ('product.view_cost_price', 'Melihat HPP / harga beli',                'PRODUCT'),
    ('category.manage',         'Mengelola kategori produk',               'PRODUCT'),
    -- Stok
    ('stock.view',              'Melihat stok',                            'STOCK'),
    ('stock.update',            'Update stok (tanpa melihat HPP)',         'STOCK'),
    ('stock.receive',           'Mencatat barang masuk dari supplier',     'STOCK'),
    ('stock.view_history',      'Melihat riwayat mutasi stok',             'STOCK'),
    ('stock.reject',            'Mencatat barang rusak/reject',            'STOCK'),
    -- Supplier
    ('supplier.view',           'Melihat supplier',                        'SUPPLIER'),
    ('supplier.manage',         'Mengelola supplier dan sales',            'SUPPLIER'),
    -- Pelanggan & poin
    ('customer.view',           'Melihat pelanggan',                       'CUSTOMER'),
    ('customer.manage',         'Mengelola pelanggan',                     'CUSTOMER'),
    ('loyalty.input_points',    'Menginput poin pelanggan',                'LOYALTY'),
    ('loyalty.redeem',          'Menukar poin',                            'LOYALTY'),
    ('loyalty.settings',        'Mengatur program poin',                   'LOYALTY'),
    -- Pengeluaran
    ('expense.view',            'Melihat pengeluaran',                     'EXPENSE'),
    ('expense.create',          'Mencatat pengeluaran',                    'EXPENSE'),
    ('expense.update',          'Mengubah pengeluaran',                    'EXPENSE'),
    ('expense.delete',          'Menghapus pengeluaran',                   'EXPENSE'),
    -- Laporan
    ('report.sales',            'Laporan omzet dan penjualan',             'REPORT'),
    ('report.profit_loss',      'Laporan laba rugi',                       'REPORT'),
    ('report.top_products',     'Laporan barang terlaris',                 'REPORT'),
    ('report.stock',            'Laporan stok (menipis, habis, posisi)',   'REPORT'),
    ('report.rejects',          'Laporan barang rusak',                    'REPORT'),
    ('report.expenses',         'Laporan pengeluaran',                     'REPORT'),
    ('report.employees',        'Laporan karyawan',                        'REPORT'),
    -- Pengguna & pengaturan
    ('user.view',               'Melihat karyawan',                        'USER'),
    ('user.manage',             'Mengelola karyawan',                      'USER'),
    ('role.manage',             'Mengelola role dan hak akses',            'USER'),
    ('settings.tax',            'Mengatur pajak/PPN',                      'SETTINGS'),
    ('settings.service_fee',    'Mengaktifkan fitur jasa',                 'SETTINGS'),
    ('settings.printer',        'Mengatur printer',                        'SETTINGS'),
    ('settings.backup',         'Mengatur dan menjalankan backup',         'SETTINGS'),
    ('settings.restore',        'Memulihkan database dari backup',         'SETTINGS'),
    ('settings.license',        'Melihat dan mengaktifkan lisensi',        'SETTINGS'),
    ('audit.view',              'Melihat log aktivitas',                   'SETTINGS');

PRAGMA user_version = 1;


-- =========================================================================
-- 10. PEMETAAN MIGRASI KE EDISI ONLINE (referensi untuk skrip impor)
-- =========================================================================
-- Prinsip: id (UUID) dipertahankan; kolom yang tidak ada di offline diisi saat impor.
--
-- SKALA NILAI
--   uang       : offline INTEGER rupiah        -> online NUMERIC(14,2)   : nilai apa adanya (x.00)
--   kuantitas  : offline INTEGER x1000         -> online NUMERIC(14,3)   : bagi 1000
--   boolean    : 0/1 -> FALSE/TRUE;  waktu TEXT ISO UTC -> TIMESTAMP WITH TIME ZONE
--
-- TABEL
--   business_profile            -> companies + stores (1 toko) ; address/phone/receipt_footer -> stores
--   app_settings                -> store_settings (warehouse_mode = 'SAME_AS_STORE')
--   app_license                 -> (dibuang; diganti subscription_plans/companies)
--   product_prices              -> store_product_prices (store_id = toko hasil impor)
--   product_price_tiers.product_price_id -> store_product_price_id
--   inventories                 -> store_inventories (expired_quantity = 0)
--   expenses                    -> store_expenses (store_id diisi)
--   stock_adjustments(reason STOCK_IN) -> goods_receipts + goods_receipt_items (store_id tujuan, tanpa gudang)
--   stock_adjustments(lainnya)  -> stock_adjustments
--   stock_mutations             -> stock_mutations (store_id diisi; STOCK_IN -> PURCHASE_IN)
--   permission 'stock.receive'  -> 'warehouse.receive'
--   users.username              -> dipertahankan sebagai info; users.email WAJIB diisi untuk edisi online
--   kolom device_id / client_created_at / synced_at -> isi 'IMPORT_OFFLINE' / created_at / waktu impor
--   transactions.invoice_number -> dipertahankan (unik per perusahaan di online)
--   tabel lain bernama sama     -> salin langsung (+ company_id)
--   products.is_new_product     -> products.is_new_product (BOOL) : penanda produk baru
--   product_prices.price_trend  -> store_product_prices.price_trend (per toko)
--   product_prices.previous_selling_price -> store_product_prices.previous_selling_price
--   product_prices.price_changed_at       -> store_product_prices.price_changed_at
--   app_settings.new_product_badge_days / price_trend_badge_days -> store_settings (nama sama)
