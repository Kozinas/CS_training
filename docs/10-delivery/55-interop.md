# 55. Interop и специальные среды

**Результат:** вы вызываете Win32-функцию с правильной сигнатурой, закрываете принадлежащий вам handle,
объясняете ограничения Native AOT и проверяете выгрузку плагина. Нужны главы 19–23, 27, 31 и 38.
Примеры — [InteropProbe и PluginHost](../../examples/Delivery/README.md).

## На границе важен ABI

P/Invoke связывает managed-метод с экспортируемой native-функцией. Компилятор C# проверяет вашу
объявленную сигнатуру, но не доказывает, что она совпала с C-заголовком. ABI определяет размеры,
выравнивание, способ передачи параметров и результата, соглашение вызова и владение памятью.
Ошибка здесь может повредить процесс, даже если проект собрался без предупреждений.

| Native-договор | Типичная managed-модель | Что уточнить |
| --- | --- | --- |
| DWORD Windows | uint | 32 бита также на x64 |
| HANDLE / указатель | SafeHandle / nint | Владение и недопустимое значение |
| LARGE_INTEGER | long в нужной сигнатуре | 64-битное значение, не указатель |
| BOOL Windows | bool с явным marshaling | Это 32-битный BOOL, не однобайтный C++ bool |
| wchar_t* / char* | Строка с явной кодировкой либо указатель | UTF-16 Windows, UTF-8 или другая кодировка по API |

Слово long в C/C++ не всегда равно C# long: Windows и Unix имеют разные модели данных.
Для struct важны StructLayout, порядок полей, padding и packing; Pack=1 не является универсальным
исправлением. На x86 особенно заметно несовпадение cdecl/stdcall. Сначала прочитайте заголовок и
документацию функции, затем задайте сигнатуру и проверьте граничный вызов.
[Правила interop](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/best-practices).

## LibraryImport и прежний DllImport

DllImport объявляет static extern метод. LibraryImport описывает static partial метод, для которого
генератор создаёт marshalling-код во время сборки. Это делает переход обозримым для анализаторов и AOT.
Поддержка сложных сигнатур отличается; предупреждение генератора нужно разобрать, а не просто отключить.
Для сгенерированного interop проект разрешает unsafe, даже когда публичный метод не содержит указателей.
[Генерация P/Invoke](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke-source-generation).

В NativeFile читается размер настоящего файла Windows:

```csharp
[LibraryImport("kernel32.dll", EntryPoint = "GetFileSizeEx",
    SetLastError = true)]
[return: MarshalAs(UnmanagedType.Bool)]
private static partial bool GetFileSize(
    SafeFileHandle file, out long size);
```

CreateFileW получает UTF-16 путь, доступ только для чтения, OPEN_EXISTING и FILE_SHARE_READ.
Неудачное открытие возвращает недопустимый handle, а не готовое managed-исключение. Код немедленно
читает Marshal.GetLastPInvokeError, закрывает wrapper и создаёт Win32Exception с сохранённым кодом.
Последующий native-вызов может изменить last error; его нельзя считывать после произвольной диагностики.
[CreateFileW](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew).

Строка с внутренним NUL отклоняется до P/Invoke: native-функция могла бы открыть только префикс пути.
Недоверенный путь также требует отдельной проверки разрешённого каталога в приложении, если пользователь
не должен читать произвольные файлы. Наш CLI — локальная команда, которой владелец явно передаёт свой файл.
Нельзя подставлять пользовательское имя DLL в библиотечную константу ради «расширяемости».

## Владение: handle, buffer, callback

SafeFileHandle владеет результатом CreateFileW и закрывает его через CloseHandle. Using задаёт обычный
своевременный выход; finalization остаётся страховкой. При передаче SafeHandle в P/Invoke runtime
удерживает его на время вызова. Извлечение DangerousGetHandle снимает часть этих гарантий и не нужно
для этого примера. Метод Size отказывается работать с закрытым wrapper.

Указатель не сообщает, кто его освобождает. Возвращённый native-буфер освобождают именно функцией
и allocator, указанными API. Нельзя обменивать Marshal.FreeHGlobal, NativeMemory.Free и чужой free
по сходству названия. Borrowed-указатель не становится вашим, если обернуть его в owning SafeHandle.
Для borrowed/owned ресурсов полезны разные типы или явно разные фабрики.

Callback должен жить не меньше native-подписки. Если передали delegate как function pointer,
удерживайте delegate и снимите подписку до освобождения владельца. UnmanagedCallersOnly подходит
для допустимой статической сигнатуры, но не превращает managed-исключение в native error code.
На границе перехватывайте ожидаемые ошибки и возвращайте результат по native-договору.

## COM и Windows API

COM использует интерфейсы с IID, HRESULT и reference counting. Managed-код может работать через
interop-обёртки или подходящие source-generated COM-интерфейсы; это отдельный договор от P/Invoke
плоской C-функции. Apartment STA/MTA влияет на допустимый поток и marshaling между потоками.
Автоматизация UI/Office не становится безопасной для ThreadPool после добавления await.

Не вызывайте ReleaseComObject в каждом месте «на всякий случай»: несколько managed-потребителей
могут разделять wrapper. Определите владельца, threading model и правила конкретного компонента.
В этой главе исполняется Win32 file API; COM рассматривается как направление интеграции без
утверждения о запуске Office, регистрации COM-server или поддержке любого COM-кода под AOT.
[COM interop](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/cominterop).

## Trimming и Native AOT

Trimming удаляет недостижимый код, Native AOT компилирует приложение заранее в native executable.
Динамическая reflection, поиск типов по строке, генерация IL и загрузка произвольных managed-сборок
могут нарушить предположения этих режимов. IsAotCompatible включает анализ, но успешная обычная сборка
не заменяет publish и запуск опубликованного результата.

InteropProbe использует JsonSerializerContext, поэтому сериализатор знает FileReport на этапе сборки.
Reflection-based сериализация отключена свойством проекта; случайный новый вызов без metadata не
должен молча стать скрытой зависимостью. Для reflection-анализаторов задавайте только обоснованные
DynamicallyAccessedMembers/DynamicDependency; широкое сохранение всей сборки скрывает проблему и увеличивает результат.
[Подготовка к trimming](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/prepare-libraries-for-trimming),
[Native AOT](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/).

Проверка выпуска действительно публикует win-x64 AOT и выполняет его. На Windows для этого нужен
C++ toolchain из Visual Studio, а не только .NET SDK. Поддержка одной платформы не доказывает
кросс-компиляцию для другой. Варианты публикации подробно сравниваются в [главе 57](57-publishing.md).

## Плагины и AssemblyLoadContext

PluginHost работает под обычным JIT-runtime. Он создаёт collectible AssemblyLoadContext, разрешает
зависимости через AssemblyDependencyResolver и разделяет PluginContracts с default context.
Если загрузить вторую копию интерфейса, одинаковое имя IReportRule не гарантирует одинаковую identity типа.
Граница контракта должна быть небольшой и не передавать внутренние типы плагина наружу без необходимости.

Unload только начинает выгрузку. Пока static, событие, Task, stack frame или другой GC root удерживает
тип/экземпляр, контекст живёт. Проверка использует NoInlining, WeakReference и три наблюдения:
обычный плагин выгрузился; сохранённый static удержал его; после Release контекст собрался.
Принудительный GC допустим в таком диагностическом опыте, но не должен стать production-циклом обслуживания.
[Выгрузка сборок](https://learn.microsoft.com/en-us/dotnet/standard/assembly/unloadability).

ALC не ограничивает права кода. Плагин в процессе может читать файлы и завершать приложение с правами
хоста. Для недоверенного расширения нужна отдельная граница процесса и реальные ограничения ОС/контейнера,
квоты и протокол. Даже отдельный процесс без ограничений прав сам по себе не является sandbox.

При запуске процесса передавайте аргументы через ProcessStartInfo.ArgumentList, отдельно читайте stdout
и stderr и задавайте deadline. WaitForExitAsync с отменой прекращает ожидание; это не равнозначно завершению
дочернего процесса. Сценарии курса останавливают только созданные ими процессы и различают normal stop
и намеренное Terminate для испытания восстановления.

## Запуск, вопросы и практика

```text
dotnet run --project examples/Delivery/InteropProbe -- size README.md
dotnet run --project examples/Delivery/PluginHost -- examples/Delivery/SamplePlugin/bin/Release/net10.0/SamplePlugin.dll
python tools/verify_delivery.py
```

Сначала соберите Delivery.slnx в Release по [инструкции](../../examples/Delivery/README.md).
Interop выдаёт размер, Closed=true и RejectsClosed=true. У отсутствующего файла код native-error равен 2,
код завершения CLI — 3. PluginHost должен показать Value=1200 и три true: Unloaded, Retained, Released.

1. Почему DWORD нельзя автоматически заменить на nint при переходе на x64?
2. Чем чужой borrowed handle отличается от результата CreateFileW, которым владеет приложение?
3. Почему Unload не является ни немедленным освобождением, ни sandbox?

**55-A.** Вход: пустой файл, 256 байт, Unicode-путь с пробелами и отсутствующий путь.
Ожидается точный размер либо native-error=2. Критерий: handle закрыт при каждом исходе, после Dispose
нельзя повторить вызов, ABI и кодировка объяснены по native-документации.

**55-B.** Добавьте второй доверенный плагин и воспроизведите удержание через событие хоста.
Вход: загрузка, подписка, Unload, отписка. Ожидается сохранение контекста до отписки и сборка после неё.
Критерий: общий contract загружается один раз, нет вывода «выгружено» только по возврату Unload.

**55-C.** Оберните native-ресурс с отдельной функцией освобождения либо вынесите плагин в процесс
с ограниченным протоколом. Вход: ошибка создания, обычный ответ, таймаут и неверная длина сообщения.
Критерий: описаны allocator/lifetime или граница процесса, нет утечки ресурса/зависшего процесса,
опубликованный выбранный режим проверен; ограничения AOT и изоляции явно указаны.
[Подсказки](../appendices/hints.md).
