# KingraPOS

Aplikasi Point of Sale (POS) berbasis **.NET 8** dan **WPF**, dengan library UI **WPF-UI** (lepo.co).

## Arsitektur

Solusi memakai pemisahan berlapis (layered architecture) dengan arah dependency satu arah:

```
UI  ──▶  Application  ──▶  Domain
 │                          ▲
 └──▶  Infrastructure  ─────┘
```

| Project | Tanggung jawab | Referensi |
|---|---|---|
| `KingraPOS.Domain` | Entitas, enum, dan abstraksi repository | — |
| `KingraPOS.Application` | Business logic / use case, DTO, mapping | Domain |
| `KingraPOS.Infrastructure` | Implementasi repository (in-memory) | Domain, Application |
| `KingraPOS.UI` | WPF — Views, ViewModels, composition root | Application, Infrastructure |

> Project UI dinamai `KingraPOS.UI` (bukan `*.Wpf`) karena namespace berakhiran `.Wpf` bentrok dengan namespace `Wpf.Ui.Controls` milik WPF-UI saat kompilasi XAML.

## Struktur

```
src/
├─ KingraPOS.Domain/
│  ├─ Common/                 Entity (base)
│  ├─ Entities/               Category, Product, Sale, SaleItem
│  ├─ Enums/                  PaymentMethod, SaleStatus
│  └─ Interfaces/Repositories/
├─ KingraPOS.Application/
│  ├─ Abstractions/           IProductService, ISaleService
│  ├─ Dtos/
│  ├─ Mappings/               Mapping manual entity → DTO
│  └─ Services/               ProductService, SaleService
├─ KingraPOS.Infrastructure/
│  ├─ Persistence/            InMemoryDataStore (+ seed data)
│  └─ Repositories/           InMemoryCategoryRepository, InMemoryProductRepository, InMemorySaleRepository
└─ KingraPOS.UI/
   ├─ Common/                 ObservableObject, RelayCommand
   ├─ ViewModels/             MainViewModel
   └─ Views/                  MainWindow
```

## Alur

`View` → `ViewModel` → `Application Service` → `Repository`. Pada pola MVVM, ViewModel berperan sebagai "controller".

## Menjalankan

```powershell
dotnet run --project src/KingraPOS.UI/KingraPOS.UI.csproj
```

## Catatan

- Penyimpanan data masih **in-memory** (belum memakai database). Untuk pindah ke EF Core + SQLite, cukup tambahkan implementasi di `KingraPOS.Infrastructure/Persistence` tanpa mengubah Domain/Application.
- Dependency injection masih manual di `App.xaml.cs` (composition root) dan dapat diganti ke DI container tanpa menyentuh layer lain.
