# Changelog / История изменений

Формат основан на [Keep a Changelog](https://keepachangelog.com/ru/1.1.0/), версии — по [SemVer](https://semver.org/lang/ru/).

## [Unreleased]

### Добавлено / Added
- Свободная лицензия MIT. / MIT license.
- Сайт проекта на GitHub Pages. / Project website on GitHub Pages.
- Шаблоны Issues для ошибок и предложений. / Issue templates for bugs and feature requests.
- Описание для установки через winget. / winget manifest.

## [1.1.0] — 2026-10-04

Первая публичная версия. / First public release.

### Добавлено / Added
- Сканирование подключённого диска с пропуском системных папок, корзины и кэшей; нечитаемые папки не прерывают обход.
  / Drive scanning that skips system folders, the Recycle Bin and caches; unreadable folders don't stop the scan.
- Категории: документы, таблицы, презентации, фото (включая RAW), архивы, видео; фильтр мелких картинок.
  / Categories: documents, spreadsheets, presentations, photos (including RAW), archives, videos; tiny-image filter.
- Редактор категорий и расширений (*Настройки → Расширения файлов*).
  / Category and extension editor (*Settings → File extensions*).
- Выбор файлов галочками, поиск, сортировка, «показать в проводнике».
  / Checkbox selection, search, sorting, "show in Explorer".
- Копирование с сохранением структуры папок или по категориям; важные файлы копируются первыми.
  / Copying with original folder structure or by category; important files are copied first.
- Проверка каждой копии по MD5, `manifest.csv` и `errors.log` в архиве.
  / MD5 verification of every copy, `manifest.csv` and `errors.log` in the archive.
- Автообновление списка дисков при подключении.
  / Drive list refreshes automatically when a drive is connected.
- Окно «О программе». / About dialog.

[Unreleased]: https://github.com/novumdata0-collab/disksaver/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/novumdata0-collab/disksaver/releases/tag/v1.1.0
