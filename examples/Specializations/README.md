# Примеры 50–54: клиенты, игры и вычисления

Из корня репозитория: `python tools/verify_specializations.py`.
Нужен .NET SDK 10.0.402 и первый restore из NuGet. Версии пакетов закреплены CPM и packages.lock.json.
WPF исполняется на Windows. Для headless-сцены дополнительно нужен Godot **4.6.2 .NET**;
сам Godot-проект компилируется обязательной проверкой без установки редактора.

| Проект | Назначение |
| --- | --- |
| ClientCore | CommunityToolkit.Mvvm 8.4.2, HTTP-клиент L06, очередь Microsoft.Data.Sqlite 10.0.13 |
| DesktopClient | WPF/net10.0-windows, XAML, dispatcher, загрузка и отмена |
| BlazorClient | Interactive Server, форма, состояния и отмена, токен на экземпляр панели |
| GameRules | Независимые правила инвентаря/движения; netstandard2.1, C# 9 |
| GodotClient | Godot.NET.Sdk 4.6.2, net8.0/C# 12, сцена и headless-проверка |
| UnityAdapter | Файл для Assets Unity 6.0; вне общего решения и без заявления о запуске Unity |
| Analysis | MathNet.Numerics 5.0.0, точная сумма, выборочная статистика и SIMD |
| Scenarios | CLI: offline, game, analyze, measure, report |
| Specializations.Tests | 43 xUnit/MTP кейса, включая bUnit 2.12.5 и реальную SQLite |

## Сборка и проверки

```powershell
$env:DOTNET_CLI_HOME = Join-Path (Get-Location) '.artifacts/dotnet-home'
$env:NUGET_PACKAGES = Join-Path (Get-Location) '.artifacts/packages'
dotnet restore examples/Specializations/Specializations.slnx --locked-mode
dotnet build examples/Specializations/Specializations.slnx -c Release --no-restore
python -B tools/verify_specializations.py
```

Скрипт проверяет 43 кейса и сценарии CLI, вызывает WPF `--verify` на Windows, компилирует Godot,
поднимает временный API L06 для настоящего HTTP-клиента и Blazor-хост для HTML/asset smoke.
Тестовые БД и учётные данные находятся во временном каталоге внутри `.artifacts` и удаляются.
В `.artifacts/specializations` остаются summary.json и measurement.json без токенов.
При standalone-запуске и отсутствии сборки Web скрипт дополнительно собирает предыдущий блок.

```powershell
python -B tools/verify_specializations.py --godot 'C:/path/to/Godot_v4.6.2-stable_mono_win64_console.exe'
```

Этот вариант ещё запускает сцену в движке. Обычный CI компилирует Godot, но движок не устанавливает.
WPF smoke не показывает окно; bUnit не заменяет настоящий браузер; Unity и мобильные устройства не запускались.

## Desktop и Blazor

```powershell
dotnet run --project examples/Specializations/DesktopClient
```

По умолчанию две категории: food=2000 и travel=3000 копеек. Запрос можно отменить.
Для Blazor в отдельном терминале:

```powershell
$env:DOTNET_ENVIRONMENT = 'Development'
dotnet run --project examples/Specializations/BlazorClient -- --urls http://localhost:5080
```

Откройте http://localhost:5080. Поле токена для демонстрации оставляется пустым.
Для API сначала запустите L06 по [Web README](../Web/README.md), создайте пользователя и получите
короткоживущий accessToken через `/auth/token`. В текущем терминале WPF задайте LEDGER_API и LEDGER_TOKEN;
для Blazor задайте Ledger__Api, а токен введите в самой панели. Сервер Blazor не хранит один общий токен.

```powershell
$env:LEDGER_API = 'https://localhost:7443/'
# Получите accessToken из JSON-ответа /auth/token в переменную $login:
$env:LEDGER_TOKEN = $login.accessToken
dotnet run --project examples/Specializations/DesktopClient
Remove-Item Env:LEDGER_TOKEN

$env:Ledger__Api = 'https://localhost:7443/'
dotnet run --project examples/Specializations/BlazorClient -- --urls http://localhost:5080
```

Порт и схема должны совпадать с вашим L06; используйте доверенный локальный сертификат, не отключайте TLS.
Демонстрационный HTTP допускается только к loopback. Конфигурация API задаётся владельцем приложения,
redirect выключен, ответ ограничен 64 KiB, timeout — 10 секунд. В production нужны полноценный login/logout,
срок/обновление сессии, HTTPS и настройка reverse proxy. Пример не является публичным сервисом авторизации.

## Offline, сохранение и анализ

```powershell
New-Item -ItemType Directory -Force .artifacts | Out-Null
dotnet run --project examples/Specializations/Scenarios -- offline .artifacts/mobile-demo.db
dotnet run --project examples/Specializations/Scenarios -- game .artifacts/game-save.json
dotnet run --project examples/Specializations/Scenarios -- game .artifacts/game-save.json
'[100,200,300]' | Set-Content -Encoding ASCII .artifacts/amounts.json
dotnet run --project examples/Specializations/Scenarios -- analyze .artifacts/amounts.json
dotnet run -c Release --project examples/Specializations/Scenarios -- measure
```

Offline: Retained=1, Sent=1, Pending=0, Attempts=2, UniqueBatches=1. Здесь сервер квитанций — fake;
SQLite — настоящая. Account key должен описывать API и подтверждённого пользователя, а не только отображаемое имя.
Слой сессии отвечает за соответствие ключа токену. В очереди нет токенов. Blocked сохраняется до явного
решения пользователя; UI такого разрешения и background task ОС оставлены заданиям главы 52.

Game: для нового пути Added=true, затем false, Coins=10 в обоих запусках. Изменённая/неизвестная версия
сохранения отклоняется без перезаписи. Один писатель и локальное переименование не доказывают power-loss durability.
Analysis: Count=3, TotalMinor=600, MeanMinor=200, SampleDeviationMinor=100. Допустимы 2..100000 целых сумм
1..99999999 копеек, JSON до 2 MiB. Ошибка даёт код 3 и пустой stdout; неверная CLI-команда — код 2.
Measure сохраняет наблюдение, не устанавливает переносимый порог скорости.

## Godot и Unity

```powershell
dotnet restore examples/Specializations/GodotClient --locked-mode
dotnet build examples/Specializations/GodotClient -c Debug --no-restore
godot-dotnet --path examples/Specializations/GodotClient
godot-dotnet --headless --path examples/Specializations/GodotClient -- --verify
```

`godot-dotnet` — условное имя установленного executable/wrapper; при необходимости используйте полный путь.
Обычный запуск показывает счётчик и кнопку pickup. `--verify` проверяет сцену/JSON/delta и завершает процесс.
Для Unity соберите GameRules в Release, скопируйте `GameRules/bin/Release/netstandard2.1/GameRules.dll`
в Assets/Plugins, затем UnityAdapter/InventoryBehaviour.cs в Assets. Это упражнение на интеграцию:
компонент нужно добавить объекту сцены, проверить профиль API и выполнить приёмку в вашем Editor.

Мобильная оболочка, Avalonia, ML.NET-модель и ONNX-inference описаны как расширения с критериями,
а не как якобы протестированные проекты. Все пять глав входят в общий PDF курса.
