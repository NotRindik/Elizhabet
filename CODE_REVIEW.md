# Eliza;Beth — разбор кода и проекта

Дата: 2026-11-15. Ветка: `New-Inventory-Changes`.

Что смотрела: ядро проекта (контроллеры, системы, FSM, ввод, сохранения, урон, инвентарь, анимации, пара врагов) — примерно 20 файлов из 340 прочитаны целиком, остальное по структуре и поиском. В игру не играла, про геймплей сужу по коду и набору контента.

Пути ниже — относительно `Assets/Scripts/`.

## Общее впечатление

49k строк своего кода, 21 месяц, 294 коммита. Архитектура продуманная, не «MonoBehaviour на каждый чих».

Сильные стороны:

- **Ввод через `IInputProvider`.** Игрок и AI врагов подают команды через один интерфейс, враги ходят на тех же системах движения, что и игрок.
- **Запись сохранений.** Staging-папка, backup, `Recover`, очередь задач, `SaveGate`. Падение посреди записи не побьёт сейв.
- **Набор движения.** Dash, slide, slide-dash, wall run, ledge climb, hook, pogo, coyote time, jump buffer. Плюс hit stop, impact-эффекты, audio materials — ставка на game feel, для метроидвании это правильно.

## Главная проблема кода

Состояние игрока размазано по флагам, FSM им не владеет.

Цепочка вида `wallRunProcess == null && SlideProcess == null && EdgeStuckProcess == null && !isHooked && ...` повторяется в десятке переходов и почти в каждом обработчике ввода в `Controllers/Entity/PlayerController.cs`. Новая способность = правка всех этих условий.

Куда двигаться: каждое состояние само объявляет, что оно разрешает (прыжок, dash, атаку и т.д.), а обработчики ввода спрашивают текущее состояние, а не шесть компонентов. Ложится на план state machine поверх composer.

Попутно:

- Состояния создаются через `new` на каждое нажатие (`new DashState(this)`, `new SlideState(this)`, в jump buffer — `new JumpState` каждый кадр на земле).
- Текущее состояние сравнивается строкой: `fsmComponent.currentState != nameof(TakeHitState)`. Работает только потому, что `TakeHitState` лежит без namespace; если перенести в `States`, `ToString()` вернёт `States.TakeHitState` и все проверки молча сломаются.

## Баги

- [x] **1. Билд, скорее всего, не соберётся.**
  - `using NUnit.Framework` в runtime-коде: `ContextManager.cs`, `Systems/ItemThrowSystem.cs`, `Systems/SpeedBoosMod.cs`.
  - `using UnityEditor` без `#if UNITY_EDITOR`: `PixelDrawer/PixelCanvasCreator.cs`.
- [x] **2. Penetration вычитается дважды.** `Systems/HealthSystem.cs`: сначала в `CalculateProtection`, потом ещё раз в `ApplyDamage` (и в `Damage`, и в `EnemyDamage`).
- [x] **3. NRE при ударе без `WeaponType`.** `Systems/HealthSystem.cs`, `Damage.ApplyDamage` вызывает `BodyTypeContains` без проверки на null; в `CalculateCrit` проверка есть.
- [x] **4. `OnDie` может сработать несколько раз.** `HealthSystem.TakeHit` не проверяет, что цель уже мертва. Два удара в один кадр — две смерти.
- [x] **5. `ActiveStateChange` передаёт не то значение.** `Systems/BaseComponentSystem.cs`: вместо `value` уходит `WasInitialized`. Подписан `PenguinMod`. Там же: `OnEnable()`/`OnDisable()` систем вызываются при каждом присваивании `IsActive`, даже если значение не изменилось.
- [~] **6. Hook разрешён почти всегда.** `Controllers/Entity/PlayerController.cs:395` — все условия через `&&`, `return` почти никогда не срабатывает. Похоже, имелось в виду `||`.
- [~] **7. Any-переход в `fallUp` мёртвый.** `Controllers/Entity/PlayerController.cs:472` — условие то же, что у `fall`, а `fall` проверяется раньше.

Мелочи:

- [x] `_onInteract` в `PlayerController` нигде не присваивается, подписывается null. Удалён 2026-11-25: взаимодействие работает через `InteractionHandleSystem`.
- [x] `ContextManager.Awake`: `Destroy(Instance)` уничтожает только компонент, не объект. Не баг (2026-11-25): менеджер живёт в сцене, новая сцена должна побеждать, а на `__ Managers` висят другие менеджеры.
- [x] В `InputState` действие `ThrowItem` не добавлено в словарь `actionState`, поэтому не освобождается в `Dispose`.
- [x] 118 `Debug.Log`, включая отладочные вроде `"ASDSAD"` и `Debug.Log(knockDir)` на каждом контактном уроне.

## Что ещё стоит поправить

- **ID предмета в сейве — имя префаба.** Переименование префаба ломает сохранения. Имена вроде `PeniLava`, `BALLS`, `BigFuckingSword` лучше поменять до первых публичных сейвов.
- **Ключи мира по `gameObject.name`** (`WorldKeyBuilder`). Два объекта с одним именем или переименование в сцене — коллизия или потерянный флаг.
- **Сейв зависит от имён классов.** `Dictionary<Type, ISaveSerialize>` и `TypeNameHandling.Auto`: переименование класса компонента ломает старые сейвы. `saveFormatVersion` в манифесте есть, миграций нет.
- **Мёртвый unsafe-код.** `@string`, `Vector<T>`, `Allocator` в `StdLib.cs` и `States/UnsafeFieldExtractor.cs` нигде не используются — можно удалить. (`InputContext` не трогать.)
- **Нет asmdef и namespace.** 250 файлов без namespace, 109 лежат в корне `Scripts`. Отдельные asmdef хотя бы для `PixelDrawer` и `Editor` ускорят компиляцию и поймают утечки `UnityEditor` в runtime.
- **Мусор в репозитории.** `Assets/Scripts.zip`, `Assets/Resources/Sprites/TileSets.zip`, `Assets/_Recovery`, `Assets/TEST`, папка `Resurces` рядом с `Resources`.
- **Сцена `Level_01_Start.unity` весит 38 МБ текстом.** Пока работаешь одна — терпимо, но любой merge по ней будет болью.
- **Тестов нет.** Урон, инвентарь (`StorageGrid`, `SwapOrMoveItems`) и сейвы — чистая логика, её можно покрыть без сцены.

## Про игру в целом

Настораживает соотношение инструментов и контента.

- Инструменты: свой pixel-редактор (~10k строк), редактор анимаций (~2.6k), кастомный рендеринг.
- Контент: один уровень, около семи врагов, около 45 предметов.

Метроидвания живёт количеством комнат, врагов и боссов, одной это тянуть долго.

Совет: заморозить инструменты и собрать vertical slice — одну законченную зону с боссом, которую можно показать на странице Steam и отдать как демо. Она же покажет, каких инструментов реально не хватает.

## С чего начать

1. Баги 1–7: мелкие и независимые.
2. Перевод условий переходов на «состояние объявляет разрешения».
3. Стабильные ID предметов и объектов мира — до того, как появятся сейвы, которые жалко терять.

## Прогресс 2026-11-22

- 1: убраны `using NUnit` из runtime. `using UnityEditor` теперь под `#if UNITY_EDITOR` в `PixelCanvasCreator`, `EventSound`, `ShadowCaster2DTileMap`.
- 2: penetration вычитается один раз, в `CalculateProtection`.
- 3: `BodyTypeContains` проверяет `WeaponType` на null.
- 4: `OnDie` вызывается только при переходе живой → мёртвый (`TakeHit` и `SetHealth`).
- 5: `ActiveStateChange` передаёт `value`. Повторные `OnEnable`/`OnDisable` при одинаковом значении оставлены, иначе может поменяться поведение.
- `ThrowItem` добавлен в `actionState`.
- Убраны `Debug.Log("ASDSAD")` (спамил на каждый доступ к `player`, когда его нет) и `Debug.Log(knockDir)`.
- `TriggerEvent` больше не делает прямой `SaveModule<WorldScope>()` на каждый вход.
- 6, 7: не баги, Рина подтвердила, что условия правильные. Проблема в архитектуре записи условий FSM, займётся позже.

## Прогресс 2026-11-25

- Убраны отладочные логи (`Abiba`, `ComeBack`, `YEAH`, `BOOTSTRAP`, `PLAYER INIT` и т.п.); `Not initialized InputContext` и `Not Legacy Controller` стали `LogWarning`. Информативные логи сохранения, режимов и InputManager оставлены.
- Класс `Button` (`MonoBehScripts/InteractableButton.cs`) переименован в `InteractableButton`. GUID скрипта тот же, ссылки в сценах целы. Убраны обходные алиасы `UIButton` / `using Button = ...`.
- Замечено, не исправлено: `ManaSystem.UseMana` пишет лог и выходит, когда `CanTakeMana` вернул true — похоже на перевёрнутое условие.
- Кодировка: все .cs в UTF-8. Русский текст, испорченный в `�`, восстановлен из старых версий git в ManaSystem, InputManager, EventSound, ParticleDamage, RatController, PlatformMaker, SpeedBoosMod; SceneExitData и SoundEmiter перекодированы из 1251. Не восстановить (нет в истории): комментарий в `StdLib.cs:215` и в `WeaponTrail.cs:12`.
