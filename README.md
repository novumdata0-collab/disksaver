# DiskSaver

**[Русский](#русский) · [English](#english)**

---

## Русский

**DiskSaver** — приложение для Windows, которое быстро находит на подключённом диске (HDD или SSD) нужные файлы — документы, таблицы, презентации, фото, видео, архивы — и сохраняет выбранное в архив на другой диск. После этого старый диск можно спокойно отформатировать.

### Возможности

- **Автоопределение дисков** — при подключении диска (SATA, USB-бокс, док-станция) список обновляется сам, новый диск выбирается автоматически.
- **Быстрое сканирование** — пропускаются системные папки (`Windows`, `Program Files`, корзина, кэши браузеров, временные файлы), повреждённые и недоступные папки не прерывают обход.
- **Категории файлов** — документы, таблицы, презентации, фото (включая RAW), архивы, видео. Мелкие картинки (иконки, миниатюры) отсекаются.
- **Свои расширения** — в меню *Настройки → Расширения файлов* можно менять расширения, добавлять и удалять категории.
- **Удобный выбор** — счётчики и объём по категориям, галочки, поиск по имени и пути, сортировка, «показать в проводнике».
- **Надёжное копирование**:
  - два режима — сохранить структуру папок или разложить по категориям (фото и видео — по годам);
  - сначала копируются мелкие ценные файлы (документы, таблицы), видео — в конце;
  - каждая копия проверяется повторным чтением и сравнением MD5;
  - в архив записываются `manifest.csv` (открывается в Excel) и `errors.log` при ошибках;
  - нельзя сохранить архив на тот же диск, перед стартом проверяется свободное место.

### Системные требования

- Windows 10 / 11, x64.
- Готовый `DiskSaver.exe` самодостаточен — устанавливать .NET не нужно.

### Установка и запуск

1. Скачайте `DiskSaver.exe` из раздела [Releases](https://github.com/novumdata0-collab/disksaver/releases).
2. Запустите двойным кликом. Установка не требуется.

### Как пользоваться

1. Подключите диск — он появится в списке «Диск» вверху окна.
2. Нажмите **«Сканировать»**. Слева видно, сколько файлов найдено в каждой категории.
3. Снимите галочки с лишнего: целыми категориями слева или отдельными файлами в таблице.
4. Укажите папку на **другом** диске в блоке «Куда сохранить архив» и выберите режим раскладки.
5. Нажмите **«Копировать выбранное»**. По окончании программа покажет итог и предложит открыть папку архива.
6. Форматируйте старый диск, только если копирование завершилось **без ошибок**.

Архив создаётся в папке вида `<диск>_<метка>_<дата>`, например `E_Seagate_2026-10-04_1530`.

Настройки категорий хранятся в `%APPDATA%\DiskSaver\categories.json`.

### Сборка из исходников

Нужен [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
# запуск в режиме разработки
dotnet run --project src/DiskSaver

# сборка одного exe-файла
dotnet publish src/DiskSaver -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true -p:DebugType=none -o publish
```

Результат — `publish/DiskSaver.exe`.

### Структура проекта

```
src/DiskSaver.Core   — логика: категории, сканер диска, копирование с проверкой, список дисков
src/DiskSaver        — интерфейс WPF (MVVM): главное окно, настройки расширений, «О программе»
PLAN.md              — план развития
```

### Планы

Иконка в трее, запоминание просканированных дисков (SQLite), пауза и продолжение копирования, сверхбыстрое сканирование через чтение MFT, поиск дубликатов, безопасное форматирование из программы. Подробнее — в [PLAN.md](PLAN.md).

### Разработчик

ООО «Дата новум» — [datanovum.ru](https://datanovum.ru)

---

## English

**DiskSaver** is a Windows application that quickly finds important files on a connected drive (HDD or SSD) — documents, spreadsheets, presentations, photos, videos, archives — and copies the ones you select to an archive on another drive. After that, the old drive can be safely formatted.

### Features

- **Drive detection** — when a drive is connected (SATA, USB enclosure, docking station), the drive list refreshes automatically and the new drive is selected.
- **Fast scanning** — system folders (`Windows`, `Program Files`, Recycle Bin, browser caches, temp files) are skipped; damaged or inaccessible folders do not stop the scan.
- **File categories** — documents, spreadsheets, presentations, photos (including RAW), archives, videos. Tiny images (icons, thumbnails) are filtered out.
- **Custom extensions** — use *Settings → File extensions* to edit extensions and add or remove categories.
- **Easy selection** — per-category counts and sizes, checkboxes, search by name and path, sorting, "show in Explorer".
- **Reliable copying**:
  - two layouts — keep the original folder structure, or sort by category (photos and videos by year);
  - small valuable files (documents, spreadsheets) are copied first, videos last;
  - every copy is verified by re-reading it and comparing MD5 hashes;
  - `manifest.csv` (opens in Excel) and, if anything failed, `errors.log` are written to the archive;
  - the archive cannot be placed on the source drive; free space is checked before starting.

### Requirements

- Windows 10 / 11, x64.
- The prebuilt `DiskSaver.exe` is self-contained — no .NET installation needed.

### Installation

1. Download `DiskSaver.exe` from [Releases](https://github.com/novumdata0-collab/disksaver/releases).
2. Double-click to run. No installation required.

### Usage

1. Connect the drive — it appears in the "Диск" (Drive) list at the top.
2. Click **"Сканировать"** (Scan). The left panel shows how many files were found in each category.
3. Uncheck what you don't need — whole categories on the left or individual files in the table.
4. Choose a folder on a **different** drive under "Куда сохранить архив" (Archive destination) and pick a layout.
5. Click **"Копировать выбранное"** (Copy selected). When finished, the app shows a summary and offers to open the archive folder.
6. Format the old drive only if copying finished **without errors**.

The archive is created in a folder like `<drive>_<label>_<date>`, e.g. `E_Seagate_2026-10-04_1530`.

Category settings are stored in `%APPDATA%\DiskSaver\categories.json`.

> The user interface is currently in Russian.

### Building from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
# run in development mode
dotnet run --project src/DiskSaver

# build a single exe file
dotnet publish src/DiskSaver -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true -p:DebugType=none -o publish
```

The output is `publish/DiskSaver.exe`.

### Project structure

```
src/DiskSaver.Core   — core logic: categories, disk scanner, verified copying, drive list
src/DiskSaver        — WPF UI (MVVM): main window, extension settings, About dialog
PLAN.md              — development roadmap (in Russian)
```

### Roadmap

System tray icon, remembering scanned drives (SQLite), pause/resume copying, ultra-fast scanning by reading the NTFS MFT, duplicate detection, safe formatting from within the app. See [PLAN.md](PLAN.md).

### Developer

Data Novum LLC (ООО «Дата новум») — [datanovum.ru](https://datanovum.ru)
