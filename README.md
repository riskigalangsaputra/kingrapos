# KingraPOS

Aplikasi Point of Sale (POS) berbasis **.NET 8** dan **WPF**, dengan library UI **WPF-UI** (lepo.co) dan database lokal **SQLite** melalui **EF Core**.

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
| `KingraPOS.Application` | Business logic / use case (belum diisi) | Domain |
| `KingraPOS.Infrastructure` | EF Core + SQLite, `DbContext`, skema & konfigurasi database | Domain, Application |
| `KingraPOS.UI` | WPF — Views, ViewModels, composition root | Application, Infrastructure |

> Project UI dinamai `KingraPOS.UI` (bukan `*.Wpf`) karena namespace berakhiran `.Wpf` bentrok dengan namespace `Wpf.Ui.Controls` milik WPF-UI saat kompilasi XAML.

## Struktur

```
src/
├─ KingraPOS.Domain/
│  ├─ Common/                 Entity (base, Id UUID string)
│  ├─ Entities/               38 entity sesuai skema
│  └─ Enums/                  enum untuk kolom TEXT ber-CHECK
├─ KingraPOS.Application/     (kosong — business logic menyusul)
├─ KingraPOS.Infrastructure/
│  └─ Persistence/
│     ├─ KingraPosDbContext.cs        pemetaan 38 tabel (snake_case)
│     ├─ KingraPosDatabase.cs         path & connection string
│     ├─ DatabaseInitializer.cs       menjalankan skema saat DB belum ada
│     ├─ Converters/                  konverter DateTimeOffset → ISO-8601 UTC
│     └─ Scripts/triggers.sql         trigger updated_at
└─ KingraPOS.UI/
   ├─ Common/                 ObservableObject, RelayCommand
   ├─ ViewModels/             MainViewModel
   └─ Views/                  MainWindow
```

## Database (SQL-first)

File **`skema_kasir_final_offline.sql`** di root repo adalah **sumber kebenaran** skema. File itu di-embed ke assembly `KingraPOS.Infrastructure` dan **dijalankan otomatis** saat database belum ada — jadi `STRICT` table, `WITHOUT ROWID`, partial unique index, `CHECK`, dan seed dipakai apa adanya. EF Core hanya **memetakan** entity ke tabel; tidak memakai migrations/`EnsureCreated`.

- Lokasi database: `%LOCALAPPDATA%\KingraPOS\kingrapos.db`
- Versi skema dilacak lewat `PRAGMA user_version` (sekarang **1**); initializer idempoten.
- `PRAGMA journal_mode = WAL`, `foreign_keys = ON`, `busy_timeout = 5000` di-set setiap koneksi.
- Bagian trigger (`-- @@TRIGGERS@@`) pada file skema masih placeholder, sehingga trigger `updated_at` di-generate di `Scripts/triggers.sql` (27 tabel).

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

## Menjalankan

```powershell
dotnet run --project src/KingraPOS.UI/KingraPOS.UI.csproj
```

## Catatan

- UI dan business logic belum diimplementasikan; layer `Application` sengaja dikosongkan.
- Repository per-agregate belum dibuat — `DbContext` adalah akses data untuk saat ini.
- Dependency injection masih manual di `App.xaml.cs` (composition root) dan dapat diganti ke DI container tanpa menyentuh layer lain.
