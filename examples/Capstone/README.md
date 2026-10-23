# Опорный пример глав 59–60 и L08

BudgetRules — чистое правило месячного лимита. AcceptanceChecks — независимые fixture cases,
проверка одноразового перечисления/disposal и воспроизводимая гонка между снимком и commit.
Это два проекта net10.0 / C# 14 без внешних NuGet-пакетов. SDK 10.0.402 закреплён global.json;
lock-файлы фиксируют отсутствие пакетов и project reference.

```powershell
dotnet restore examples/Capstone/Capstone.slnx --locked-mode
dotnet build examples/Capstone/Capstone.slnx -c Release --no-restore
dotnet examples/Capstone/AcceptanceChecks/bin/Release/net10.0/AcceptanceChecks.dll labs/08-capstone/data/budget-cases.json
python tools/verify_capstone.py
```

Ожидаются 47 успешных проверок: 12 случаев в трёх культурах, состав набора, точная граница,
один обход, disposal, null, максимальное число строк, неизменность входа и два опыта конкуренции.
Полный verifier дополнительно требует два ожидаемых отказа намеренно ошибочных вариантов:

- `strict-boundary` заменяет <= на < внутри test driver; отказ происходит на fixture `exact`.
- `double-enumeration` добавляет Count перед обходом; отказ — `Sequence enumerated twice.`.

Эти переключатели принимает только AcceptanceChecks. Библиотека BudgetPolicy не изменяется,
и дефект не добавляется в прежние приложения. Вызов driver с мутацией вручную должен завершиться
кодом 1; verifier проверяет именно этот код и причину. Результаты — `.artifacts/capstone/summary.json`.

В опыте гонки два участника сначала читают 600, затем оба разрешают 300 при лимите 1000: итог 1200.
Барьер обеспечивает порядок без Sleep. Второй опыт защищает проверку и изменение одним lock:
принимается один расход, итог 900. Это гарантия одного процесса, не проверка транзакции БД.

Policy не группирует по owner/месяцу, не авторизует, не устраняет дубликаты и не записывает данные.
Она получает только новые суммы одной уже выбранной группы. Интеграция в API/БД/клиент,
проверка двух процессов и CR-01 — самостоятельное задание [L08](../../labs/08-capstone/README.md).
Выполнение этих 47 проверок не выставляет балл за проект учащегося.
