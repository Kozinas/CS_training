# Запускаемые примеры C#

Примеры относятся к написанным главам 01–10. Solution курса — [CSharpCourse.slnx](CSharpCourse.slnx).
Она независима от пользовательского ConsoleApp1 в корне.

## Запуск

Из корня репозитория:

```powershell
dotnet build examples/CSharpCourse.slnx -c Release
dotnet run --project examples/Foundations -- 03
dotnet run --project examples/ExpenseLedger -- labs/01-expense-ledger/data/valid.txt
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
| ContractChecks | Проверки парсера в трёх культурах и инвариантов модели без внешнего test framework |

[Directory.Build.props](Directory.Build.props) задаёт net10.0, C# 14, nullable и ошибки при предупреждениях.
[global.json](global.json) выбирает стабильный SDK 10.0 с допустимым обновлением feature band.
При работе из корня выбор SDK обычно определяется корнем; проверочный скрипт запускает сборку из examples,
чтобы использовать этот global.json.

В [NuGet.Config](NuGet.Config) источники отключены: в первом блоке внешних пакетов нет, restore работает офлайн.
При добавлении будущих примеров с пакетами конфигурацию потребуется явно расширить и зафиксировать версии.
Этот файл действует на проекты examples и не меняет настройки ConsoleApp1.

Пример 09 создаёт собственный временный файл с уникальным именем, читает его и удаляет в finally.
ExpenseLedger читает только переданный файл. Свои эксперименты выполняйте в work.
