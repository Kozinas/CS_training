# 38. Git, сборка и NuGet

**Результат:** вы готовите проверяемое изменение, воспроизводите сборку и проверяете библиотеку
как отдельный пакет. Нужны главы 19, 22, 29 и 36. Рабочая папка примеров —
[Engineering](../../examples/Engineering/README.md).

## Изменение проходит через diff

Working tree — файлы на диске; index — выбранное содержимое следующего коммита;
commit — снимок с родителем и метаданными; branch — подвижная ссылка на коммит.
Обычный git diff показывает изменения между working tree и index, git diff --cached —
между index и HEAD. Поэтому после git add дальнейшее редактирование файла не попадёт в коммит,
пока вы снова не добавите нужное изменение.

Пример действий в собственной рабочей копии; имя ветки и пути выбираются под вашу задачу:

```powershell
git status --short
git switch -c fix/review-deadline
git diff -- examples/Engineering/ReviewRules/ApprovalPolicy.cs
git add -- examples/Engineering/ReviewRules/ApprovalPolicy.cs
git diff --cached
git diff --cached --check
git commit -m "Fix review expiry at the exact deadline"
```

В реальном исправлении добавьте также тест и документацию. Не включайте в коммит чужие изменения,
bin/obj, токены, локальные базы и Books. Маленький осмысленный diff проще проверить и отменить.
Сообщение коммита объясняет результат; PR связывает его с задачей, тестами и ограничениями.
Перед push просмотрите remote и имя текущей ветки; push переносит коммиты на сервер.

## Merge и конфликт смысла

Git объединяет изменения относительно общего предка. Когда две стороны меняют одну строку,
он может оставить конфликт. Маркеры показывают варианты, но не выбирают правильное бизнес-правило.
Файл с удалёнными маркерами ещё может быть семантически неверен: после разрешения нужны тесты.

`python tools/practice_git.py --keep` создаёт отдельный учебный репозиторий в .artifacts,
делает две ветки с лимитами 500 и 2000, воспроизводит конфликт и сохраняет согласованное в упражнении
значение 1500. Затем проверяет merge-коммит с двумя родителями. У репозитория нет remote.
Без --keep временная папка удаляется после проверки; основной репозиторий не переключается.

В оставленной папке изучите `git log --graph --oneline --all` и `git show --cc HEAD`.
Для ручного упражнения повторите две ветки от первого коммита с другими значениями.
При незавершённом merge сначала изучите status, исправьте файлы, добавьте их в index и завершите commit.
Если нужно отказаться от начатого merge, существует git merge --abort; начинайте упражнение
с чистой рабочей копии, чтобы не смешивать его с незакоммиченной работой.
[Git merge](https://git-scm.com/docs/git-merge).

Rebase переносит коммиты на другую основу и меняет их идентификаторы. Для уже общей истории
это требует координации. Revert создаёт новый коммит, отменяющий изменение, и сохраняет историю;
reset передвигает ссылку, а его режимы по-разному затрагивают index и файлы.
Не используйте reset --hard как способ «починить непонятный status».

## Что собирает MSBuild

csproj задаёт свойства, элементы и targets для MSBuild. SDK-style проект получает много правил
из Microsoft.NET.Sdk. TargetFramework определяет доступный контракт платформы,
LangVersion — синтаксис компилятора, Nullable — анализ null, OutputType — библиотеку или программу.
Сборка net10.0 не становится совместимой с net8.0 от смены LangVersion.

Directory.Build.props добавляет общие свойства при раннем импорте, Directory.Build.targets —
поздние правила. MSBuild ищет ближайший Directory.Build.props; родительский файл не соединяется
с ним автоматически. Поэтому Engineering явно импортирует ../Directory.Build.props,
а затем задаёт собственные настройки. solution группирует проекты и конфигурации;
ProjectReference задаёт реальную зависимость и порядок сборки.
[Настройка MSBuild по каталогу](https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-by-directory).

```xml
<!-- Фрагмент отдельной библиотеки; в курсе часть свойств общая. -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

global.json выбирает SDK, а не runtime приложения. В Engineering закреплён 10.0.402,
latestPatch допускает обслуживающее обновление внутри feature band; allowPrerelease=false
исключает предварительные SDK. Там же выбран Microsoft.Testing.Platform.
Проверяйте `dotnet --version` из папки примера: рабочий каталог влияет на поиск global.json.
Корень examples допускает latestFeature для других задач курса; это отдельная политика.
[Выбор SDK](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json).

## Пакеты и воспроизводимость

PackageReference подключает NuGet-пакет, ProjectReference — исходный проект.
Direct dependency указана вами, transitive dependency пришла через другую библиотеку.
Нужна проверка всего разрешённого графа: совместимая прямая версия ещё не гарантирует
совместимость или отсутствие известной уязвимости у транзитивной зависимости.

Central Package Management переносит версии в ближайший Directory.Packages.props:
ManagePackageVersionsCentrally=true и PackageVersion с Include/Version.
В csproj остаётся PackageReference без Version. Это единая точка настройки,
но не автоматическое обновление и не проверка качества пакета.
[CPM](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management).

packages.lock.json фиксирует разрешённый граф и хеши содержимого. Он хранится в Git вместе
с исходниками. `restore --locked-mode` проверяет соответствие зависимостей lock-файлу;
изменение PackageReference без обновления lock должно остановить сборку.
Для намеренного обновления измените версию, выполните обычный restore, проверьте diff всех
затронутых lock-файлов и тесты, затем вернитесь к locked-mode в CI.
Lock не закрепляет SDK, внешнюю БД, состояние сети или исходный код проекта-потребителя.
[PackageReference и lock](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files).

```powershell
Push-Location examples/Engineering
try {
    dotnet restore Engineering.slnx --locked-mode
    dotnet build Engineering.slnx -c Release --no-restore
    dotnet test --project ReviewRules.Tests -c Release --no-build
    $auditArgs = @("package", "list", "--project", "Engineering.slnx",
        "--vulnerable", "--include-transitive", "--no-restore")
    dotnet @auditArgs
} finally { Pop-Location }
```

В новом порядке `dotnet package list` проект передаётся через --project.
Если источник аудита недоступен, результат не означает «уязвимостей нет».
На дату проверки выпуска аудит Engineering и отдельного Container.Tests не сообщил известных
уязвимостей; это снимок базы предупреждений, а не доказательство безопасности.
[Аудит NuGet](https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages).

## Анализаторы и форматирование

Компилятор проверяет язык и типы, nullable-анализ предупреждает о возможном null,
анализаторы ищут дополнительные ошибки и нарушения соглашений. .editorconfig задаёт формат
и severities диагностик. Например, IDE0161 предлагает file-scoped namespace;
предупреждения в Engineering считаются ошибками, поэтому изменения правил надо проверять в сборке.

`dotnet format whitespace` проверяет пробелы и отступы, `style` — правила стиля,
`analyzers` — доступные исправления анализаторов. --verify-no-changes превращает несоответствие
в ошибку без переписывания файлов; --include ограничивает область.
Проверочный скрипт ограничивает проверку форматирования новыми проектами решения.
Автоформатирование не подтверждает правильность алгоритма.
[dotnet format](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format).

Не подавляйте предупреждения сразу для всего проекта. В тесте старого метода подавление CRV001
ограничено конкретным вызовом; оно документирует намеренную проверку совместимости.
Новые вызовы устаревшего API по-прежнему получают диагностику.

## Pack, потребитель и версия API

`dotnet pack` создаёт nupkg с metadata и сборками. Для ReviewRules заданы PackageId,
Version=1.1.0 и README. --no-build использует ранее собранную конфигурацию:
сначала выполните Release build, затем pack тоже в Release.
[dotnet pack](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-pack).

```powershell
# Из examples/Engineering после Release build.
dotnet pack ReviewRules -c Release --no-build -o ../../.artifacts/feed
```

Успешный pack ещё не проверяет потребителя. `python tools/verify_engineering.py` создаёт
временный проект из [шаблона](../../examples/Engineering/Packaging/Consumer.csproj.template),
настраивает единственным источником локальный feed и выделяет пустой NuGet-кэш.
В проекте нет ProjectReference; он восстанавливает именно пакет и запускает публичные сценарии.
Проверяется также ошибка NU1004 при изменении требуемой версии без обновления lock.

В NuGet `Version="1.1.0"` задаёт минимальную версию в правилах разрешения;
`Version="[1.1.0]"` — точное требование. Шаблон потребителя использует второй вариант.
SemVer связывает MAJOR с несовместимым изменением публичного договора,
MINOR с совместимым добавлением, PATCH с совместимым исправлением.
Исправление дефекта всё равно может менять поведение зависимых программ — опишите границу
и руководство по миграции. [SemVer](https://semver.org/).

Версия 1.1.0 в курсе иллюстрирует новый Evaluate с объяснимым исходом и сохранённый bool-wrapper.
Это учебная модель миграции; опубликованного в NuGet выпуска 1.0.0 здесь нет.
Obsolete сохраняет метод, но новое предупреждение ломает сборку потребителя с warnings-as-errors:
совместимость сигнатуры, поведения и сборки надо оценивать отдельно.
Удаление метода или изменение числового смысла сохранённого enum потребует отдельного решения.

## CI делает проверку повторяемой

[Workflow](../../.github/workflows/verify.yml) запускает проверку на Windows при push в master,
pull request и вручную. Он устанавливает SDK/Python, выполняет полный tools/verify.py,
проверяет книгу, строит общий PDF и сохраняет артефакты. Actions закреплены по commit SHA;
обновление ссылки — самостоятельное проверяемое изменение.
У job только contents:read; публикация пакета не настроена.
[Сборка .NET в GitHub Actions](https://docs.github.com/en/actions/tutorials/build-and-test-code/net).

Container.Tests компилируется, но не запускается в этом Windows job без Linux Docker engine.
CI полезен только с понятным набором проверок: зелёный job не означает проверку необъявленной среды.
Для команд, объединённых в одном PowerShell step, проверяется LASTEXITCODE, чтобы последующая
успешная команда не скрыла предыдущую ошибку. Общий Markdown сравнивается с сохранённым в Git;
PDF проверяется по структуре и ссылкам, его байты могут отличаться из-за доступных шрифтов.

## Контрольные вопросы

1. Чем различаются diff, diff --cached и содержимое последующего commit?
2. Какие части среды не закрепляет packages.lock.json?
3. Почему потребитель nupkg без ProjectReference проверяет больше, чем успешный pack?

## Практика

**38-A.** Выполните Git-упражнение с --keep и объясните оба родителя merge-коммита.
В своей новой ветке добавьте тест с именем поведения. Критерий: staged diff содержит только
нужные файлы, а разрешение конфликта подтверждено ожидаемым значением, не только отсутствием маркеров.

**38-B.** В копии Engineering измените центральную версию одного тестового пакета.
Сначала зафиксируйте отказ locked restore, затем осознанно обновите lock и пройдите тесты.
Критерий: объяснён diff прямых и транзитивных зависимостей; исходный lock курса не изменён упражнением.

**38-C.** Соберите пакет с новым совместимым методом и независимого потребителя из пустого кэша.
Добавьте CI для обоих тестовых проектов и книги. Критерий: нет ProjectReference к библиотеке,
неверная версия пакета останавливает проверку, артефакт содержит README и публичные методы,
а статус CI связан с конкретным коммитом и перечисленным набором тестов.

[Назад: 37](37-design.md) · [Далее: 39](39-maintenance.md) · [Подсказки](../appendices/hints.md)
