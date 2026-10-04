# winget

Описание пакета `DataNovum.DiskSaver` для [winget](https://learn.microsoft.com/windows/package-manager/).
После принятия в репозиторий [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs) программу можно будет установить командой:

```
winget install DataNovum.DiskSaver
```

## Как отправить

1. Проверить описание: `winget validate --manifest winget/manifests/d/DataNovum/DiskSaver/1.2.0` (схема 1.12)
2. Сделать fork `microsoft/winget-pkgs`, скопировать папку `manifests/d/DataNovum/DiskSaver/1.2.0` в тот же путь и открыть pull request.

## Новая версия

Скопировать папку с новым номером версии, обновить `PackageVersion`, `InstallerUrl`, `InstallerSha256` и `ReleaseDate`.
SHA256 файла: `(Get-FileHash DiskSaver.exe).Hash`.
