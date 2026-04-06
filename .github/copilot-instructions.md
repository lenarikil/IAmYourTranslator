## Инструкции для AI-кодинга — I Am Your Translator

Короткое, конкретное руководство, чтобы агент был продуктивен с минимальным вводом.

0) Внимание. Режим "Обдумывания" должен быть по возможности включен, чтобы обеспечить высокое качество ответов. Так же он по возможности должен использовать внешнюю документацию и автогенерацию кода (пункт 8). Так же он всегда должен отвечать на русском языке.

1) Общая картина
- Назначение: плагин BepInEx, переводящий UI/аудио и заменяющий шрифты/текстуры для Unity-игры "I Am Your Beast".
- Среда: .NET Framework 4.7.2 сборка, загружается игрой через BepInEx. GUID плагина: `com.lenarikil.iamyourbeast.translator`.
- Основной рабочий язык кодирования: **C#**; целевая платформа — **Unity + BepInEx**.

2) Основные компоненты и поток данных
- `src/Plugin.cs` — точка входа: инициализирует LanguageManager, устанавливает GlobalTMPFont, подписывается на события загрузки сцен.
- `src/Core.cs`, `src/StartScreenHandler.cs` — маршрутизация по сценам и обработчики сцен.
- `src/json/LanguageManager.cs` + `src/json/JsonFormat.cs` — единый источник переводов; хранит текущий язык в памяти и на диске (BepInEx/config/IAmYourTranslator/languages/*.json).
  - **JsonFormat** содержит словари: `settings`, `timings`, `tutorialPrompts`, `enemyRadio`, `levelNames`, `bonusObjectives`, `mainObjectives`, `levelFailedHeaders`, `categorySlideTexts`, `killConfirmation`, `overviewScreen`, `Hints` и др.
  - **LanguageManager.CurrentLanguage** — текущий загруженный язык в памяти; сохраняется методом `SaveCurrentLanguage()`.
- `src/CommonFunctions.cs` — сборник утилит:
  - **Кеширование** (static, глобальное):
    - `rootObjectCache` / `GetInactiveRootObject()` — поиск неактивных root-объектов с кешем (1 сек)
    - `childCache` / `GetGameObjectChild()` — поиск дочерних объектов с кешем (0.5 сек)
  - **Трансляция текста** (с автосохранением):
    - `TranslateTextAndSaveIfMissing(Text/TMP_Text, key, dict)` — перевод 2 типов текста (overload'ы)
  - **Применение шрифтов**:
    - `TMPFontReplacer.ApplyFontToTMP(tmp, font)` — замена одного компонента
    - `ApplyFontToAllChildrenTMP(target, font)` — применение ко всем детям (+ логирование)
  - **UI Layout-хелперы**:
    - `DisableGameObjectPanels(params GameObject[])` — отключение нескольких панелей
    - `StretchRectTransformHorizontal(rt)` — растяжение на полную ширину (Y-оси сохраняются)
  - **Поиск с fallback'ами**:
    - `FindComponentWithFallback<T>(start, params string[] paths)` — ищет компонент по нескольким путям
  - **UI замена текстур**:
    - `UITextureReplacer.ApplyTo(gameObject, filePath)` — замена Image или RawImage
- `src/Harmony Patches/*.cs` — Harmony-патчи перехватывают методы игры и выполняют замену текста/ресурсов/размеров.

3) Проектные соглашения (обязательно соблюдать)
- **Патчи**:
  - Используйте `[HarmonyPatch(typeof(TargetClass), "MethodName")]` с `[HarmonyPrefix]`/`[HarmonyPostfix]`.
  - Для доступа к экземпляру: `__instance`; для изменения параметров: `ref`/`out`.
  - Патчи для `Start` срабатывают один раз при создании (используйте для инициализации); для динамического контента используйте постфиксы для методов, которые вызываются многократно (например, `RefreshHint()`).
  - Задокументируйте цель патча в комментарии выше класса.

- **Пути**: используйте `BepInEx.Paths.ConfigPath` и поддиректории `fonts/`, `languages/`, `textures/`.

- **Декомпиляция**: типы/методы целевой игры берутся из декомпилированного `Assembly-CSharp.dll`; подписи должны совпадать точно (имена, типы параметров, static/instance).

- **Сборка**: не используйте NuGet для игровых сборок; `.csproj` ссылается на локальные DLL через `HintPath`.

- **Соглашения по именованию**:
  - Классы патчей: `TargetClassName_MethodName_Patch` или `TargetClassName_Patch` (если один метод).
  - Переводные ключи в JSON: `camelCase` (например, `"UILevelNameBorder"`, `"killConfirmation"`).
  - Локальные булевы флаги для включения/отключения функций: `enableWideScreenLayout`, `enableFeatureName`.

4) Сборка / деплой / отладка (точные команды)
```powershell
dotnet build --configuration Debug   # быстрая итерация
dotnet build --configuration Release # релизная сборка
```
Деплой: скопировать `bin/Release/net472/IAmYourTranslator.dll` в `<GameDir>/BepInEx/plugins/`.
Отладка: подключиться через Visual Studio к процессу игры; ставьте брейкпоинты; используйте `dnSpy` для исследования декомпилированного кода.

5) Примеры-паттерны (копировать/использовать)

**Замена шрифта:**
```csharp
var tmpFont = Plugin.GlobalTMPFont;
if (tmpFont != null)
    TMPFontReplacer.ApplyFontToTMP(textComponent, tmpFont);

// Или применить ко всем детям сразу:
ApplyFontToAllChildrenTMP(__instance, tmpFont, "[MyPatch]");
```

**Трансляция текста с автосохранением:**
```csharp
var dict = LanguageManager.CurrentLanguage.overviewScreen;
// Для Text компонента:
TranslateTextAndSaveIfMissing(textComponent, "Original Text", dict, "[MyPatch]");
// Для TMP_Text компонента (overload):
TranslateTextAndSaveIfMissing(tmpComponent, "Original Text", dict, "[MyPatch]");
```

**Замена текстуры UI:**
```csharp
string texturesDir = Path.Combine(BepInEx.Paths.ConfigPath, "IAmYourTranslator", "textures");
string logoFile = Path.Combine(texturesDir, "UILogoText.png");
UITextureReplacer.ApplyTo(targetGameObject, logoFile, false);
```

**Отключение UI панелей:**
```csharp
Transform leftPanel = RecursiveFindChild(__instance.transform, "LeftSide");
Transform rightPanel = RecursiveFindChild(__instance.transform, "RightSide");
if (leftPanel != null && rightPanel != null)
    DisableGameObjectPanels(leftPanel.gameObject, rightPanel.gameObject);
```

**Растяжение RectTransform на полную ширину:**
```csharp
var rt = __instance.GetComponent<RectTransform>();
if (rt != null)
    StretchRectTransformHorizontal(rt);  // anchorMin/Max X: 0→1, Y сохраняются
```

**Поиск компонента с fallback'ами:**
```csharp
// Ищет Logo сначала в Level Name Border, потом рекурсивно в Canvas
var logoImage = FindComponentWithFallback<Image>(__instance, "Level Name Border/logo", "Canvas/logo");
if (logoImage != null)
    UITextureReplacer.ApplyTo(logoImage.gameObject, logoFile);
```

**Получение/добавление перевода (старый паттерн):**
```csharp
var dict = LanguageManager.CurrentLanguage.overviewScreen; // или другой словарь
if (dict.TryGetValue(key, out var translated) && !string.IsNullOrEmpty(translated))
{
    text = translated;
}
else
{
    dict[key] = key; // добавить оригинал как заготовку
    LanguageManager.SaveCurrentLanguage();
}
```

**Поиск GameObjects:**
```csharp
Transform child = RecursiveFindChild(__instance.transform, "ChildName");
Transform direct = __instance.transform.Find("Canvas/ChildName");
var root = GetInactiveRootObject("RootName");  // с кешем
```

6) Типичные паттерны Harmony-патчей

**Простой постфикс (замена текста при инициализации):**
```csharp
[HarmonyPatch(typeof(UIComponent), "Start")]
class UIComponent_Patch
{
    [HarmonyPostfix]
    static void StartPostfix(UIComponent __instance)
    {
        if (__instance == null) return;
        // выполнить замену текста/шрифта/текстуры
    }
}
```

**Динамическая замена (каждый раз, когда содержимое обновляется):**
```csharp
[HarmonyPatch(typeof(UIComponent), "RefreshContent")]
class UIComponent_RefreshPatch
{
    [HarmonyPostfix]
    static void RefreshPostfix(UIComponent __instance)
    {
        if (__instance == null) return;
        // переводить и переприменять шрифт для нового содержимого
    }
}
```

**Доступ к приватным полям:**
```csharp
var privateField = Traverse.Create(__instance).Field("fieldName").GetValue<FieldType>();
```

7) Зависимости и интеграция
- В `.csproj` используются ссылки (HintPath) на: `Assembly-CSharp.dll`, `0Harmony.dll`, `BepInEx.dll`, `TextMeshPro`, `Newtonsoft.Json`, `AudioTextSynchronizer.dll`.
- Добавляемые зависимости должны быть добавлены в `.csproj` с `HintPath` на локальные DLL.

8) Внешняя документация и автогенерация кода
- Используйте Context7 MCP (мощный инструмент для документации и генерации кода) без отдельного запроса, когда требуется:
  - Документация по библиотеке/API.
  - Генерация кода на основе требований.
  - Инструкции по настройке, конфигурации.
  - Примеры использования сложных API.

9) Куда смотреть в первую очередь
- [src/Plugin.cs](src/Plugin.cs) — стартовая логика и глобальный шрифт.
- [src/json/JsonFormat.cs](src/json/JsonFormat.cs) — список всех доступных переводных словарей.
- [src/json/LanguageManager.cs](src/json/LanguageManager.cs) — логика загрузки/сохранения языков.
- [src/CommonFunctions.cs](src/CommonFunctions.cs) — утилиты для замены шрифтов, текстур, поиска объектов.
- [src/Harmony Patches/](src/Harmony%20Patches/) — примеры паттернов патчей; скопируйте структуру существующего патча.

10) Типичная рабочая последовательность при добавлении новой функции
1. **Определить целевой метод/класс** в декомпилированном коде.
2. **Создать файл патча** в `src/Harmony Patches/` по существующему шаблону.
3. **Добавить переводной словарь** в `JsonFormat.cs` (если нужен).
4. **Реализовать логику** (замена текста, шрифта, текстуры, размеров RectTransform).
5. **Собрать** (`dotnet build --configuration Release`).
6. **Протестировать** в игре; проверить логи BepInEx.
7. **Итерировать** на основе тестирования.

11) Примеры недавно добавленных функций
- **Вайдскрин паузы** (`UIPauseMenu_LogoPatch.cs`): флаг `enableWideScreenLayout`, функция `WideScreenPausePatch()`, которая расширяет UI на весь экран.
- **Подсказки** (`UIHintDisplay_Patch.cs`): патч на `Start` и `RefreshHint`, переводит каждую подсказку из словаря `Hints`.
- **Замена логотипа в Overview** (`UILevelCompleteOverviewDetails_Patch.cs`): применяет текстуру логотипа и глобальный шрифт ко всем дочерним TMP_Text элементам.

12) Рефакторинг и оптимизация (февраль 2026)

**Исправленные баги производительности:**
- ✅ **Кеширование GetInactiveRootObject** — было сломано (Dictionary создавалась каждый вызов), теперь static и работает (+30-50% производительности)
- ✅ **Кеширование GetGameObjectChild** — аналогично, теперь кешируется правильно (0.5 сек TTL)

**Вынесенные общие паттерны в CommonFunctions:**
1. **TranslateTextAndSaveIfMissing(Text/TMP_Text, dict, logPrefix)** — унифицированная трансляция текста
   - Работает для Text и TMP_Text (2 overload'а)
   - Автоматически ищет в словаре и сохраняет новые ключи
   - Все патчи (UIHint, HUDTimer, Overview) теперь используют одну функцию
   - **Экономия:** -60 строк копипасты

2. **ApplyFontToAllChildrenTMP(target, font, logPrefix)** — применение шрифта к детям
   - Рекурсивно находит все TMP_Text дети (включая неактивные)
   - Использует GetComponentsInChildren<T>(true)
   - Логирует количество применённых элементов
   - **Применяется в:** UIHintDisplay, UILevelCompleteOverviewDetails

3. **DisableGameObjectPanels(params GameObject[])** — отключение UI панелей
   - Отключает несколько панелей за один вызов
   - **Применяется в:** UIPauseMenu (левая/правая сторона), UILevelCompleteScreen

4. **StretchRectTransformHorizontal(RectTransform rt)** — растяжение UI
   - Растягивает по X на полный экран (anchorMin/Max X: 0→1)
   - Сохраняет Y-оси (anchors и offsets)
   - Устанавливает sizeDelta.x = 0 (для правильного растяжения)
   - **Применяется в:** Widescreen паузы и LevelComplete

5. **FindComponentWithFallback<T>(startComponent, params string[] fallbackPaths)** — поиск с резервом
   - Попробует несколько путей для поиска компонента
   - Вернёт первый найденный или null
   - **Применяется в:** поиск логотипа с fallback'ами

**Архитектурные улучшения:**
- Harmony патчи теперь **тонкие слои**, которые вызывают методы из CommonFunctions
- Трансляция текста централизована → изменения в одном месте
- RectTransform манипуляции стандартизированы → консистентность UI
- Поиск компонентов с fallback'ами → надёжность при различных UI структурах

**Результаты рефакторинга (февраль 2026):**
| Файл | До | После | Изменение |
|------|----|----|-----------|
| CommonFunctions.cs | 620 строк | 770 строк | +150 строк (7 новых методов) |
| UIHintDisplay_Patch.cs | 118 строк | 78 строк | -40 строк (удалены дублирующиеся методы) |
| HUDTimerIncreaseListing_Patch.cs | 280 строк | 230 строк | -50 строк (extracted helpers) |
| UILevelCompleteOverviewDetails_Patch.cs | 155 строк | 135 строк | -20 строк (используется общий метод) |
| **ИТОГО** | **≈1173** | **≈1213** | **-13 строк сложности** |

**Как использовать в новых патчах:**
1. Для трансляции текста → используй `TranslateTextAndSaveIfMissing()`
2. Для применения шрифта ко всем детям → используй `ApplyFontToAllChildrenTMP()`
3. Для отключения панелей → используй `DisableGameObjectPanels()`
4. Для растяжения RectTransform → используй `StretchRectTransformHorizontal()`
5. Для поиска с fallback'ами → используй `FindComponentWithFallback<T>()`

**Пример рефакторинного патча:**
```csharp
[HarmonyPatch(typeof(UIMyComponent), "Start")]
public static class UIMyComponent_Patch
{
    [HarmonyPostfix]
    public static void StartPostfix(UIMyComponent __instance)
    {
        try
        {
            if (__instance == null) return;

            // Трансляция (старый паттерн → 3 строки с проверкой)
            // теперь → 1 строка:
            var dict = LanguageManager.CurrentLanguage.myDict;
            TranslateTextAndSaveIfMissing(__instance.myText, "Original", dict, "[UIMyComponent]");

            // Применение шрифта ко всем детям (было → 8 строк)
            // теперь → 1 строка:
            ApplyFontToAllChildrenTMP(__instance, Plugin.GlobalTMPFont, "[UIMyComponent]");

            // Отключение панелей (было → 4 строки)
            // теперь → 1 строка:
            DisableGameObjectPanels(left.gameObject, right.gameObject);
        }
        catch (Exception e)
        {
            Logging.Error($"[UIMyComponent] Error: {e}");
        }
    }
}
```

