## Инструкции для AI-кодинга — I Am Your Translator

Короткое, конкретное руководство, чтобы агент был продуктивен с минимальным вводом.

0) Внимание. Режим "Обдумывания" должен быть по возможности включен, чтобы обеспечить высокое качество ответов. Так же он по возможности должен использовать внешнюю документацию и автогенерацию кода (пункт 8). Так же он всегда должен отвечать на русском языке.

1) Общая картина
- Назначение: плагин BepInEx, переводящий UI/аудио и заменяющий шрифты/текстуры для Unity-игры "I Am Your Beast".
- Среда: .NET Framework 4.7.2 сборка, загружается игрой через BepInEx. GUID плагина: `com.lenarikil.iamyourbeast.translator`.

2) Основные компоненты и поток данных
- `src/Plugin.cs` — точка входа: вызывает `LanguageManager.LoadLanguage()` и устанавливает `Plugin.GlobalTMPFont`; подписывается на события загрузки сцен.
- `src/Core.cs`, `src/StartScreenHandler.cs` — маршрутизация по сценам и лёгкие обработчики сцен.
- `src/json/LanguageManager.cs` + `src/json/JsonFormat.cs` — единый источник переводов: три словаря (`settings`, `timings`, translations). Файлы хранятся в `BepInEx/config/IAmYourTranslator/languages/`.
- `src/CommonFunctions.cs` — общие хелперы: `TMPFontReplacer.ReplaceFont()`, `UITextureReplacer.ApplyTo()`, и утилиты поиска GameObject.
- `src/Harmony Patches/*.cs` — основная логика подмены выполняется через Harmony-патчи (prefix/postfix), которые перехватывают методы игры и заменяют текст/ресурсы.

3) Проектные соглашения (обязательно соблюдать)
- Патчи: следуйте уже существующему паттерну в `src/Harmony Patches/` — используйте `[HarmonyPatch(typeof(...))]` с `[HarmonyPrefix]`/`[HarmonyPostfix]`. Для доступа к экземпляру используйте `__instance`, для изменения параметров — `ref`/`out`.
- Пути: используйте BepInEx-пути (`Paths.ConfigPath`) и поддиректории `fonts/`, `languages/`, `textures/`.
- Декомпиляция в первую очередь: типы/методы целевой игры берутся из декомпилированного `Assembly-CSharp.dll` — подписи должны совпадать точно.
- Сборка: не используйте NuGet для игровых сборок — `.csproj` ссылается на локальные DLL через `HintPath`.

4) Сборка / деплой / отладка (точные команды)
```powershell
dotnet build --configuration Debug   # быстрая итерация
dotnet build --configuration Release # релизная сборка
```
Деплой: скопировать `bin/Debug/net472/IAmYourTranslator.dll` или `bin/Release/net472/IAmYourTranslator.dll` в `<GameDir>/BepInEx/plugins/`.
Отладка: подключиться через Visual Studio к процессу игры; ставьте брейкпоинты и в исходнике, и в декомпилированных типах (`dnSpy`).

5) Примеры-паттерны (копировать/использовать)
- Глобальная замена шрифта:
```csharp
CommonFunctions.TMPFontReplacer.ReplaceFont(Plugin.GlobalTMPFont);
```
- Заменить текстуру UI:
```csharp
UITextureReplacer.ApplyTo(targetGameObject);
```
- Получение перевода в патче:
```csharp
var t = LanguageManager.GetTranslations(key);
// при отсутствии запись автоматически добавляется и сохраняется через SaveCurrentLanguage()
```

6) Шаблоны Harmony (prefix/postfix) — вставьте в `src/Harmony Patches/`
```csharp
[HarmonyPatch(typeof(TargetType), "MethodName")]
class TargetType_MethodName_Patch
{
  [HarmonyPrefix]
  static bool Prefix(TargetType __instance, ref string text)
  {
    // пример: подмена текста до выполнения оригинального метода
    text = LanguageManager.GetTranslations("some.key") ?? text;
    return true; // вернуть false чтобы пропустить оригинал
  }

  [HarmonyPostfix]
  static void Postfix(TargetType __instance, ref string text)
  {
    // пример: корректировка результата после оригинала
  }
}
```
Ключевые советы: всегда проверяйте сигнатуры через декомпилятор (имена, типы параметров, static/instance).

7) Зависимости и интеграция
- В `.csproj` используются ссылки через `HintPath` на: `Assembly-CSharp.dll`, `0Harmony.dll`, `BepInEx.dll`, `TextMeshPro`, `Newtonsoft.Json`, `AudioTextSynchronizer.dll`. При изменениях, затрагивающих игровые типы, используйте те же ссылки.

8) Внешняя документация и автогенерация кода
- Всегда используйте Context7 MCP, когда требуется документация по библиотеке/API, генерация кода, инструкции по настройке или конфигурации, без отдельного запроса.

9) Куда смотреть в первую очередь
- `src/Plugin.cs` — подтверди стартовую логику и глобальный шрифт.
- `src/json/LanguageManager.cs` — формат JSON и поведение сохранения/добавления ключей.
- `src/CommonFunctions.cs` — примеры замены шрифтов/текстур и утилиты поиска объектов.
- `src/Harmony Patches/*` — реальные примеры патчей и шаблоны для копирования.
