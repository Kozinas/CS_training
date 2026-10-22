# Источники и версии

База курса — C# 14 и .NET 10. Дата сверки: 22 октября 2026. Учебные объяснения и задания
написаны самостоятельно; книги используются как дополнительное чтение.
Содержимое папки Books не включается в PDF курса.

## Официальные источники

| Назначение | Источник |
| --- | --- |
| Синтаксис и семантика | [C# language reference](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/) |
| Нормативная модель и feature specifications | [C# specification](https://learn.microsoft.com/en-us/dotnet/csharp/specification/) |
| Версия языка | [C# 14](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14) |
| Поддержка SDK/runtime | [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) |
| Работа с проектами | [.NET CLI](https://learn.microsoft.com/en-us/dotnet/core/tools/) |
| Стандартная библиотека | [.NET API browser](https://learn.microsoft.com/en-us/dotnet/api/) |
| Nullable-анализ | [Nullable reference types](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/null-safety/nullable-reference-types) |
| Сравнение строк | [String best practices](https://learn.microsoft.com/en-us/dotnet/standard/base-types/best-practices-strings) |
| Веб-разработка | [ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/) |
| ORM | [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/) |
| Объектная модель | [Интерфейсы](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/interface), [records](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record) |
| Generic-контракты | [Ограничения типов](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/generics/constraints-on-type-parameters) |
| Отложенное выполнение | [Yield](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/yield), [LINQ](https://learn.microsoft.com/en-us/dotnet/standard/linq/deferred-execution-lazy-evaluation) |
| Современный синтаксис | [Patterns](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/patterns), [extension members](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/extension-methods) |

Справочник языка обновляется и может упоминать preview-возможности; в курс их включают только с явной пометкой.
Существование API в документации не подтверждает его доступность в выбранном TFM.
Запускаемые примеры компилируются под net10.0 и LangVersion 14.0.

## Книги в репозитории

| Книга | Файл в Books | Для чего использовать |
| --- | --- | --- |
| Adam Freeman, Pro ASP.NET Core 6, 9th edition, 2022 | `Adam_Freeman_Pro_ASP_NET_Core_6_Develop_Cloud_Ready_Web_Applications.pdf` | HTTP pipeline, MVC, Razor Pages, Blazor; главы 44–51 |
| Xiaodi Yan, Web API Development with ASP.NET Core 8, 2024 | `Xiaodi_Yan_Web_API_Development_with_ASP_NET_Core_8_Learn_techniques.pdf` | Проектирование и проверка API; главы 44–49 |
| Jeffrey Richter, CLR via C#, русское издание | `Мастер_класс_Джеффри_Рихтер_CLR_via_C#_Программирование_на_платформе.pdf` | Типы, CLR, ресурсы и конкурентность; главы 11–24, 31–32 |

В первых двух файлах сверены титульные страницы. У файла Рихтера начальные страницы представлены
без извлекаемого текста; номер издания здесь не утверждается.
Вместо ненадёжных номеров PDF-страниц ищите соответствующую тему в оглавлении своего издания.
Для начала курса книги не обязательны.

В старых книгах встречаются API и настройки эпохи .NET Framework, ASP.NET Core 6 или 8.
Изучайте объяснение задачи, затем проверяйте современный способ по документации.
Например, рекомендации о hosting model, nullable, пакетах и версиях SDK нельзя переносить механически.

## Внешние библиотеки следующих блоков

Версии внешних NuGet-пакетов пока не зафиксированы, потому что главы с их примерами ещё не написаны.
При наполнении каждой главы выбирается совместимая стабильная версия, выполняется restore/build/test
и приводится ссылка на официальную документацию пакета. Текущие два блока не используют внешние NuGet-пакеты.

## Инструменты книги

PDF собирается из Markdown с помощью
[markdown-it-py](https://markdown-it-py.readthedocs.io/en/latest/using.html) и
[ReportLab](https://www.reportlab.com/docs/reportlab-userguide.pdf).
Кириллические TrueType-шрифты встраиваются в PDF. Проверка структуры использует pypdf.
Основа структуры и сборщика адаптирована из соседнего курса SystemVerilog пользователя;
учебный текст и примеры здесь относятся к C#.
