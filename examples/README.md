# Запускаемые примеры C#

Примеры относятся к главам 01–49. Языковая solution — [CSharpCourse.slnx](CSharpCourse.slnx) для 01–24.
Главы 25–35 используют [Integration.slnx](Integration/Integration.slnx); команды и пакеты описаны в
[Integration/README](Integration/README.md). Главы 36–39 используют [Engineering.slnx](Engineering/Engineering.slnx);
[Engineering/README](Engineering/README.md) описывает xUnit, MTP, NuGet-пакет и отдельный контейнерный тест.
Главы 40–43 и L05 используют [Data.slnx](Data/Data.slnx) с SQLite, Dapper и EF Core; команды — в [Data/README](Data/README.md).
Главы 44–49 и L06 используют [Web.slnx](Web/Web.slnx): API, Identity, Razor, gRPC/SignalR, outbox и тесты; [команды Web](Web/README.md).
Все решения независимы от пользовательского ConsoleApp1 в корне.

## Запуск

Из корня репозитория:

```powershell
dotnet build examples/CSharpCourse.slnx -c Release
dotnet run --project examples/Foundations -- 03
dotnet run --project examples/ExpenseLedger -- labs/01-expense-ledger/data/valid.txt
dotnet run --project examples/TypesAndQueries -- 18
dotnet run --project examples/CoreChecks
dotnet run --project examples/RuntimeLab -c Release -- 23
dotnet run --project examples/RuntimeChecks -c Release
dotnet run --project examples/CompilerLab -c Release
dotnet run --file examples/FileBased/hello.cs
python tools/verify.py
```

Foundations принимает номер от 01 до 09 и показывает короткий сценарий соответствующей главы.
ExpenseLedger — полный образец L01. Стартовые условия — в [лабораторной](../labs/01-expense-ledger/README.md).
Фрагменты Markdown объясняют отдельные приёмы и иногда требуют указанного окружающего кода;
полные программы находятся именно здесь.

## Состав

| Проект | Назначение |
| --- | --- |
| Foundations | Детерминированные короткие примеры глав 01–09 |
| ExpenseLedger | Файл, проверка формата, модель расхода, группировка и диагностика |
| ContractChecks | Проверки парсера L01 и модели в трёх культурах |
| ExpenseLedger.Core | Money, Expense, Result, IExpenseStore и LedgerService для L02 |
| ExpenseLedger.Memory | Реализация хранения через Dictionary |
| TypesAndQueries | Сценарии глав 11–18, включая C# 14 extension property |
| CoreChecks | Контракты L02 на двух хранилищах, культурах и семантика языка |
| RuntimeLab | Сценарии 19–21, 23–24; IL, ресурсы, reflection, span, unsafe, C# 14 |
| RuntimeChecks | Владение, схемы метаданных, парсеры и семантика новых возможностей |
| CompilerLab | Analyzer и incremental generator в собственном Roslyn-хосте; проверки и Emit |
| FileBased/hello.cs | Отдельный пример запуска .cs, без проекта в solution |

[Directory.Build.props](Directory.Build.props) задаёт net10.0, C# 14, nullable и ошибки при предупреждениях.
[global.json](global.json) выбирает стабильный SDK 10.0 с допустимым обновлением feature band.
При работе из корня выбор SDK обычно определяется корнем; проверочный скрипт запускает сборку из examples,
чтобы использовать этот global.json.

В [NuGet.Config](NuGet.Config) источники отключены для языковой solution: её restore работает офлайн.
Поддерево Integration имеет собственную конфигурацию nuget.org и packages.lock.json.
Полная проверка: `python tools/verify.py`; только язык: `python tools/verify.py --core-only`.
Этот файл действует на проекты examples и не меняет настройки ConsoleApp1.

Пример 09 создаёт собственный временный файл с уникальным именем, читает его и удаляет в finally.
ExpenseLedger читает только переданный файл. Свои эксперименты выполняйте в work.

## Дополнительные эксперименты

RuntimeLab принимает 19, 20, 21, 23 и 24; глава 22 запускается через CompilerLab.
Режим 19-il печатает IL маленького метода, а 23-alloc сравнивает выделения двух парсеров после прогрева.
Точные IL-байты и количества выделенных байтов не являются переносимыми критериями прохождения тестов.
Семантика парсеров проверяется отдельно, включая переполнение и три культуры.

CompilerLab ссылается на Roslyn-сборки выбранного SDK через MSBuildSDKsPath.
Он работает как обычное приложение net10.0 и запускает генератор/анализатор через API;
он не устанавливается в IDE и не подключается как analyzer к остальным проектам.
Это сохраняет офлайн-сборку. Распространяемое расширение потребует отдельной библиотеки,
совместимого target и закреплённых пакетов Roslyn; описание есть в главе 22.

RuntimeChecks ссылается на RuntimeLab как на сборку с публичными учебными типами:
ссылка на exe-проект допустима, точка входа при этом не запускается.
RuntimeLab разрешает unsafe для ограниченных примеров главы 23.
Публикация Native AOT и trimming не входит в текущую автоматическую проверку.

FileBased/hello.cs запускается SDK напрямую и проверяется отдельно от solution.
Для него явно отключён PublishAot. Остальные проекты не включают этот исходник.
