# Примеры 55–58 и L07: interop, migration и поставка

Из корня: `python -B tools/verify_delivery.py`.
Обязательная среда этого блока — **Windows x64**, SDK **10.0.402**, Framework **4.8+** для net48-процесса
и Visual Studio с workload **Desktop development with C++** для Native AOT. Ничего из этого скрипт
не устанавливает сам. В текущей локальной среде Framework 4.8.1 и C++ toolchain присутствуют.

| Проект | Target | Проверка |
| --- | --- | --- |
| InteropProbe | net10.0; публикация win-x64 | LibraryImport, CreateFileW/GetFileSizeEx, SafeFileHandle, source-generated JSON |
| PluginContracts | net10.0 | Один общий IReportRule |
| SamplePlugin | net10.0 | Правило округления до сотен minor units |
| PluginHost | net10.0 JIT | Collectible ALC, удержание static и освобождение |
| LegacyRules | net48 / C# 7.3 | Старое правило с ConfigurationManager/App.config |
| PortableRules | netstandard2.0 / C# 7.3 | Извлечённое правило с явным rate |
| CompatibilityProbe | net48 и net10.0 | 36/19 проверок и три культуры |
| LifecycleProbe | net10.0 + ASP.NET Core framework | Generic Host, bounded Channel, отказ при перегрузке и drain |

Reference assemblies закреплены на Microsoft.NETFramework.ReferenceAssemblies 1.0.3.
ILCompiler/ILLink берутся из SDK и закреплены lock-файлом на 10.0.13. Все восемь проектов имеют lock-файлы.
Legacy-проекты здесь SDK-style: сравнивается runtime/поведение, а не заявляется автоматическая конвертация non-SDK проекта.

## Сборка и отдельные запуски

```powershell
$env:DOTNET_CLI_HOME = Join-Path (Get-Location) '.artifacts/dotnet-home'
$env:NUGET_PACKAGES = Join-Path (Get-Location) '.artifacts/packages'
dotnet restore examples/Delivery/Delivery.slnx --locked-mode
dotnet build examples/Delivery/Delivery.slnx -c Release --no-restore
dotnet examples/Delivery/InteropProbe/bin/Release/net10.0/InteropProbe.dll size README.md
dotnet examples/Delivery/PluginHost/bin/Release/net10.0/PluginHost.dll examples/Delivery/SamplePlugin/bin/Release/net10.0/SamplePlugin.dll
& examples/Delivery/CompatibilityProbe/bin/Release/net48/CompatibilityProbe.exe
dotnet examples/Delivery/CompatibilityProbe/bin/Release/net10.0/CompatibilityProbe.dll
dotnet examples/Delivery/LifecycleProbe/bin/Release/net10.0/LifecycleProbe.dll
```

Interop: Bytes равен размеру входа, Closed/RejectsClosed=true. Отсутствующий файл: stdout пуст,
stderr `native-error: 2`, exit code 3. Не передавайте недоверенную DLL PluginHost: ALC не является sandbox.
PluginHost: Value=1200, Unloaded/Retained/Released=true. Forced GC используется только для опыта.
Compatibility: 36 проверок Framework и 19 современного .NET. Lifecycle: Accepted=3, Processed=[1,2,3],
OverloadRejected/ClosedRejected/Drained=true. Задания находятся в памяти; аварийное завершение их не сохраняет.

## Публикация

Полный verify публикует InteropProbe тремя способами: framework-dependent, self-contained с single-file
и trimming, Native AOT. Каждый executable реально выполняется с обычным и ошибочным входом.
Для SC/AOT дополнительно задаётся пустой путь DOTNET_ROOT; это не заменяет чистую виртуальную машину.
Анализаторы AOT/trimming не подавляются. Для ручного AOT:

```powershell
dotnet publish examples/Delivery/InteropProbe -c Release -r win-x64 --self-contained true -o .artifacts/manual-aot
& .artifacts/manual-aot/InteropProbe.exe size README.md
```

Внутри csproj PublishAot=true. При сравнении других режимов скрипт явно переопределяет PublishAot=false
и PublishTrimmed/PublishSingleFile. Такой переключатель не делает PluginHost с динамической загрузкой
или прежний EF/Blazor стек AOT-совместимым: они намеренно остаются обычными JIT-приложениями.

## Поставка API/UI и аварийные сценарии

```text
python tools/package_release.py create 1.0.0 .artifacts/releases/1.0.0
python tools/package_release.py verify .artifacts/releases/1.0.0
```

Новый каталог содержит опубликованные **Web/LedgerWeb** и **Specializations/BlazorClient** и release.json.
Создание разрешено только внутри `.artifacts`, существующий каталог не перезаписывается.
Проверка hashes не является цифровой подписью. Никакой внешней доставки этот инструмент не выполняет.
Полный ручной запуск и runbook — в [L07](../../labs/07-client-release/README.md).

`verify_delivery.py` создаёт два временных релиза 1.0.0/1.1.0 с одной схемой. Проверяет manifest/tamper,
health до/после миграции, Identity/import/report, отказы зависимости, rollback и backup/restore.
Смена версии сохраняет пользователя, key ring и receipt; повтор импорта не удваивает 1250 копеек.
SQLite backup выполняется через поддерживаемый API, а не копированием работающего WAL-файла.
Kestrel стартует из publish-папки; UI отдаёт собственный опубликованный framework asset.

Скрипт создаёт уникальный временный каталог под `.artifacts` и очищает только его. Процессы принадлежат
проверке и слушают loopback; токены/пароли не включаются в сохраняемые результаты.
В `.artifacts/delivery` остаются summary.json, publication.json и incidents.json.
Автоматическое завершение Kestrel — принудительное; graceful drain отдельно проверяется Host.StopAsync.

## Что остаётся отдельной проверкой

- COM, Office automation и вызов собственной C-библиотеки не выполняются; native-пример использует Win32.
- Linux/macOS/ARM64, чистая VM без SDK, browser e2e и публичный HTTPS reverse proxy требуют своего прогона.
- Dockerfile — дополнительная Linux-заготовка, контейнер в обязательном наборе не запускается.
  Контекст — корень репозитория: `docker build -f examples/Delivery/Dockerfile -t course-ledger .`.
  Закрепите digest образов; задайте права volume, схему, ключи и TLS-прокси до эксплуатации.
- Rollback проверен с одной схемой; разрушительные миграции и резервная площадка не моделируются.
- Drain не гарантирует сохранность при Terminate или потере питания. Deadline зависшего drain — задание 58-B.

Все главы 55–58 и условие L07 включаются в один PDF вместе с предыдущим материалом.
