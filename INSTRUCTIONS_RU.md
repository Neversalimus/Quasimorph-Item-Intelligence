# Item Intelligence 1.7.42.6 — публикация

Принят код Test2 с совместимостью Quasimorph 1.0.4.582 и тремя новыми языками: испанским, бразильским португальским и французским. Все 1971 новые строки вычитаны; локализации содержат по 657 ключей. Свежий лог и скриншоты подтверждают работу цен и показанных экранов. В стабильную версию добавлена небольшая подгонка длинных кнопок каталога.

## 1. Подготовка

Нужны PowerShell 7, git и авторизованный gh. Закрой игру, скачай `Quasimorph_ItemIntelligence_v1.7.42.6_Release_Publish_Full.zip` в Downloads и вставь:

```powershell
$qiiZip = Get-ChildItem "$env:USERPROFILE\Downloads" -Filter "Quasimorph_ItemIntelligence_v1.7.42.6_Release_Publish_Full*.zip" -File | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $qiiZip) { throw "Релизный архив 1.7.42.6 не найден в Downloads" }
$qiiReleaseDir = Join-Path $env:TEMP ("QII_RELEASE_17426_" + [guid]::NewGuid().ToString("N"))
Expand-Archive -LiteralPath $qiiZip.FullName -DestinationPath $qiiReleaseDir -ErrorAction Stop
pwsh -NoProfile -ExecutionPolicy Bypass -File (Join-Path $qiiReleaseDir "Prepare_QII_v1.7.42.6_Release.ps1") -GameRoot "C:\Program Files (x86)\Steam\steamapps\common\Quasimorph"
if ($LASTEXITCODE -ne 0) { throw "Подготовка релиза завершилась ошибкой; см. сообщение выше" }
```

Путь игры взят из присланного архива. Он указан явно, поэтому прежнее ложное распознавание двух одинаковых путей с разным регистром не мешает подготовке. Если Steam установлен иначе, укажи реальную папку в `-GameRoot`.

Скрипт проверит SHA256 принятой игры, восстановит закреплённый коммит из проверенного Git bundle, выполнит проверки RELEASE и соберёт DLL. Исходники будут в `C:\QM_Workshop\_QII_RELEASE_SOURCE_17426`; комплект для публикации — в `C:\QM_Workshop\Frozen\ItemIntelligence-v1.7.42.6`.

При существующем полном комплекте повторный запуск проверяет и использует прежние файлы без пересборки. Отличающиеся исходники или неполный комплект сохраняются, а скрипт останавливается. Для отдельной подготовки доступны `-SourceWork` и `-OutputDirectory`; последующие команды будут напечатаны с выбранными путями.

## 2. Workshop

После `STABLE RELEASE FROZEN` запусти игру и выполни в её консоли разработчика:

```text
mod_updateworkshopitem 3780078201 C:\QM_Workshop\Frozen\ItemIntelligence-v1.7.42.6\payload FALSE
```

После успешной загрузки включи PUBLIC, отключи DEV и полностью перезапусти игру. Открой F2 и Торговлю, проверь цены и выбранный язык. В Player.log ожидается:

```text
[ItemIntelligence] ACTIVE VERSION 1.7.42.6 (StableRelease17426).
```

Обновляется существующий публичный предмет. DEV 3781927679 сохраняется для следующих тестов. Публикуй именно замороженную папку `payload`.

## 3. GitHub

После успешной проверки публичной копии, в той же PowerShell-консоли:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File (Join-Path $qiiReleaseDir "Publish_QII_v1.7.42.6_GitHub.ps1") -WorkshopPublished
```

Если консоль закрыта, для стандартных путей можно запустить сохранённый издатель напрямую:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File "C:\QM_Workshop\_QII_RELEASE_SOURCE_17426\Release\Publish-Release.ps1" -ReceiptPath "C:\QM_Workshop\Frozen\ItemIntelligence-v1.7.42.6\release-receipt.json" -WorkshopPublished
```

Издатель повторно сверяет исходники и каждый файл, атомарно обновляет main и тег, загружает архив и контрольные суммы в черновик, скачивает их для сравнения и только затем открывает релиз. Исправление прежней ошибки обнаружения черновика через 404 включено. При обрыве повтори ту же команду; совпадающие файлы сохраняются. Отличающийся тег, main или вложение не перезаписываются принудительно.

До завершения публикации сохраняй исходники и frozen-комплект без изменений. `-WorkshopPublished` подтверждает уже выполненные загрузку Steam и проверку публичного запуска.

## Что проверено

Пройдены 66052 автоматических утверждения и компиляция C# 5 всех 112 исходников против 118 DLL новой игры, без ошибок и предупреждений. Игровая приёмка Test2: 15 скриншотов, один Player.log, показанные RU/ES/PT-BR/FR экраны при 1920×1080 и масштабах 100–150%. Носители языка пока не участвовали в вычитке. После финальной подгонки кнопок новых скриншотов нет. Сохраняются известное исключение слайдера MCM и два коротких превышения времени; они перечислены в отчёте.

`ItemIntelligence/Support/ReleaseAcceptance17426.md` и `LocalizationReview17426.md` описывают точную область проверки. `VALIDATION.json` фиксирует проверку комплекта публикации и явно указывает подменённые внешние сервисы. Игровых DLL в архиве нет; итоговая DLL собирается на твоей машине, её хеш записывается в `release-receipt.json`.

Новые описания Workshop находятся в `ItemIntelligence/Workshop/Description_ES.bbcode.txt`, `Description_PT-BR.bbcode.txt`, `Description_FR.bbcode.txt`. Их можно вставить в соответствующие языковые описания страницы после выпуска.
