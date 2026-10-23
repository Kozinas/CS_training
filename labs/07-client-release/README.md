# L07. Клиент, поставка и восстановление

**Вход:** главы 50–58 и рабочий L06. **Результат:** клиент выбранной ветки, проверенный дистрибутив,
инструкция запуска/обновления и протокол восстановления. Эталон выбирает Blazor из главы 51
и API L06. WPF/MAUI/игровая ветка допустима при равноценных проверках её поставки и данных.
[Примеры и команды](../../examples/Delivery/README.md).

## Договор приложения

Пользователь входит в API и видит собственный отчёт в клиенте. Клиент показывает Loading, Empty,
Ready, Error и Canceled; ошибка не подменяется пустым успешным списком. Токены не смешиваются между
сессиями. Все прежние ограничения L06 на расходы, ownership и идемпотентность сохраняются.

Вход [batch.json](data/batch.json) создаёт один расход food на 1250 копеек. Повтор даёт прежнюю
квитанцию, а не второй расход. После замены приложения и восстановления backup результат остаётся тем же.
Выпуск 1.1.0 в эталоне меняет metadata версии, сохраняя schema L06-InitialWeb и договор 1.0.0.
Это упражнение совместимого переключения; изменения схемы нужно проектировать отдельно.

## Что находится в поставке

```text
releases/
  1.0.0/ api/ ui/ release.json
  1.1.0/ api/ ui/ release.json
state/
  ledger.db
  keys/
backups/
  выбранный проверенный снимок
```

Каталог релиза не содержит изменяемую БД, ключи, пароли или журналы. Manifest указывает версию,
target/mode и hashes. Проверка отказывает при изменённом бинарном файле, лишнем файле и выходе пути
за каталог. Hash не заменяет подпись; в реальной передаче важен доверенный источник артефакта.

## Подготовка и автоматический прогон

Эталон проверяется на Windows x64: .NET SDK 10.0.402, Framework 4.8+, C++ workload Visual Studio
для Native AOT, Python 3.10+. Для PDF нужны зависимости из requirements-dev.txt.
Ничего не разворачивается в облако и не устанавливается как постоянная служба.

```text
python tools/verify_delivery.py
```

Скрипт собирает Delivery, выполняет Win32/ALC/Framework-проверки, публикует FDD, trimmed single-file
и AOT, затем создаёт два API/UI-релиза. Процессы используют свободные loopback-порты. БД, key ring,
тестовая учётная запись и большие бинарные результаты находятся во временном каталоге `.artifacts`
и удаляются после проверки. Остаются summary.json, publication.json и incidents.json без секретов.

Старый блок клиента проверяется `python tools/verify_specializations.py`; полный `python tools/verify.py`
выполняет оба блока и предыдущие лабораторные. Godot engine по умолчанию не запускается; его отдельная
headless-проверка описана в главе 53. Для выбранного эталона L07 Godot не требуется.

## Собрать сохраняемый релиз

Команды из корня репозитория; каталог назначения должен быть новым:

```text
python tools/package_release.py create 1.0.0 .artifacts/releases/1.0.0
python tools/package_release.py verify .artifacts/releases/1.0.0
python tools/package_release.py create 1.1.0 .artifacts/releases/1.1.0
```

Это framework-dependent выпуск: на целевой машине нужен подходящий ASP.NET Core runtime.
`dotnet --list-runtimes` помогает проверить наличие; SDK на целевой машине для запуска не обязателен.
Проект Native AOT — отдельная проверка главы 55, а не обещание AOT-совместимости EF/Identity/Blazor.

## Ручной запуск API и клиента

В первом PowerShell-терминале из корня репозитория:

```powershell
New-Item -ItemType Directory -Force .artifacts/release-state | Out-Null
$env:DOTNET_ENVIRONMENT = 'Development'
$env:Ledger__Path = [IO.Path]::GetFullPath('.artifacts/release-state/ledger.db')
$env:Keys__Directory = [IO.Path]::GetFullPath('.artifacts/release-state/keys')
$env:Web__AllowHttp = 'true'
$env:Worker__Enabled = 'false'
dotnet .artifacts/releases/1.0.0/api/LedgerWeb.dll --migrate
Push-Location .artifacts/releases/1.0.0/api
try { dotnet LedgerWeb.dll --urls http://127.0.0.1:5081 }
finally { Pop-Location }
```

HTTP здесь разрешён только для изолированного локального упражнения. Публичный запуск должен иметь
TLS, правильный proxy и secret store. Razor-формы L06 с Secure-cookie этим HTTP-примером не проверяются;
вход выполняется через API, а выбранный клиент — Blazor.

Во втором терминале создайте тестового пользователя и расход:

```powershell
$base = 'http://127.0.0.1:5081'
$credentials = @{ email='release@example.test'; password='Course-Release-Only-2026!' } | ConvertTo-Json
Invoke-RestMethod "$base/auth/register" -Method Post -ContentType application/json -Body $credentials
$login = Invoke-RestMethod "$base/auth/token" -Method Post -ContentType application/json -Body $credentials
$headers = @{ Authorization = "Bearer $($login.accessToken)" }
$batch = Get-Content labs/07-client-release/data/batch.json -Raw -Encoding UTF8
Invoke-RestMethod "$base/api/v1/imports" -Method Post -Headers $headers -ContentType application/json -Body $batch
Invoke-RestMethod "$base/api/v1/report" -Headers $headers
```

При повторном запуске используйте существующую учётную запись и новый вход вместо повторной регистрации.
Токен живёт 15 минут; поздний повтор требует нового входа. Не сохраняйте его в Git или текстовый файл релиза.

В третьем терминале настройте опубликованный UI:

```powershell
$env:DOTNET_ENVIRONMENT = 'Development'
$env:Ledger__Api = 'http://127.0.0.1:5081/'
Push-Location .artifacts/releases/1.0.0/ui
try { dotnet BlazorClient.dll --urls http://127.0.0.1:5080 }
finally { Pop-Location }
```

Откройте http://127.0.0.1:5080, введите accessToken из ответа входа и загрузите отчёт.
Ожидается food, count=1, amountMinor=1250. Вставляйте только токен временной учебной сессии.
Проверьте также отмену/ошибку, keyboard focus и отсутствие смешения данных двух разных аккаунтов.
Автоматический smoke читает HTML/asset, а эту браузерную приёмку следует выполнить самостоятельно.

## Runbook обновления и отката

1. Проверьте manifest нового релиза и совместимость схемы, runtime и настроек. Подготовьте backup.
2. Закройте приём пользовательской работы; остановите принадлежащие вам процессы старого релиза.
   Для реального сервиса это управляемый drain, а не произвольное завершение всех dotnet-процессов.
3. Выполните отдельную миграцию по плану. В этой работе её повтор не меняет существующую схему.
4. Запустите API и UI 1.1.0 с прежними внешними путями state/keys и правильным адресом API.
5. Проверьте readiness, вход, отчёт и повтор BatchId. Только затем возобновите пользовательский доступ.
6. Для учебного rollback остановите 1.1.0 и верните 1.0.0 с той же БД. Повторите бизнес-проверки.

Если схема перестала быть совместимой, этот rollback неприменим. Не подменяйте план восстановления
данных возвратом старой папки. Не запускайте одновременно несколько владельцев одного SQLite-файла
без проверки требуемой модели конкурентности и режима размещения.

## Матрица приёмки

| Сценарий | Ожидаемый результат |
| --- | --- |
| Изменённый binary / лишний файл / путь `../` в manifest | Verify отклоняет выпуск |
| API до миграции | Live=200, Ready=503 |
| Миграция и повтор миграции | Readiness=200, прежние данные сохранены |
| Первый импорт и повтор пакета | Один расход, повторная квитанция |
| Fail/slow внешнего /rate | 503/504; /report остаётся корректным |
| Выпуск 1.1.0 и rollback 1.0.0 | Пользователь, отчёт и receipt сохранены |
| Restore backup в новый файл | Integrity=ok, report=1250, replay не удваивает сумму |
| UI из publish-папки | HTML и blazor.web.js доступны; ручная загрузка работает |
| Graceful drain | Приняты 1,2,3; перегрузка и новые задания после закрытия отвергнуты |

Автоматический сценарий отдельно проверяет AOT/trimmed executable и Framework-совместимость.
Он останавливает тестовые Kestrel-процессы принудительно, моделируя замену/перезапуск; это не
доказательство graceful Kestrel shutdown. Drain проверяется отдельно через Host.StopAsync в LifecycleProbe.

## Что сдавать

Предъявите исходники выбранного клиента, версии SDK/пакетов, manifest/способ получения артефакта,
инструкцию конфигурации, результаты проверок и incident report. Для отчёта о сбое укажите симптом,
timeline, доказательства, причину, восстановление и регрессионный сценарий. Секреты и личные данные
не должны быть приложены к сдаче.

Минимальная готовность: другой человек запускает приложение по инструкции, выполняет матрицу,
объясняет границы гарантий и восстанавливает известный отчёт. Чистая VM/Linux-контейнер, публичный TLS,
zero-downtime rollout, разрушительная миграция и потеря питания являются дополнительными испытаниями;
локальный успешный прогон не следует выдавать за их прохождение.

После L07 переходите к самостоятельному итоговому проекту и защите изменения требований в главах 59–60.
