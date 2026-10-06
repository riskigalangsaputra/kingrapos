# PRD Aplikasi Kasir Edisi Full Offline

**Tanggal:** 5 Oktober 2026  
**Penulis:** @Kingra  

---

## 1. Ringkasan Produk

Aplikasi kasir edisi full offline adalah aplikasi *point-of-sale* (POS) untuk satu usaha dengan satu toko. Aplikasi berjalan penuh di satu perangkat dengan database lokal SQLite (versi 3.37 atau lebih baru), tanpa server, tanpa sinkronisasi cloud, maupun pengelolaan multi-gudang. 

Aplikasi dijual putus melalui lisensi dan dikembangkan terpisah dari edisi online, namun menggunakan format ID UUID dan nama tabel yang semirip mungkin agar data dapat diimpor ke edisi online di kemudian hari.

### Masalah yang Diselesaikan
Pemilik usaha kecil seperti toko kelontong atau bengkel perlu mencatat penjualan, stok, kas, dan pengeluaran tanpa ketergantungan koneksi internet dan tanpa biaya langganan bulanan. Karena tidak ada server, semua data tersimpan di satu file database lokal, sehingga keandalan pencatatan dan fitur *backup* menjadi inti keunggulan produk.

### Nilai Utama
* **Transaksi Cepat:** Mendukung banyak satuan jual (Pcs, Dus, Kg) dan harga bertingkat.
* **Fitur Fleksibel:** Jasa dan PPN dapat diaktifkan sesuai jenis usaha (misal: fitur jasa dinonaktifkan untuk toko kelontong, tetapi diaktifkan untuk bengkel).
* **Kontrol Akses Karyawan:** Pembatasan hak akses per karyawan, termasuk menyembunyikan Harga Pokok Penjualan (HPP) dari kasir.
* **Integritas Keuangan & Stok:** Stok, kas shift, dan laba tercatat secara konsisten karena setiap perubahan saldo dicatat melalui buku besar mutasi.
* **Jalur Migrasi:** Dapat diimpor ke edisi online tanpa perlu melakukan *data entry* ulang.

---

## 2. Tujuan dan Metrik Keberhasilan

Produk dianggap berhasil jika pemilik usaha dapat menjalankan operasional toko harian sepenuhnya tanpa internet dan tanpa mengalami kehilangan data.

### Tujuan Produk
1. Kasir dapat menyelesaikan transaksi sepenuhnya secara *offline*.
2. Stok, kas, dan laba akurat hingga rupiah terkecil tanpa selisih pembulatan.
3. Pemilik memiliki kontrol penuh atas akses HPP, perubahan harga, dan pembatalan transaksi (*void*).
4. Data aman melalui mekanisme *backup* terjadwal dan dapat dipulihkan (*restore*).
5. Data dapat dipindahkan ke edisi online tanpa kehilangan riwayat transaksi.

### Metrik Keberhasilan

| Metrik | Target Usulan | Cara Ukur |
| :--- | :--- | :--- |
| Waktu transaksi 5 item (dari pemindaian hingga selesai) | $< 30$ detik | Uji pengguna langsung dengan kasir |
| Pencarian produk (pada katalog 10.000 produk) | $< 300$ ms | Uji performa otomatis |
| Selisih total laporan vs jumlah transaksi | $\text{Rp } 0$ | Uji rekonsiliasi otomatis |
| Saldo stok vs total mutasi stok | 100% sama | Uji rekonsiliasi otomatis |
| Backup terjadwal berhasil | $99\%$ dari jadwal | Log `backup_logs` berstatus `SUCCESS` |
| Restore dari backup valid | $100\%$ berhasil | Uji manual pada setiap rilis |
| Migrasi ke edisi online | $0$ baris data hilang | Skrip impor dan penghitungan jumlah baris |

---

## 3. Pengguna dan Peran

Tiga peran bawaan (*default roles*) dibuat saat *setup* awal dan dapat dikustomisasi oleh pemilik usaha. Hak akses tiap karyawan juga dapat ditambah atau dicabut secara individual.

| Peran | Target Pengguna | Kebutuhan Utama | Batasan Khas |
| :--- | :--- | :--- | :--- |
| **Owner** | Pemilik usaha | Laporan, harga, HPP, pengaturan, backup, lisensi, manajemen karyawan. | Mengatur izin untuk peran lain. |
| **Admin** | Orang kepercayaan pemilik | Mengelola produk, stok, pelanggan, pengeluaran, dan laporan operasional. | Hak akses ditentukan oleh pemilik. |
| **Kasir** | Karyawan toko | Transaksi, kelola shift, pencarian produk, pelanggan, update stok. | Tidak dapat melihat HPP; tidak dapat mengubah harga tanpa izin. |

> **Catatan Akun:** Every user menggunakan satu akun dengan *username* (email bersifat opsional) dan PIN cepat untuk pergantian shift atau otorisasi tindakan sensitif. Karyawan yang hanya bertugas menerima barang dapat menggunakan menu **Update Stok** tanpa masuk ke Master Produk dan tanpa melihat HPP.

---

## 4. Ruang Lingkup

Versi ini berfokus pada operasional satu toko secara *full offline*. Fitur multi-lokasi dan arsitektur *online-cloud* sengaja dikecualikan.

### Termasuk dalam Lingkup (In-Scope)
* **Kasir:** Multi-satuan, harga bertingkat, fitur jasa opsional, PPN opsional, diskon, poin *loyalty*, pembulatan kas, dan cetak struk.
* **Master Data:** Produk, kategori, satuan, pelanggan, dan supplier (opsional).
* **Shift Kasir:** Pencatatan kas harian dan modal kembalian.
* **Stok:** Update stok (termasuk penerimaan barang sederhana), pencatatan barang rusak (*reject*), dan mutasi stok.
* **Pengeluaran & Laporan:** Pencatatan biaya operasional dan laporan bisnis harian.
* **Manajemen Pengguna:** User, role, dan izin kustomisasi (*RBAC*).
* **Sistem:** Printer thermal/inkjet, backup/restore, dan manajemen lisensi.
* **Penanda Produk:** Label produk *Baru*, *Harga Naik*, dan *Harga Turun*.

### Tidak Termasuk dalam Lingkup (Out-of-Scope)
* Manajemen multi-gudang, penerimaan barang berdokumen formal (*purchase order* kompleks), dan transfer antar-gudang.
* Manajemen tanggal kadaluwarsa (*expiry date*) dan nomor *batch*.
* Dukungan multi-cabang / multi-toko.
* Fitur berlangganan bulanan dan sinkronisasi data antar perangkat.
* Server terpusat atau akses jarak jauh (*remote access*).

---

## 5. Kebutuhan Fungsional

*Skala Prioritas: $M$ = Must-have (Wajib pada rilis pertama), $S$ = Should-have (Sebaiknya ada).*

### Kasir dan Penjualan (KSR)
* **KSR-01 ($M$):** Transaksi menggunakan banyak satuan jual per produk. Jumlah kuantitas desimal hanya diperbolehkan jika satuan mendukung (misal: Kg, Liter, Meter).
* **KSR-02 ($M$):** Harga bertingkat (*tiered pricing*): Otomatis menerapkan tingkat harga berdasarkan kuantitas minimum yang dipenuhi; di bawah batas minimum terendah menggunakan harga dasar.
* **KSR-03 ($M$):** Pencarian produk berdasarkan nama, SKU, atau barcode per satuan.
* **KSR-04 ($M$):** Diskon manual per item maupun per transaksi (memerlukan izin khusus).
* **KSR-05 ($M$):** Pembayaran fleksibel (metode, provider, dan nomor referensi), penghitungan kembalian, dan pembulatan kas sesuai pengaturan.
* **KSR-06 ($M$):** Penomoran invoice otomatis dengan format `[PREFIX]-[YYYYMMDD]-[URUT]` dan bersifat unik.
* **KSR-07 ($M$):** Pembatalan transaksi (*void*) wajib menyertakan alasan dan identitas pencatat; stok dikembalikan dan poin pelanggan dibatalkan.
* **KSR-08 ($S$):** Biaya jasa opsional per baris barang; nilai awal diambil dari data produk, dapat diedit atau dihapus jika memiliki izin.
* **KSR-09 ($S$):** PPN opsional dengan nama, tarif, dan opsi harga (apakah sudah termasuk pajak atau belum).
* **KSR-10 ($S$):** Fitur *Hold Transaction* (Tahan Transaksi); data disimpan sementara di memori/antrean dan baru ditulis ke database saat dibayar.
* **KSR-11 ($M$):** Cetak struk otomatis (dapat dimatikan) mencakup logo, alamat, footer, dan NPWP (jika PPN aktif).

### Shift dan Kas (SHF)
* **SHF-01 ($M$):** Buka shift wajib menginput modal uang kembalian. Hanya satu shift berstatus `OPEN` yang diperbolehkan dalam satu waktu.
* **SHF-02 ($M$):** Tutup shift: Membandingkan kas sistem dengan hitungan fisik serta mencatat selisihnya.
* **SHF-03 ($S$):** Fitur ganti shift (menutup shift berjalan lalu membuka shift baru) dikonfirmasi menggunakan PIN.
* **SHF-04 ($S$):** Pencatatan pengeluaran kas dapat ditautkan secara langsung ke shift yang sedang aktif.

### Produk (PRD)
* **PRD-01 ($M$):** Kelola (CRUD) produk dengan SKU unik, kategori, satuan dasar, gambar, status aktif, dan opsi pelacakan stok.
* **PRD-02 ($M$):** Multi-satuan jual per produk yang dilengkapi faktor konversi, barcode, dan harga jual per satuan.
* **PRD-03 ($M$):** HPP per satuan dasar menggunakan metode *Moving Average* (Rata-rata Bergerak); hanya dapat dilihat oleh user berizin.
* **PRD-04 ($S$):** Riwayat perubahan harga (harga jual, tier harga, HPP, jasa) beserta identitas pembuat perubahan.
* **PRD-05 ($S$):** Penanda otomatis produk *Baru*, *Harga Naik*, dan *Harga Turun* (default durasi 7 hari; nilai 0 menonaktifkan penanda).
* **PRD-06 ($M$):** Pengelolaan kategori dan satuan dengan nama unik untuk data yang aktif.

### Stok (STK)
* **STK-01 ($M$):** Satu baris data stok per produk dengan pemisahan saldo *siap jual* dan saldo *reject*.
* **STK-02 ($M$):** Update Stok dengan opsi alasan: *Barang Masuk*, *Salah Input*, *Hitung Stok (Stock Opname)*, *Hilang*, atau *Lainnya*; mencatat saldo awal, saldo akhir, dan selisih.
* **STK-03 ($M$):** Penerimaan barang sederhana yang mencatat supplier, nama sales, penerima, dan nomor nota; HPP otomatis diperbarui jika harga beli diisi.
* **STK-04 ($M$):** Setiap perubahan saldo stok wajib menghasilkan satu baris riwayat buku besar (*stock mutation*) dalam satu transaksi database atomik.
* **STK-05 ($M$):** Peringatan stok menipis berdasarkan ambang batas per produk (default: 5 satuan). Stok negatif hanya diperbolehkan jika diizinkan di pengaturan.
* **STK-06 ($S$):** Penanganan barang rusak: Dialokasikan ke `REJECTED_IN_STORE`, kemudian dapat diproses ke `RETURNED_TO_SUPPLIER` atau `WRITTEN_OFF` (dihapus sebagai kerugian) berdasarkan HPP saat kejadian.

### Pelanggan, Poin, Supplier, dan Pengeluaran (CUS / LOY / SUP / EXP)
* **CUS-01 ($M$):** Master data pelanggan dengan nomor telepon unik dan kode member opsional.
* **LOY-01 ($S$):** Penghitungan poin perbelanjaan: $\text{Poin} = \lfloor \frac{\text{Total Belanja}}{\text{Nilai Per Poin}} \rfloor$ jika mencapai minimum belanja.
* **LOY-02 ($S$):** Penukaran poin menjadi diskon belanja dengan batas minimum penukaran dan persentase diskon maksimum per transaksi.
* **LOY-03 ($S$):** Penukaran poin dengan produk; stok berkurang dan tercatat sebagai transaksi `POINT_REWARD`.
* **LOY-04 ($S$):** Buku besar poin (*point ledger*) sebagai sumber kebenaran data (*source of truth*); saldo pada profil pelanggan hanya berupa *cache*.
* **SUP-01 ($S$):** Master supplier beserta kontak sales (data referensi opsional).
* **EXP-01 ($M$):** Pencatatan pengeluaran per kategori, pilihan sumber dana (kas laci, bank, dana pribadi, dll.), tanggal, foto nota, serta tautan opsional ke supplier atau karyawan (gaji).

### Laporan, Pengguna, dan Pengaturan (RPT / USR / SET)
* **RPT-01 ($M$):** Laporan omzet & penjualan, laba rugi, barang terlaris, stok, barang rusak, pengeluaran, dan kinerja karyawan. Tiap laporan memiliki batasan izin terpisah.
* **RPT-02 ($M$):** Pengelompokan laporan harian menggunakan zona waktu operasional usaha, bukan standar UTC.
* **USR-01 ($M$):** Autentikasi lokal/offline menggunakan *username* & *password*, serta PIN cepat untuk otorisasi tindakan khusus.
* **USR-02 ($M$):** Role-Based Access Control (RBAC): Hak akses efektif dihitung dari kombinasi role bawaan dan pengecualian (*override*) per pengguna.
* **SET-01 ($M$):** Pengaturan konfigurasional: Pajak, biaya jasa, pembulatan kas, prefix invoice, dan zona waktu usaha.
* **SET-02 ($S$):** Pengaturan printer thermal, laser, atau inkjet via USB, LAN, Bluetooth, atau driver OS (ukuran 58mm/80mm, *auto-cut*, dan pemicu laci kasir).
* **SET-03 ($M$):** Backup otomatis/manual, restore data, manajemen lisensi aplikasi, dan catatan audit log.

---

## 6. Aturan Bisnis dan Rumus

Untuk menghindari kesalahan akumulasi pecahan floating-point, **semua nilai mata uang disimpan sebagai bilangan bulat Rupiah** dan **semua kuantitas disimpan sebagai bilangan bulat per seperseribu satuan** ($1/1000$).

### Total Transaksi
$$\text{Total Kotor} = \text{Total Barang} + \text{Total Jasa}$$

$$\text{Total Bersih} = \text{Total Kotor} - \text{Diskon} - \text{Diskon Poin} + \text{PPN (jika eksklusif)} + \text{Pembulatan}$$

### Konversi Satuan
$$\text{Qty Dasar} = \frac{\text{Qty} \times \text{Faktor Konversi}}{1000}$$

*Contoh:* 1 Dus berisi 12 Pcs.
* Input: Qty = 1 (disimpan `1000`), Faktor Konversi = 12 (disimpan `12000`).
* Hasil Qty Dasar = $\frac{1000 \times 12000}{1000} = 12000$ ($12 \text{ Pcs}$).

### HPP Rata-Rata Bergerak (Moving Average)
$$\text{HPP Baru} = \frac{(\text{Stok Lama} \times \text{HPP Lama}) + (\text{Qty Masuk} \times \text{Harga Beli})}{\text{Stok Lama} + \text{Qty Masuk}}$$

### Aturan Lainnya
1. **Perhitungan Poin:** Poin diperoleh dari total belanja dibagi nilai per poin (dibulatkan ke bawah) jika memenuhi syarat minimum transaksi.
2. **Penukaran Poin:** Nilai tukar poin tidak boleh melebihi persentase maksimum total transaksi yang diizinkan dan harus memenuhi syarat minimum poin tukar.
3. **Integritas Stok:** Perubahan saldo stok wajib dibarengi dengan pembuatan 1 baris riwayat mutasi stok dalam transaksi database yang sama (*atomic transaction*).
4. **Stok Negatif:** Hanya diizinkan apabila opsi `allow_negative_stock` pada pengaturan aktif.
5. **Void Transaksi:** Mengubah status transaksi menjadi `VOIDED`, mencatat koreksi mutasi stok berjenis `SALE_VOID`, dan mencatat pembalikan poin berjenis `REVERSAL`.
6. **Imutabilitas Laporan:** Harga jual, HPP, dan faktor konversi disalin langsung (*snapshot*) ke baris detail transaksi saat terjadi penjualan. Perubahan harga di masa depan tidak akan mengubah riwayat transaksi lama.
7. **Penanda Produk:** Label *Baru* aktif sejak produk dibuat sampai batas hari yang ditentukan. Label tren harga (*Naik/Turun*) aktif jika rentang waktu perubahan harga terakhir masih dalam durasi hari yang ditentukan.
8. **Soft Delete:** Data tidak dihapus permanen. Kolom `deleted_at` digunakan untuk menandai data non-aktif. Keunikan nama/SKU/kode hanya berlaku untuk data yang aktif (`deleted_at IS NULL`).
9. **Single Rule Constraint:** Hanya 1 aturan poin loyalty dan 1 printer default yang boleh aktif dalam satu waktu.

---

## 7. Hak Akses (RBAC)

Aplikasi memiliki total **49 izin (permissions)** yang terbagi dalam **11 modul**.

### Formula Hak Akses Efektif
$$\text{Izin Efektif} = (\text{Izin Role} \cup \text{Izin Diberikan ke User}) \setminus \text{Izin Dicabut dari User}$$

### Ringkasan Modul & Hak Akses

| Modul | Jumlah Izin | Contoh Kode Izin |
| :--- | :---: | :--- |
| **SALES** | 6 | `sales.create`, `sales.void`, `sales.edit_price` |
| **SHIFT** | 3 | `shift.open`, `shift.close`, `shift.view_all` |
| **PRODUCT** | 7 | `product.manage_price`, `product.view_cost_price` |
| **STOCK** | 5 | `stock.update`, `stock.receive`, `stock.reject` |
| **SUPPLIER** | 2 | `supplier.view`, `supplier.manage` |
| **CUSTOMER** | 2 | `customer.view`, `customer.manage` |
| **LOYALTY** | 3 | `loyalty.input_points`, `loyalty.redeem`, `loyalty.settings` |
| **EXPENSE** | 4 | `expense.create`, `expense.update`, `expense.delete` |
| **REPORT** | 7 | `report.profit_loss`, `report.stock`, `report.employees` |
| **USER** | 3 | `user.manage`, `role.manage` |
| **SETTINGS** | 7 | `settings.backup`, `settings.restore`, `settings.license`, `audit.view` |

### Catatan Keamanan Hak Akses
* **Izin Sensitif:** `product.view_cost_price` berfungsi menyembunyikan HPP dan nilai margin/laba dari karyawan. Izin `sales.void`, `sales.edit_price`, dan `settings.restore` idealnya hanya diberikan kepada Owner atau Admin.
* **Role Sistem:** Peran bawaan (`Owner`, `Admin`, `Kasir`) adalah role sistem. Pemilik dapat menyesuaikan izin pada role ini atau memberikan pengecualian khusus per user.

---

## 8. Kebutuhan Non-Fungsional

Mengingat database lokal SQLite merupakan satu-satunya tempat penyimpanan data usaha, **keandalan penyimpanan dan sistem backup menjadi prioritas utama**.

| Kategori | Kebutuhan & Spesifikasi |
| :--- | :--- |
| **Offline** | Seluruh fungsi berjalan $100\%$ tanpa jaringan internet, server luar, atau akun cloud. |
| **Penyimpanan** | Database SQLite versi 3.37 atau lebih baru, menggunakan tabel `STRICT` dan mode `WAL` (*Write-Ahead Logging*). |
| **Integritas** | Koneksi wajib mengaktifkan `foreign_keys = ON` dan `$busy\_timeout = 5000$`. Perubahan stok, kas, dan poin wajib diselesaikan dalam 1 transaksi DB atomik. |
| **Kinerja** | Pencarian produk $< 300\text{ ms}$ pada 10.000 katalog produk. Eksekusi simpan transaksi perbelanjaan $< 1\text{ detik}$. |
| **Backup** | Penjadwalan per jam, harian, atau mingguan. Retensi bawaan 7 salinan terakhir. Lokasi tujuan: folder lokal, flashdisk, atau folder terhubung cloud sync. Dilengkapi checksum SHA-256 dan catatan riwayat di `backup_logs`. |
| **Restore** | Sistem membuat backup otomatis darurat tepat sebelum proses restore dijalankan. Riwayat dicatat pada `restore_logs`. |
| **Keamanan** | *Password* dan PIN disimpan dalam bentuk *hash* yang aman. Hak akses diverifikasi pada setiap aksi sensitif. Setiap aktivitas dicatat di *audit log*. |
| **Lisensi** | Status lisensi: `TRIAL`, `ACTIVE`, `EXPIRED`, atau `REVOKED`. Terikat pada *hardware fingerprint* perangkat. Masa aktif dapat diatur tanpa batas (*lifetime*). |
| **Waktu** | Waktu disimpan dalam format UTC. Laporan harian disesuaikan berdasarkan *offset* zona waktu usaha (WIB = +420, WITA = +480, WIT = +540 menit). |
| **Printer** | Format struk 58mm atau 80mm. Kegagalan proses cetak tidak boleh membatalkan transaksi yang telah berhasil disimpan ke database. |
| **Bahasa & Kas** | Antarmuka berbahasa Indonesia dengan standar mata uang Rupiah (tanpa desimal/sen). |

---

## 9. Model Data dan Konvensi

Skema database terdiri dari **38 tabel** yang terbagi ke dalam 6 kelompok utama. Nama tabel dan kolom diselaraskan dengan edisi online untuk kemudahan migrasi.

### Kelompok Tabel
1. **Identitas & Pengaturan (4):** `app_meta`, `business_profile`, `app_settings`, `app_license`
2. **Pengguna & Hak Akses (5):** `permissions`, `roles`, `users`, `role_permissions`, `user_permissions`
3. **Master Data (12):** `categories`, `units`, `products`, `product_units`, `product_prices`, `product_price_tiers`, `suppliers`, `supplier_contacts`, `expense_categories`, `customers`, `loyalty_point_rules`, `loyalty_reward_items`
4. **Stok (5):** `inventories`, `stock_adjustments`, `stock_adjustment_items`, `product_reject_logs`, `stock_mutations`
5. **Penjualan & Kas (6):** `cashier_shifts`, `transactions`, `transaction_items`, `customer_point_redemptions`, `customer_point_ledger`, `expenses`
6. **Audit & Sistem (6):** `product_price_changelogs`, `activity_logs`, `printer_settings`, `backup_schedules`, `backup_logs`, `restore_logs`

### Konvensi Data

| Jenis Data | Format & Konvensi |
| :--- | :--- |
| **ID** | UUID v4 (Format Teks/String) |
| **Mata Uang** | Bilangan bulat (Integer Rupiah). Contoh: $\text{Rp } 103.230 \rightarrow `103230`$ |
| **Kuantitas** | Bilangan bulat (Integer $\times 1000$). Contoh: $2,5\text{ Kg} \rightarrow `2500`$ |
| **Tarif / Persentase**| Angka desimal persen. Contoh: $11.0 \rightarrow 11\%$ |
| **Waktu** | ISO-8601 UTC String. Contoh: `2026-10-04T07:30:00.000Z` |
| **Tanggal Usaha** | Format Teks `YYYY-MM-DD` |
| **Boolean** | Integer `0` (False) atau `1` (True) |
| **Pembaruan** | Kolom `updated_at` diperbarui otomatis melalui database trigger. |

> **Catatan:** Tabel `app_settings`, `business_profile`, dan `app_license` masing-masing hanya berisi **1 baris data** dengan ID tetap `main`.

---

## 10. Alur Pengguna Utama

```
[ Setup Awal ] ──> [ Buka Shift ] ──> [ Transaksi Penjualan ] ──> [ Tutup Shift ]
                          │
                          ├──> [ Update Stok / Barang Masuk ]
                          ├──> [ Penanganan Barang Rusak ]
                          └──> [ Backup & Restore Data ]
```

1. **Setup Awal Usaha:**
   * Pemilik mengisi profil toko, zona waktu, serta preferensi PPN dan biaya jasa.
   * Sistem menginisialisasi role bawaan (`Owner`, `Admin`, `Kasir`) serta akun utama pemilik.
   * Pemilik memasukkan lisensi, menghubungkan printer, dan mengaktifkan jadwal backup.
   * Pemilik menambahkan master data (kategori, satuan, dan produk). Stok awal dimasukkan sebagai penyesuaian berjenis `OPENING`.

2. **Alur Transaksi Penjualan:**
   * Kasir membuka shift baru dengan memasukkan saldo awal modal kembalian.
   * Kasir memindai barcode atau mencari nama produk, menentukan satuan dan kuantitas (harga bertingkat terpasang otomatis).
   * Kasir menambahkan item jasa, diskon, data pelanggan, atau penukaran poin jika ada.
   * Sistem menghitung total kotor, PPN, pembulatan, dan total bersih.
   * Kasir memilih metode pembayaran dan memasukkan jumlah uang bayar.
   * Dalam 1 transaksi DB atomik: Sistem menyimpan data transaksi, mengurangi saldo stok, mencatat mutasi stok, dan menambahkan poin pelanggan.
   * Printer mencetak struk belanja.

3. **Tutup / Ganti Shift:**
   * Kasir menghitung fisik uang tunai di laci kasir.
   * Aplikasi menampilkan kas sistem, menghitung selisih, dan menutup shift (`CLOSED`).
   * Untuk pergantian shift, kasir berikutnya dapat membuka shift baru menggunakan otorisasi PIN.

4. **Barang Masuk & Update Stok:**
   * Karyawan membuka menu Update Stok, memilih alasan (misal: *Barang Masuk*).
   * Mengisi referensi supplier, nama sales, nomor nota, serta memilih daftar produk beserta jumlahnya.
   * User berizin mengisi harga beli sehingga sistem memperbarui HPP rata-rata produk.
   * Sistem menyimpan penyesuaian stok dan menerbitkan riwayat mutasi stok.

5. **Pengelolaan Barang Rusak (*Reject*):**
   * Karyawan mencatat kuantitas dan alasan kerusakan produk.
   * Stok barang berpindah dari saldo *siap jual* ke saldo *reject*.
   * Pemilik menentukan keputusan akhir: dikembalikan ke supplier (`RETURNED_TO_SUPPLIER`) atau dihapus sebagai kerugian (`WRITTEN_OFF`).

6. **Backup dan Restore:**
   * Aplikasi menjalankan pembuatan file backup secara otomatis sesuai jadwal.
   * Saat proses restore dijalankan, aplikasi secara otomatis membuat *snapshot backup darurat* terlebih dahulu sebelum menimpa database dengan file cadangan.

---

## 11. Migrasi ke Edisi Online

Data dari edisi offline dapat diimpor ke edisi online melalui skrip migrasi tanpa mengubah nilai ID UUID.

### Pemetaan dan Transformasi Data

| Data Offline | Tujuan Edisi Online | Transformasi / Penyesuaian |
| :--- | :--- | :--- |
| **Uang** (Integer Rp) | `NUMERIC(14,2)` | Nilai tetap, dikonversi ke bentuk desimal (`x.00`). |
| **Kuantitas** ($\times 1000$) | `NUMERIC(14,3)` | Nilai dibagi 1000. |
| **Boolean** (`0` / `1`) | `BOOLEAN` | Dikonversi ke `FALSE` atau `TRUE`. |
| **Waktu Teks** (ISO UTC) | `TIMESTAMP WITH TIME ZONE` | Dikonversi ke tipe timestamp PostgreSQL/Online. |
| `business_profile` | `companies` & `stores` | Menjadi 1 entitas perusahaan & 1 toko hasil impor. |
| `app_settings` | `store_settings` | Kolom `warehouse_mode` otomatis diisi `SAME_AS_STORE`. |
| `product_prices`, `price_tiers`| `store_product_prices` | `store_id` diisi dengan ID toko hasil impor. |
| `inventories` | `store_inventories` | Kolom `expired_quantity` diisi `0`. |
| `expenses` | `store_expenses` | `store_id` diisi dengan ID toko hasil impor. |
| `stock_adjustments` (`STOCK_IN`) | `goods_receipts` & `items` | Ditransformasi ke dokumen penerimaan barang toko. |
| `stock_mutations` | `stock_mutations` | `store_id` diisi; tipe `STOCK_IN` menjadi `PURCHASE_IN`. |
| Izin `stock.receive` | Izin `warehouse.receive` | Pemetaan penamaan izin. |
| `app_license` | *(Dibuang)* | Digantikan dengan mekanisme paket langganan online. |

### Prasyarat Migrasi
* Seluruh pengguna wajib memiliki alamat email sebelum proses migrasi dilakukan.
* Kolom metadata migrasi pada edisi online diisi: `device_id = 'IMPORT_OFFLINE'`, `client_created_at = created_at`, dan `synced_at = [WAKTU_IMPOR]`.
* Nomor invoice dipertahankan dan dipastikan unik per entitas perusahaan pada sistem online.

---

## 12. Asumsi, Risiko, dan Pertanyaan Terbuka

### Asumsi Pengembangan
1. Satu perangkat digunakan secara bergantian oleh karyawan (tidak ada dua instans aplikasi kasir yang mengakses database SQLite yang sama secara bersamaan).
2. Pengguna tidak memiliki server lokal. Keamanan backup sangat bergantung pada media penyimpan eksternal (flashdisk) atau folder yang tersinkronisasi dengan cloud storage pihak ketiga.
3. Target angka performa dan prioritas pada dokumen ini merupakan draf awal.

### Analisis Risiko & Mitigasi

| Risiko | Dampak | Mitigasi |
| :--- | :--- | :--- |
| **Media penyimpanan (Disk) rusak atau hilang** | Seluruh data usaha hilang | Backup otomatis berkala wajib diaktifkan; peringatan jika backup terakhir sudah terlalu lama; pengujian restore berkala. |
| **Aplikasi dibuka bersamaan di banyak proses** | Database SQLite terkunci (*database locked*) | Menggunakan mode SQLite WAL dan setelan `busy_timeout`; membatasi 1 shift `OPEN` per perangkat. |
| **Selisih zona waktu lokal** | Laporan harian salah tanggal | Menggunakan `utc_offset_minutes` pada setiap agregasi laporan harian; menyelaraskan tanggal pengeluaran. |
| **Karyawan melihat HPP / mengubah harga** | Kebocoran margin usaha | Pembatasan izin `product.view_cost_price` dan `sales.edit_price`; pencatatan aktivitas di audit log. |
| **Penggantian perangkat menyebabkan lisensi terblokir** | Kasir tidak dapat beroperasi | Menyediakan alur *transfer license* resmi dan masa toleransi tenggang (*grace period*). |
| **Skema database trigger belum komprehensif** | Kolom `updated_at` tidak terbarui | Memastikan pengujian seluruh DB trigger selesai sebelum tahap implementasi kode aplikasi. |

### Pertanyaan Terbuka (Perlu Keputusan)
1. Platform utama aplikasi: Apakah akan dikembangkan sebagai aplikasi Desktop (Windows) atau Mobile (Android)?
2. Alur Pengembalian Uang (*Refund*): Bagaimana skenario detailnya mengingat status `REFUNDED` ada tetapi belum ada tabel modul retur?
3. Perhitungan Poin Loyalty: Apakah dihitung dari Total Kotor atau Total Bersih (setelah potongan diskon dan PPN)?
4. Potensi Double Counting pada Laporan Laba Rugi: Apakah pengeluaran kategori *Pembelian Stok* dan input stok masuk (`STOCK_IN`) dihitung terpisah agar tidak ganda?
5. Pengaruh Pengeluaran Kas Laci: Apakah pengeluaran dengan sumber dana kas laci secara otomatis mengurangi perhitungan saldo fisik kas saat tutup shift?
6. Perilaku Masa Kadaluwarsa Lisensi: Saat lisensi `EXPIRED` atau `TRIAL` habis, apakah aplikasi menjadi *Read-Only* atau terkunci total?
7. Formulasi Selisih Kas Shift: Apakah rumus selisih adalah $\text{Uang Fisik} - \text{Kas Sistem}$ atau sebaliknya?
8. Formula PPN Inklusif: Apakah PPN inklusif menggunakan rumus $\text{Total} \times \frac{\text{Tarif}}{100 + \text{Tarif}}$?
9. Data Pelanggan Anonim: Apakah pelanggan umum (*Walk-in Customer*) diperbolehkan tanpa nomor telepon, mengingat kolom nomor telepon disyaratkan unik?

---

## 13. Rencana Rilis dan Kriteria Penerimaan

Rencana pengembangan dibagi menjadi **5 Fase Utama** berdasarkan ketergantungan struktur data.

### Rencana Fase Rilis

| Fase | Cakupan Modul | Kriteria Selesai Fase |
| :---: | :--- | :--- |
| **Fase 1: Fondasi** | Skema DB, setup awal, user, role, permission, lisensi, backup & restore. | Database terbentuk lengkap dengan seluruh tabel/indeks; pengujian backup dan restore $100\%$ berhasil. |
| **Fase 2: Master & Stok** | Kategori, satuan, produk, multi-satuan, harga bertingkat, supplier, update stok, barang rusak. | Saldo stok sama dengan akumulasi total mutasi pada uji rekonsiliasi data. |
| **Fase 3: Kasir & Shift** | Transaksi, biaya jasa, PPN, diskon, pembayaran, cetak struk, shift kasir, void. | Total transaksi sesuai dengan rumus; aturan 1 shift `OPEN` terjaga; printer berhasil mencetak. |
| **Fase 4: Poin & Laporan** | Program loyalty, penukaran poin, pengeluaran, seluruh modul laporan, penanda produk. | Laporan harian akurat sesuai zona waktu; saldo poin pelanggan sesuai dengan buku besar poin. |
| **Fase 5: Migrasi & Hardening** | Skrip impor ke edisi online, uji performa, dan pengujian volume data besar. | $0$ baris data hilang pada skrip migrasi; seluruh target metrik performa tercapai. |

### Kriteria Penerimaan Umum (*General Acceptance Criteria*)
* Semua kebutuhan fungsional berkategori **Must-Have ($M$)** lolos uji tanpa bug kritis.
* Tidak ada selisih nominal Rupiah antara total di laporan harian dengan rekapitulasi data transaksi.
* Seluruh fitur utama berjalan penuh tanpa membutuhkan koneksi internet.
* Pengguna tanpa izin `product.view_cost_price` tidak dapat melihat angka HPP dan margin laba pada layar, struk, maupun laporan.
* Seluruh poin pada *Pertanyaan Terbuka* telah disepakati dan ditandatangani oleh pemangku kepentingan.