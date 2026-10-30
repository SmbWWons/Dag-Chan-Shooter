# Структура проекта DagChan

- `Assets/Scripts` — код по разделам: Player, Combat, Enemies, Health, UI, Input, Level. Инструменты редактора — в Editor.
- `Assets/Models` — модели врагов и оружия, отдельные меши уровня.
- `Assets/Textures` — текстуры оружия, рук, медведей, уровня и интерфейса.
- `Assets/Materials` — материалы по тем же разделам.
- `Assets/Animations` — анимации и Animator Controller врагов и оружия.
- `Assets/Audio/DagChan` — Shots (выстрелы) и Ultimate (реплика активации).
- `Assets/Prefabs` — готовые префабы врагов.
- `Assets/Input/Resources/DagChanControls.inputactions` — действия и привязки Input System. Открывается двойным кликом.
- `Assets/Scenes` — обе рабочие сцены, DagChan_Level_01 и SampleScene.
- `Assets/Data/Navigation` — данные NavMesh.
- `Assets/Shaders` — шейдеры врагов, оружия, уровня и меню.
- `Assets/Settings` — настройки рендера и постобработки.
- `Assets/ThirdParty` — сторонние ресурсы TextMesh Pro с сохранением их внутренней структуры.

Проверка связей: Tools → DagChan → Check Project Links.

При переносе ресурсов в Unity сохраняйте их `.meta` файлы: они содержат идентификаторы ссылок сцен, материалов и анимаций.
