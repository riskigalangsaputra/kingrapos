# KingraPOS

Aplikasi Point of Sale (POS) berbasis **.NET 8** dan **WPF**, dengan library UI **WPF-UI** (lepo.co) dan database lokal **SQLite** melalui **EF Core**. Produk dan aturan bisnisnya dijelaskan di [`docs/PRD.md`](docs/PRD.md).

## Arsitektur

Solusi memakai pemisahan berlapis (layered architecture) dengan arah dependency satu arah:

```
UI  ──▶  Application  ──▶  Domain
 │                          ▲
 └──▶  Infrastructure  ─────┘
```

| Project | Tanggung jawab | Referensi |
|---|---|---|
| `KingraPOS.Domain` | Entitas, enum (model murni, tanpa dependency) | — |
| `KingraPOS.Application` | Business logic / use case: setup, auth, RBAC, manajemen user | Domain |
| `KingraPOS.Infrastructure` | EF Core + SQLite, `DbContext`, skema, PBKDF2 hasher | Domain, Application |
| `KingraPOS.UI` | WPF — Views, ViewModels, host/DI, composition root | Application, Infrastructure |
| `tests/KingraPOS.Tests` | Uji xUnit (setup, auth, RBAC, hashing) | Application, Infrastructure |

> Project UI dinamai `KingraPOS.UI` (bukan `*.Wpf`) karena namespace berakhiran `.Wpf` bentrok dengan namespace `Wpf.Ui.Controls` milik WPF-UI saat kompilasi XAML.

## Struktur

```
src/
├─ KingraPOS.Domain/
│  ├─ Common/                     Entity (base, Id UUID string)
│  ├─ Entities/                   38 entity sesuai skema
│  └─ Enums/                      enum untuk kolom TEXT ber-CHECK
├─ KingraPOS.Application/
│  ├─ Abstractions/               kontrak persistence & service
│  ├─ Dtos/  Security/  Validation/  Services/
│  └─ DependencyInjection/        AddApplication()
├─ KingraPOS.Infrastructure/
│  ├─ Persistence/                DbContext, initializer, konverter, skrip
│  ├─ Security/                   Pbkdf2PasswordHasher
│  └─ DependencyInjection/        AddInfrastructure()
├─ KingraPOS.UI/
│  ├─ App.xaml(.cs)               host + DI + Serilog + alur startup
│  ├─ Services/                   session, navigasi
│  ├─ ViewModels/                 setup, login, shell, halaman
│  ├─ Views/                      SetupWindow, LoginWindow, shell + halaman
│  └─ appsettings.json            konfigurasi Serilog
└─ tests/KingraPOS.Tests/         uji xUnit
```

## Database (SQL-first)

File **`skema_kasir_final_offline.sql`** di root repo adalah **sumber kebenaran** skema. File itu di-embed ke assembly `KingraPOS.Infrastructure` dan **dijalankan otomatis** saat database belum ada — jadi `STRICT` table, `WITHOUT ROWID`, partial unique index, `CHECK`, dan seed dipakai apa adanya. EF Core hanya **memetakan** entity ke tabel; tidak memakai migrations/`EnsureCreated`.

- Lokasi database: `%LOCALAPPDATA%\KingraPOS\kingrapos.db`
- Versi skema dilacak lewat `PRAGMA user_version` (sekarang **1**); initializer idempoten.
- `PRAGMA journal_mode = WAL`, `foreign_keys = ON`, `busy_timeout = 5000` di-set setiap koneksi.
- Bagian trigger (`-- @@TRIGGERS@@`) pada file skema masih placeholder, sehingga trigger `updated_at` di-generate di `Scripts/triggers.sql` (27 tabel).
- Relasi FK dimodelkan eksplisit (tanpa navigation property) agar EF tahu urutan insert dan graf relasi.

### Konvensi pemetaan

| Skema SQLite | C# |
|---|---|
| `TEXT` PK UUID | `string` |
| `INTEGER` uang (rupiah) | `long` |
| `INTEGER` kuantitas (x1000) | `long` |
| `REAL` tarif/persen | `double` |
| `INTEGER` 0/1 | `bool` |
| `TEXT` ISO-8601 UTC | `DateTimeOffset` |
| `TEXT` `YYYY-MM-DD` | `DateOnly` |
| `TEXT` enum ber-CHECK | `enum` C# (nama anggota = nilai DB) |
| nama tabel/kolom | `snake_case` |

## Autentikasi & Hak Akses

- Password & PIN di-hash **PBKDF2-SHA256** (210.000 iterasi, salt acak 16 byte); format `pbkdf2$sha256$<iterasi>$<salt>$<hash>`.
- Izin efektif = `role_permissions` ∪ `user_permissions(is_granted=1)` − `user_permissions(is_granted=0)`, di-cache di session saat login.
- Tiga role sistem dibuat saat setup: **Owner** (semua izin), **Admin** (semua kecuali `settings.license`, `settings.restore`, `role.manage`), **Kasir** (transaksi, shift, stok dasar — tanpa HPP, ubah harga, void, dan pengaturan).
- Aksi sensitif diperiksa izinnya di layer Application (mis. membuat user butuh `user.manage`).
- Log diagnostik ke `%LOCALAPPDATA%\KingraPOS\logs`; audit aksi bisnis ke tabel `activity_logs`.

## Menjalankan & Menguji

```powershell
dotnet run --project src/KingraPOS.UI/KingraPOS.UI.csproj
dotnet test kingrapos.slnx
```

Saat pertama dijalankan dan `business_profile` masih kosong, aplikasi menampilkan **Setup Awal**, lalu **Login**, lalu shell utama.

## Catatan

- Repository per-agregate belum dibuat — `Application` memakai abstraksi `IKingraPosDbContext` (+ factory).
- Lisensi, backup/restore, dan editor role/permission belum dikerjakan (lanjutan Fase 1).
