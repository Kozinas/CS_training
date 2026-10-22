# Примеры тестирования и сопровождения

Главы 36–39 используют [Engineering.slnx](Engineering.slnx).
Решение содержит ReviewRules, ReviewDemo, два xUnit-проекта и четыре ранее написанные зависимости.
Container.Tests — отдельный проект с настоящим PostgreSQL через Testcontainers.
Объяснения и задания находятся в [главе 36](../../docs/06-engineering/36-testing.md)
и [практикуме сопровождения](../../docs/06-engineering/39-maintenance.md).

## Среда

SDK выбирается по [global.json](global.json): .NET 10.0.402 с latestPatch; ниже 10.0.400 режим MTP
этого примера не поддерживается. Версии пакетов находятся в [Directory.Packages.props](Directory.Packages.props),
разрешённые графы — в packages.lock.json. Новый NuGet.Config и CPM действуют только в Engineering.
Другие решения и исходный ConsoleApp1 сохраняют свои настройки.

| Пакет | Версия | Назначение |
| --- | --- | --- |
| xunit.v3.mtp-v2 | 4.0.2 | xUnit.net v3, запуск на Microsoft.Testing.Platform v2 |
| Microsoft.Testing.Extensions.TrxReport | 2.5.1 | Машинный отчёт результатов |
| Microsoft.Testing.Extensions.CodeCoverage | 18.12.0 | Cobertura для unit-тестов |
| Microsoft.Extensions.TimeProvider.Testing | 10.11.0 | FakeTimeProvider |
| NSubstitute | 6.2.0 | Подмена IReviewSink и проверка вызовов |
| Testcontainers.PostgreSql | 4.16.0 | Изолированный PostgreSQL в Docker |
| Npgsql | 10.0.3 | Реальное соединение и проверка unique constraint |

Название xUnit.net v3 обозначает поколение framework; версия закреплённого пакета — 4.0.2.
Не смешивайте команды VSTest `--collect` с опциями MTP `--coverage`.

## Сборка и проверки

Из корня репозитория:

```powershell
python tools/verify_engineering.py
python tools/practice_git.py --keep
```

Основная проверка делает locked restore, Release build, проверку форматирования,
27 unit-кейсов и 6 loopback/контрактных кейсов. Затем собирает .nupkg,
создаёт независимого потребителя с чистым кэшем и локальным feed, проверяет locked restore
и отказ при рассогласовании версии. Во временной копии воспроизводится дефект `>=` → `>`;
ожидаемое падение регрессионного теста проверяется по TRX и конкретному сообщению assertion.
Git-практикум создаёт собственный временный репозиторий без remote и разрешает реальный конфликт.
Результаты и временные данные лежат в .artifacts; `--keep` сохраняет Git-песочницу для изучения.

Ручной запуск из корня с правильной рабочей папкой:

```powershell
Push-Location examples/Engineering
try {
    dotnet restore Engineering.slnx --locked-mode
    dotnet build Engineering.slnx -c Release --no-restore
    dotnet test --project ReviewRules.Tests -c Release --no-build
    dotnet test --project Integration.Tests -c Release --no-build
    dotnet run --project ReviewDemo -- 1000 1800 food
} finally { Pop-Location }
```

Demo принимает сумму, возраст в секундах и категорию. Ожидаемый результат примера — Expired.
Фиксированные часы позволяют проверять его как отдельный процесс. Это демонстрация policy,
а не новый импортёр: проверка файла и доменная модель расходов остаются в прежних работах.

## Контейнерный тест

```powershell
python tools/verify_engineering.py --containers
```

Требуется работающий Docker с Linux containers и доступ к образу при первом запуске.
Образ PostgreSQL 17.6-alpine закреплён digest, полученным из официального registry;
это тестовый fixture, а не выбор версии для production-развёртывания.
Тест создаёт таблицу с UUID primary key и проверяет, что второй INSERT того же Id отклоняется
самой БД с SQLSTATE 23505. Все подключения и контейнер освобождаются через await using.
Подробное изучение SQL и реального хранилища продолжится в главах 40–43.

В среде этого выпуска Docker CLI установлен, но daemon недоступен. Container.Tests восстановлен
и собран без предупреждений; запуск контейнера не выполнялся и не засчитывается как прошедший тест.
При явном --containers отсутствие Docker считается ошибкой, а не тихим skip.

## Покрытие, упаковка и CI

Отчёт .artifacts/engineering/summary.json содержит число тестов и наблюдаемое покрытие ReviewRules.
Процент не используется как доказательство правильности. Полный краткоживущий Cobertura можно сохранить
ручной командой из главы 36. Результат дефектного прогона — .artifacts/engineering/boundary-repro.txt.

В [Packaging](Packaging/Consumer.csproj.template) лежит шаблон потребителя без ProjectReference.
Временный feed и отдельный NuGet-кэш исключают попадание старого пакета с тем же номером версии.
Ни публикация на nuget.org, ни постоянная установка инструментов не выполняется.
[PACKAGE.md](ReviewRules/PACKAGE.md) включается в пакет как его README.

Workflow [verify.yml](../../.github/workflows/verify.yml) повторяет проверки курса на Windows,
собирает единый PDF и сохраняет артефакты на 7 дней. Контейнеры в этом job не запускаются.
Локальная успешная проверка не означает, что GitHub Actions уже выполнил удалённый job.
