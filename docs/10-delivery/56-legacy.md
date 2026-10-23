# 56. Существующие проекты на .NET Framework

**Результат:** вы исследуете старое приложение, извлекаете переносимую библиотеку и подтверждаете
сохранение поведения на двух runtime. Нужны главы 19, 36–39 и 55.
Пример — [LegacyRules, PortableRules и CompatibilityProbe](../../examples/Delivery/README.md).

## Framework не является прежним названием .NET 10

.NET Framework — Windows-платформа с собственными CLR, установкой и библиотеками. Современный .NET
имеет другие модели развёртывания, загрузки и доступные API. TargetFramework=net48 не меняется на
net10.0 механически, если приложение зависит от System.Web, старого WCF-server, COM или vendor-контрола.
Новый SDK может собрать SDK-style net48-проект, а выполняться такой exe будет под Framework.

В учебном примере LegacyRules и один target CompatibilityProbe действительно используют net48.
Reference assemblies приходят через Microsoft.NETFramework.ReferenceAssemblies 1.0.3; для исполнения
на Windows нужен установленный Framework 4.8 или совместимый 4.x. Пакет reference assemblies не
устанавливает runtime. Здесь проекты уже записаны в SDK-style, чтобы воспроизводить сборку; это
не утверждение, что автоматически перенесён любой старый non-SDK csproj.
[Обзор переноса](https://learn.microsoft.com/en-us/dotnet/core/porting/framework-overview).

## Инвентаризация перед изменением

Соберите исходный проект, запишите runtime, архитектуру и способы запуска. Найдите зависимости
на registry, GAC, COM, Windows Service/IIS, файлы рядом с exe, machine.config и переменные окружения.
Проверьте installer, фоновые задания, миграции БД, лицензии контролов и native DLL. Нельзя оценить
готовность миграции только по числу компилирующихся файлов.

В старом csproj могут быть перечислены Compile/Reference, HintPath, Import .targets и события build.
Packages.config хранит установленные версии, а package scripts могли менять проект при установке.
PackageReference задаёт граф иначе; переход требует проверки content/build assets и restore, а не
только замены XML-тега. Сохраняйте исходный рабочий вариант в Git и меняйте по одной границе.

```xml
<!-- Фрагмент классического проекта; не самостоятельный csproj. -->
<PropertyGroup>
  <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
</PropertyGroup>
<ItemGroup>
  <Reference Include="System.Configuration" />
  <Compile Include="LegacyQuote.cs" />
</ItemGroup>
```

SDK-style по умолчанию включает .cs по glob. После переноса старого списка Compile можно получить
дубликаты. Также сравните AssemblyInfo, ресурсы, условные symbols, платформы x86/AnyCPU и config-файлы.
[Миграция packages.config](https://learn.microsoft.com/en-us/nuget/consume-packages/migrate-packages-config-to-package-reference).

## Binding redirects, GAC и AppDomain

Framework связывает strong-named сборки по identity, включая версию. Binding redirect в конфигурации
может направить старую ссылку на другую версию, но не создаёт отсутствующий метод и не доказывает
бинарную совместимость. У приложения важен exe.config; App.config библиотеки не становится автоматически
конфигурацией каждого хоста. GAC — глобальный механизм Framework, не обычная папка NuGet и не модель
развёртывания современного .NET.
[Binding redirects](https://learn.microsoft.com/en-us/dotnet/framework/configure-apps/redirect-assembly-versions).

AppDomain использовался в Framework для изоляции загрузки, конфигурации и lifetime с особыми правилами
взаимодействия. В современном .NET нельзя перенести создание дополнительных AppDomain как готовое
решение. Для загрузки плагинов есть ALC с другими гарантиями, для надёжной границы — процессы.
Remoting, CAS и старые предположения о частичном доверии не являются новым способом sandbox.
[Недоступные технологии Framework](https://learn.microsoft.com/en-us/dotnet/core/porting/net-framework-tech-unavailable).

## Что делать с распространёнными стеками

| Исходная технология | Что исследовать при переносе |
| --- | --- |
| Web Forms / System.Web | Новую модель UI и запросов; ASP.NET Core не запускает старые страницы без изменения |
| ASP.NET MVC 5 | Routing, filters, auth, DI, middleware, binding и представления в ASP.NET Core |
| WCF-server | Договоры клиентов, поддерживаемые bindings/features; варианты CoreWCF или новая граница API |
| EF6 | Совместимость провайдера и runtime; EF Core имеет отличия поведения и миграций |
| WinForms/WPF | Windows-only зависимости, контролы, ресурсы, binding и установка |

Выбирайте по ограничениям системы. Иногда библиотеку можно перенести раньше UI; иногда нужно временно
сохранить старый хост и соединить его с новым сервисом. Изменение технологии транспорта не должно
незаметно менять error contract, порядок операций и правила авторизации.
[Перенос ASP.NET](https://learn.microsoft.com/en-us/aspnet/core/migration/fx-to-core/?view=aspnetcore-10.0),
[CoreWCF](https://github.com/CoreWCF/CoreWCF).

## Извлечение правила на настоящем примере

LegacyQuote.Total читает feeRate из App.config хоста и считает условную сервисную надбавку.
Это учебная формула, не тариф или налог. Её наблюдаемый договор: amount в диапазоне 0..1000000,
rate в 0..1, decimal и округление MidpointRounding.ToEven до двух знаков.

В PortableRules зависимость от ConfigurationManager убрана: rate передаётся явно. Хост отвечает
за конфигурацию, правило — за расчёт и инварианты. Библиотека target netstandard2.0 позволяет использовать
один контракт из Framework и современного .NET. Netstandard2.1 для .NET Framework не подходит;
netstandard — поверхность API, не версия языка и не runtime для запуска приложения.
[.NET Standard](https://learn.microsoft.com/en-us/dotnet/standard/net-standard).

```csharp
public static decimal Total(decimal amount, decimal rate)
{
    if (amount < 0 || amount > 1000000m)
        throw new ArgumentOutOfRangeException(nameof(amount));
    if (rate < 0 || rate > 1)
        throw new ArgumentOutOfRangeException(nameof(rate));
    return decimal.Round(checked(amount * (1 + rate)),
        2, MidpointRounding.ToEven);
}
```

Не заменяйте ToEven на AwayFromZero в рамках «технической миграции». При amount=10.15 и rate=0.10
результат 11.165 округляется до 11.16; у amount=10.05 получается 11.055 → 11.06.
Такое различие легко пропустить на наборе только целых сумм.

## Characterization tests и две реализации

Сначала фиксируется наблюдаемое поведение старой системы. Если оно ошибочно, его исправление оформляют
отдельным изменением договора. Тест «новое равно старому» полезен, но обе реализации могут содержать
одинаковую ошибку; нужен ещё независимый ожидаемый ответ.

CompatibilityProbe имеет targets net48 и net10.0. Под Framework он вызывает и LegacyRules, и PortableRules,
под современным .NET — PortableRules. Проверяются известные суммы, две границы отказа и три культуры:
en-US, ru-RU, tr-TR. Config-строка `0.10` читается invariant; культура UI не меняет коэффициент.
Получается 36 проверок под Framework и 19 под .NET 10.

```text
dotnet build examples/Delivery/Delivery.slnx -c Release
examples/Delivery/CompatibilityProbe/bin/Release/net48/CompatibilityProbe.exe
dotnet examples/Delivery/CompatibilityProbe/bin/Release/net10.0/CompatibilityProbe.dll
```

Эти проверки доказывают маленькую выделенную границу. Они не доказывают переносимость IIS,
SQL-провайдера, старого UI или бинарного потребителя без пересборки. Для библиотеки с внешними
потребителями отдельно нужен тест прежнего собранного consumer, публичных API и serialization contract.

## Порядок безопасного переноса

1. Зафиксируйте рабочую сборку, входы и выходы, зависимости среды и критерии отказа.
2. Найдите небольшое правило, которое можно отделить от UI/config/I/O. Напишите characterization tests.
3. Выделите контракт и совместимый target, подключите библиотеку сначала к старому хосту.
4. Запустите тот же набор на новом runtime; отдельно проверьте культуру, пути, время, encoding и exceptions.
5. Переносите хост по частям, сохраняя наблюдаемость и возможность возврата; сравните production-подобные данные.
6. Удаляйте прежний путь после проверенной приёмки и решения судьбы оставшихся потребителей.

Типичные ошибки: менять формат данных одновременно с runtime; считать compile success полной миграцией;
положиться на binding redirect вместо проверки API; принять установку SDK за установку Framework runtime;
включить netstandard2.1 и ожидать его поддержки старым приложением.

## Контрольные вопросы и практика

1. Почему App.config библиотеки не задаёт настройки всех её потребителей?
2. Как binding redirect может исправить загрузку и одновременно оставить runtime-ошибку вызова?
3. Какие данные нужны для проверки переноса, кроме ответа «новый код собрался»?

**56-A.** Вход: пять сумм из CompatibilityProbe в трёх культурах. Ожидаются 36/19 успешных проверок.
Критерий: объяснены 11.06 и 11.16, различие reference assemblies/runtime и причина netstandard2.0.

**56-B.** Добавьте в старое правило минимальную фиксированную надбавку из App.config, затем перенесите её
как явный аргумент PortableRules. Вход: нулевая сумма, граница надбавки, неверный rate и конфигурация.
Критерий: сначала записан старый договор, ожидаемые значения рассчитаны независимо, оба runtime совпадают.

**56-C.** Возьмите учебный или разрешённый вам legacy-проект и подготовьте проверяемый план миграции
одной библиотеки. Вход: зависимости, конфигурация, один старый consumer и набор реальных форматов.
Критерий: есть матрица совместимости, characterization tests, этапы замены и отката; неопробованные
WCF/IIS/COM/провайдеры честно отмечены. [Подсказки](../appendices/hints.md).
