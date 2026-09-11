# Item Intelligence 1.7.42.6-test1 — ES / PT-BR / FR

База — исходники стабильной 1.7.42.5. Добавлены испанский, бразильский португальский и французский: по 657 строк интерфейса. Это DEV-кандидат для проверки новых переводов в игре.

## Установка

Закрой Quasimorph и скачай `Quasimorph_ItemIntelligence_v1.7.42.6_ES_PTBR_FR_Test1_Full.zip` в Downloads. В PowerShell 7 выполни:

```powershell
$qiiLangZip = Get-ChildItem "$env:USERPROFILE\Downloads" -Filter "Quasimorph_ItemIntelligence_v1.7.42.6_ES_PTBR_FR_Test1_Full*.zip" -File | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $qiiLangZip) { throw "Архив локализации не найден в Downloads" }
$qiiLangDir = Join-Path $env:TEMP ("QII_17426_LANG_" + [guid]::NewGuid().ToString("N"))
Expand-Archive -LiteralPath $qiiLangZip.FullName -DestinationPath $qiiLangDir
pwsh -NoProfile -ExecutionPolicy Bypass -File (Join-Path $qiiLangDir "ItemIntelligence\Install.ps1")
if ($LASTEXITCODE -ne 0) { throw "Сборка DEV завершилась ошибкой; см. сообщение выше" }
```

Если игру не удалось найти автоматически, повтори последний запуск с `-GameRoot "C:\Program Files (x86)\Steam\steamapps\common\Quasimorph"`.

Установщик выполнит проверки, соберёт DLL против установленной игры и подготовит `C:\QM_Workshop\ItemIntelligence_DEV`. После успешной сборки открой игру и выполни в её консоли:

```text
mod_updateworkshopitem 3781927679 C:\QM_Workshop\ItemIntelligence_DEV FALSE
```

Включи DEV, отключи публичную копию QII и полностью перезапусти игру. Ожидаемый маркер в Player.log:

```text
[ItemIntelligence] ACTIVE VERSION 1.7.42.6-test1 (LatinLanguagesTest01).
```

## Проверка трёх языков

1. В MCM последовательно выбери **Español**, **Português (Brasil)** и **Français**, каждый раз сохраняя настройку. Открой F2 и проверь каталог, Обзор, Magnum, Рецепты, Торговлю, Боеприпасы, Фракции и Добычу. Подписи MCM обновятся после перезапуска; F2 — после сохранения настройки.
2. Для каждого языка оставь его выбранным, закрой игру полностью и запусти снова. Проверь сохранённый выбор, подписи MCM, список языков и F2. Акценты должны отображаться: `ñ`, `ã`, `ç`, `é`, `ê`.
3. Проверь большое окно при 1920×1080, масштабы 150% и 200%, длинные пояснения Добычи и Торговли, заголовки и кнопки. Поиск и закрытие должны оставаться доступны. Затем верни обычное окно.
4. Для Auto выбери соответствующий язык самой Quasimorph и перезапусти игру. Ожидаемые файлы в Player.log: Spanish → `es.lang`, BrazilianPortugal → `pt-BR.lang`, French → `fr.lang`; в каждом 657 entries.
5. При русском языке игры и ручном ES / PT-BR / FR названия предметов, врагов и фракций остаются русскими. Поиск использует эти игровые имена. Для отдельной проверки смешанных шрифтов можно выбрать китайский язык игры и один из новых языков QII.
6. В числах ожидается запятая: `1,25`; во французском дни сокращены до `j`. В ES/FR перед `%` есть пробел, в PT-BR — нет. Шанс появления предмета, условный источник и шанс извлечения ампутацией должны оставаться разными показателями.

Для обратной связи приложи Player.log после перезапуска и скриншоты MCM, каталога и развёрнутой Добычи/Торговли для каждого языка. Особенно полезны обрезанные подписи и замечания носителей к терминам.

## Содержимое

`WORKSHOP_CONTENT/Localization/` содержит восемь языков и шаблон. `Support/Localization17426.md` описывает проверки и границы. В `Workshop/` лежат три описания страницы Workshop в BBCode; публиковать их следует вместе с принятым обновлением. Git bundle и идентификатор коммита включены в корень полного архива для восстановления исходников. Игровые DLL и готовая DLL мода не вложены.
