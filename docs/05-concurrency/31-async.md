# 31. Task и async await

**Результат:** вы составляете несколько асинхронных операций, наблюдаете их ошибки и отмену,
освобождаете ресурсы и объясняете, где нужен поток. Нужны главы 14–15, 20, 27–30.
Запускаемый пример — [AsyncLab](../../examples/Integration/AsyncLab/Program.cs), режим 31;
проверки — [AsyncChecks](../../examples/Integration/PipelineChecks/AsyncChecks.cs).

## Операция, поток и ожидание

Task представляет завершение операции: успех, ошибку или отмену. Он не является потоком.
Для чтения сокета ОС может сообщить о готовности позже, а поток тем временем обслужит другую работу.
Для расчёта хеша процессор действительно выполняет инструкции; async не уменьшает их число.

| Нагрузка | Начальный выбор | Что ограничивать |
| --- | --- | --- |
| I/O: HTTP, файл, БД | Асинхронный API и await | Одновременные запросы, байты, deadline |
| CPU: расчёт, преобразование | Синхронный алгоритм; при необходимости Task.Run/Parallel | Занятые ядра и память |
| UI с долгим CPU-расчётом | Task.Run с кооперативной отменой | Частоту запуска, передачу результата в UI |

Не оборачивайте каждый ReadAsync в Task.Run: API уже умеет ожидать без блокировки.
На сервере Task.Run для каждой операции не создаёт дополнительной вычислительной мощности:
тот же ThreadPool получает ещё одну задачу. Конкурентность означает перекрытие операций во времени,
параллелизм — одновременное выполнение на нескольких исполнителях.

## Что компилятор делает с await

Обычный вызов async-метода начинает его синхронную часть на вызывающем потоке.
Если ожидаемая операция уже завершена, await сразу получает результат. Иначе метод сохраняет состояние,
регистрирует продолжение и возвращает управление вызывающему. После завершения операции продолжение
возобновляет метод. Локальные переменные, нужные после await, становятся частью состояния.
Новый поток при каждом await не создаётся. [Модель async](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/).

```csharp
// Самостоятельный фрагмент внутри async-метода.
var ready = new TaskCompletionSource<int>(
    TaskCreationOptions.RunContinuationsAsynchronously);
Task<int> pending = ready.Task;
Console.WriteLine(pending.IsCompleted); // False
ready.SetResult(42);
Console.WriteLine(await pending);       // 42
```

TaskCompletionSource позволяет адаптировать callback/event API: владелец завершает задачу через
TrySetResult, TrySetException или TrySetCanceled. Нужно снять подписку и согласовать гонку
«результат против отмены». RunContinuationsAsynchronously уменьшает риск исполнения чужого
продолжения прямо внутри SetResult и удерживаемой блокировки; это не делает окружающий код потокобезопасным.

await работает по шаблону: GetAwaiter(), IsCompleted, регистрация продолжения и GetResult().
Поэтому допустимы Task, ValueTask и другие awaitable-типы. Собственный awaiter — специальная техника;
для приложения почти всегда достаточно стандартных типов.

## Исключения и возвращаемый тип

В async Task-методе исключение, в том числе до первого await, сохраняется в возвращённой задаче.
Оно наблюдается при await. Обычный метод, который возвращает Task, но не объявлен async,
может выбросить исключение прямо при вызове. Синхронная обёртка удобна для немедленной проверки аргументов:

```csharp
// Метод класса; LoadCoreAsync — его асинхронная реализация.
public Task<string> LoadAsync(string path, CancellationToken token)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(path);
    return LoadCoreAsync(path, token);
}
```

Используйте Task для операции без значения, `Task<T>` для результата. async void оставляйте
только обработчикам событий, чей делегат требует void: вызывающий не сможет await такую операцию
и обработать её исключение как ошибку Task. В обработчике нужна собственная граница ошибок.
Отбрасывание задачи через `_ = SaveAsync()` не решает ни владение, ни журналирование, ни остановку.

## WhenAll и WhenAny

Последовательные await выполняют шаги последовательно. Чтобы перекрыть независимые I/O-операции,
сначала вызовите обе, затем await Task.WhenAll. WhenAll завершается после всех участников;
результаты `Task<T>[]` возвращаются в порядке входных задач. Ошибка одной не отменяет остальные.
При нескольких сбоях await пробрасывает одно исключение, а Exception агрегирующей задачи содержит все.
[Договор WhenAll](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.whenall?view=net-10.0).

```csharp
// tasks — небольшой заранее ограниченный массив Task.
Task all = Task.WhenAll(tasks);
try { await all; }
catch
{
    if (all.Exception is { } aggregate)
        foreach (Exception error in aggregate.Flatten().InnerExceptions)
            Console.Error.WriteLine(error.GetType().Name);
    throw;
}
```

В [ObserveAll](../../examples/Integration/AsyncLab/AsyncPatterns.cs) каждый вызов обёрнут в отдельный
async-метод. Результат хранит Value, Error и Cancelled, поэтому сохраняются также синхронные ошибки фабрик.
Для трёх операций — результат 7, FormatException и IOException — возвращаются три исхода в исходном порядке.
Это удобный договор частичного успеха. Отмена в нём представлена данными и не выбрасывается наружу.

WhenAny возвращает первую завершённую **задачу**, а не её значение. После `Task winner = await Task.WhenAny(...)`
нужно `await winner`, чтобы получить значение или исключение. Проигравшие продолжают работать.
Если они больше не нужны, отмените их через принадлежащий вам CTS и всё равно дождитесь завершения.
WhenAll над миллионом созданных задач не является ограниченным конвейером; к этому вернёмся в главе 33.

## Отмена и предел времени

CancellationTokenSource принадлежит инициатору операции, CancellationToken передаётся вниз.
Cancel — просьба завершиться, а не принудительное убийство потока. I/O должен получить token;
CPU-цикл должен периодически вызывать ThrowIfCancellationRequested. CTS освобождает владелец.

```csharp
// Фрагмент границы операции; callerToken приходит от вызывающего.
using var deadline = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
deadline.CancelAfter(TimeSpan.FromSeconds(5));
try
{
    await DownloadAndReadBodyAsync(deadline.Token);
}
catch (OperationCanceledException) when (!callerToken.IsCancellationRequested)
{
    throw new TimeoutException("Deadline expired.");
}
```

Такой перевод ошибки корректен, если DownloadAndReadBodyAsync использует переданный token
и не применяет независимую отмену с другим смыслом. При одновременных причинах здесь приоритет у callerToken.
Лимит охватывает отправку, повторы и чтение тела; это уже реализовано в HTTP-клиенте L03.

Task.WaitAsync(token) отменяет **ожидание**, но не исходную операцию. Не освобождайте её Stream/CTS,
пока она ещё использует их. При остановке группы нужен общий token, переданный каждой операции,
и последующий join через WhenAll. Выход по тайм-ауту не откатывает уже сохранённые данные.

## Контекст продолжения и deadlock

UI обычно имеет SynchronizationContext, возвращающий продолжение на UI-поток. Если этот поток
блокируется через .Result или .Wait(), а операция ждёт продолжение на нём же, возникает deadlock.
Исправление — async до границы события/Main и await вместо синхронного ожидания.
GetAwaiter().GetResult() меняет форму пробрасывания исключения, но по-прежнему блокирует поток.

ConfigureAwait(false) сообщает конкретному await, что возвращение в захваченный контекст не требуется.
Это полезно в библиотеке, которая не зависит от UI; после такого await нельзя считать себя на UI-потоке.
Оно не заставляет код обязательно перейти на другой поток и не отключает ExecutionContext/AsyncLocal.
В консольном приложении и ASP.NET Core по умолчанию нет UI-подобного контекста,
но блокирующие ожидания всё равно могут истощать ThreadPool.
[Разбор ConfigureAwait](https://devblogs.microsoft.com/dotnet/configureawait-faq/).

Проверка курса использует управляемый QueueContext: продолжение ожидает ручной обработки очереди.
Она воспроизводит причину зависания без намеренного вечного deadlock процесса.

## ValueTask, async streams и освобождение

`ValueTask<T>` может содержать готовое значение без отдельного Task, ссылку на Task или повторно используемый
источник завершения. Это оптимизация для измеренного горячего пути, а не универсальная замена Task.
По общему договору экземпляр ожидают один раз. Если нужно повторное ожидание или WhenAll,
однократно вызовите AsTask и используйте полученный Task. Не читайте Result незавершённого ValueTask.
[Ограничения ValueTask](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask?view=net-10.0).

`IAsyncEnumerable<T>` отдаёт элементы по мере готовности через await foreach.
MoveNextAsync возвращает `ValueTask<bool>`; DisposeAsync освобождает перечислитель.
В async-итераторе `[EnumeratorCancellation]` связывает параметр token с WithCancellation у потребителя.
Само перечисление не запускает обработку элементов параллельно.

```csharp
// rows — IAsyncEnumerable<byte[]>; ProcessAsync обрабатывает одну строку.
await foreach (byte[] row in rows.WithCancellation(token))
    await ProcessAsync(row, token);
```

await using ожидает IAsyncDisposable.DisposeAsync даже при исключении или досрочном выходе.
Не заменяйте его синхронным ожиданием DisposeAsync. Span и другие ref struct не могут сохраняться
через точку приостановки: держите Memory/массив, а Span получайте внутри синхронного участка.
[Асинхронные потоки](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/generate-consume-asynchronous-stream).

## Практика

**31-A.** Выполните `dotnet run --project examples/Integration/AsyncLab -- 31`.
Ожидаются `success=1;errors=2`, число 0 и `disposed=True`.
Перед запуском объясните порядок; затем замените break на полное перечисление и получите 0, 1, 2.
Критерий: finally перечислителя выполняется ровно один раз в обоих случаях.

**31-B.** Создайте загрузчик трёх небольших HTTP-документов с общим token и результатом для каждого URL.
Подмените транспорт: 200 с JSON, 404, исключение I/O. Ожидаются один успех и две отдельные ошибки
в порядке входа; публичный сервер для теста не нужен. Ограничьте пакет тремя запросами.
Критерий: дождались всех задач и не потеряли ошибку, возникшую до возврата Task.

**31-C.** Добавьте deadline и отмену пользователя, затем смоделируйте зависшее чтение тела.
Ожидаются различимые timeout/cancelled и завершение всех начатых операций до освобождения транспорта.
Критерий: тест управляется сигналами, а не надеждой, что произвольный Sleep успеет создать нужную гонку.

## Контроль

Почему два последовательных await не создают конкурентности? Кто наблюдает исключение проигравшего WhenAny?
Можно ли освободить файл после отмены только WaitAsync? Почему ConfigureAwait(false) не заменяет lock?

[Назад: 30](../04-libraries/30-import.md) · [Далее: 32](32-synchronization.md) ·
[Подсказки](../appendices/hints.md)
