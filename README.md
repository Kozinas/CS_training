# C# и .NET — методичка и практикум

Русскоязычный курс с теорией, примерами, заданиями и растущим по сложности сквозным проектом.
Начинать можно без опыта программирования. Базовая среда: **C# 14 и .NET 10 LTS**.

**[Читать единый PDF](dist/csharp.pdf)** · [Единый Markdown](dist/csharp.md) ·
[Полная программа из 60 глав](PLAN.md) · [Ход наполнения](PROGRESS.md) ·
[Результаты проверок](VALIDATION.md)

**Готовы главы 01–43, 129 заданий A/B/C и лабораторные L01–L05.**
Главы 44–60 подробно спланированы, но ещё не наполнены учебным текстом.
Семь блоков ведут от первой программы к библиотеке правил, импортёру, фоновому конвейеру,
сопровождению кода и транзакционной базе данных. Дальнейший маршрут включает ASP.NET Core, UI, игры и поставку приложения.

## Начать обучение

1. Прочитайте [как проходить курс](docs/00-start.md).
2. Начните с [главы 01](docs/01-foundations/01-platform.md) и выполняйте A/B/C.
3. Храните свои решения в `work`, а с готовыми примерами сравнивайте после собственной попытки.
4. Сдайте [L01](labs/01-expense-ledger/README.md), пройдите главы 11–18 и выполните [L02](labs/02-domain-library/README.md).
5. Пройдите [блок CLR и полного языка](docs/03-runtime/19-assemblies.md), выполните практику 19–24.
6. Изучите [библиотеки и внешние данные](docs/04-libraries/25-collections.md) и сдайте [L03](labs/03-import-tool/README.md).
7. Пройдите [async, потоки и производительность](docs/05-concurrency/31-async.md) и сдайте [L04](labs/04-background-pipeline/README.md).
8. Изучите [тестирование и инженерную работу](docs/06-engineering/36-testing.md), выполните кейс сопровождения из главы 39.
9. Пройдите [SQL, Dapper и EF Core](docs/07-data/40-sql.md), выполните [L05](labs/05-durable-storage/README.md) на настоящей SQLite-базе.

```powershell
dotnet run --project examples/Foundations -- 01
dotnet run --project examples/ExpenseLedger -- labs/01-expense-ledger/data/valid.txt
python tools/verify.py
```

Для примеров нужен .NET 10 SDK, для скриптов — Python 3.10+.
Примеры 01–24 не требуют внешних NuGet-пакетов. Для 25–43 нужен первый restore из nuget.org;
версии и полный граф зависимостей закреплены. Engineering использует SDK 10.0.402 и MTP.
[Описание примеров](examples/README.md). Контейнерный тест главы 36 требует отдельного Linux Docker engine. L05 работает на SQLite без Docker.
Проверить только 01–24 без внешних пакетов: `python tools/verify.py --core-only`.

## Оглавление написанного материала

Порядок задаёт [book.json](book.json). Все перечисленные учебные страницы входят в общий PDF.
Будущие главы представлены в программе и не выдаются за готовые разделы.

<!-- TOC:START -->

### Начало работы

- [00. Как проходить практикум C# и .NET](docs/00-start.md)

### I. Первая самостоятельная программа

- [01. C# и .NET, рабочая среда](docs/01-foundations/01-platform.md)
- [02. Синтаксис и диагностика](docs/01-foundations/02-syntax.md)
- [03. Типы, числа и операции](docs/01-foundations/03-types.md)
- [04. Ветвления и циклы](docs/01-foundations/04-control.md)
- [05. Методы и декомпозиция](docs/01-foundations/05-methods.md)
- [06. Строки, null и культура](docs/01-foundations/06-text-null.md)
- [07. Массивы и базовые коллекции](docs/01-foundations/07-collections.md)
- [08. Классы и инварианты](docs/01-foundations/08-classes.md)
- [09. Ошибки, ресурсы и первые файлы](docs/01-foundations/09-errors-files.md)
- [10. Практикум ExpenseLedger CLI](docs/01-foundations/10-project.md)

### II. Система типов и выразительный код

- [11. Наследование и интерфейсы](docs/02-types/11-inheritance.md)
- [12. Структуры, записи и равенство](docs/02-types/12-values.md)
- [13. Обобщения и контракты типов](docs/02-types/13-generics.md)
- [14. Делегаты, лямбды и события](docs/02-types/14-delegates.md)
- [15. Перечисление и итераторы](docs/02-types/15-iterators.md)
- [16. LINQ в памяти и в запросах](docs/02-types/16-linq.md)
- [17. Шаблоны и расширение синтаксиса](docs/02-types/17-patterns.md)
- [18. Практикум библиотеки предметной области](docs/02-types/18-library.md)

### III. Полная карта языка и CLR

- [19. Сборки и модель исполнения](docs/03-runtime/19-assemblies.md)
- [20. Память и время жизни](docs/03-runtime/20-lifetime.md)
- [21. Атрибуты, reflection и dynamic](docs/03-runtime/21-reflection.md)
- [22. Структура исходников и инструменты компилятора](docs/03-runtime/22-compiler.md)
- [23. Ссылки, span и unsafe](docs/03-runtime/23-references.md)
- [24. Версии языка и чтение старого кода](docs/03-runtime/24-versions.md)

### IV. Библиотеки .NET и внешние данные

- [25. Коллекции и алгоритмы BCL](docs/04-libraries/25-collections.md)
- [26. Текст, время и регулярные выражения](docs/04-libraries/26-text-time.md)
- [27. Файлы, потоки и сериализация](docs/04-libraries/27-formats.md)
- [28. HTTP и сетевые клиенты](docs/04-libraries/28-http.md)
- [29. Host, DI, настройки и журналы](docs/04-libraries/29-host.md)
- [30. Практикум утилиты импорта](docs/04-libraries/30-import.md)

### V. Асинхронность, потоки и производительность

- [31. Task и async await](docs/05-concurrency/31-async.md)
- [32. Потоки и синхронизация](docs/05-concurrency/32-synchronization.md)
- [33. Параллельные конвейеры](docs/05-concurrency/33-pipelines.md)
- [34. Измерение и оптимизация](docs/05-concurrency/34-performance.md)
- [35. Практикум фонового обработчика](docs/05-concurrency/35-worker.md)

### VI. Тестирование и инженерная работа

- [36. Автоматические тесты](docs/06-engineering/36-testing.md)
- [37. Проектирование и изменение кода](docs/06-engineering/37-design.md)
- [38. Git, сборка и NuGet](docs/06-engineering/38-build-git.md)
- [39. Практикум сопровождения](docs/06-engineering/39-maintenance.md)

### VII. Данные и базы данных

- [40. SQL и реляционная модель](docs/07-data/40-sql.md)
- [41. ADO.NET и Dapper](docs/07-data/41-ado-dapper.md)
- [42. Entity Framework Core](docs/07-data/42-ef-core.md)
- [43. Практикум надёжного хранения](docs/07-data/43-durable-storage.md)

### Лабораторные работы

- [L01. Консольный журнал расходов](labs/01-expense-ledger/README.md)
- [L02. Библиотека правил и отчётов](labs/02-domain-library/README.md)
- [L03. Воспроизводимая утилита импорта](labs/03-import-tool/README.md)
- [L04. Ограниченный фоновый конвейер](labs/04-background-pipeline/README.md)
- [L05. Надёжное хранение расходов](labs/05-durable-storage/README.md)

### Приложения

- [A. Подсказки и разбор контрольных вопросов](docs/appendices/hints.md)
- [B. Карта покрытия языка C#](docs/appendices/language-map.md)
- [C. Источники и версии](docs/appendices/sources.md)
- [P. Программа практикума C# и .NET](PLAN.md)
<!-- TOC:END -->

## Два способа чтения

В репозитории главы разделены по темам, связаны переходами и ведут к условиям задач и исходникам.
В `dist/csharp.pdf` те же главы, условия лабораторных, подсказки, карта покрытия и программа
собраны в одну книгу. PDF содержит оглавление, закладки, номера страниц и внутренние ссылки.

Исходник текста один: Markdown. Новая глава подключается через book.json и появляется в обоих представлениях.
В PDF ссылки между учебными страницами внутренние. Ссылки на исходники и служебные файлы ведут
к соседним файлам репозитория относительно dist; при переносе одного PDF эти файлы нужно брать из репозитория.
Ссылки на официальную документацию требуют интернета.

## Собрать PDF

В Windows, из корня репозитория:

```powershell
python -m venv .venv
.venv/Scripts/python.exe -m pip install -r requirements-dev.txt
.venv/Scripts/python.exe tools/book.py check
.venv/Scripts/python.exe tools/book.py build
.venv/Scripts/python.exe tools/check_pdf.py
```

Linux/macOS: создайте venv тем же способом, используйте `.venv/bin/python`.
Для шрифтов по умолчанию нужны Arial и Consolas на Windows или DejaVu на Linux.
На macOS или другой системе передайте пути к трём TTF с кириллицей:

```text
python tools/book.py build --font /path/regular.ttf --bold-font /path/bold.ttf --mono-font /path/mono.ttf
```

После установки зависимостей сборка работает без сети. Pandoc и TeX не нужны.
`requirements.txt` достаточно для сборки, `requirements-dev.txt` добавляет проверку PDF.
Результат: `dist/csharp.pdf` и `dist/csharp.md`.

## Структура репозитория

| Путь | Содержимое |
| --- | --- |
| `PLAN.md` | Полный список тем, подтем, этапов и критериев |
| `docs` | Главы и приложения |
| `labs` | Условия лабораторных и входные данные |
| `examples` | Отдельные solution языка, интеграций, инженерных примеров и баз данных |
| `tools` | Сборка книги и проверки |
| `dist` | Готовые PDF и единый Markdown |
| `Books` | Дополнительные книги пользователя |
| `work` | Личные решения, исключены из Git |

Существующие `ConsoleApp1` и `ConsoleApp1.slnx` сохраняются как отдельный проект.
Порядок обновления курса описан в [CONTRIBUTING](CONTRIBUTING.md).
