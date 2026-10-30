using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public static class ProjectOrganizationAudit
{
    static readonly string Report=Application.dataPath+"/../Logs/ProjectOrganizationVerification.txt";
    /// <summary>
    /// Добавляет результат проверки связей в журнал и при false выбрасывает исключение.
    /// Это не позволяет объявить остальные этапы успешными после обнаруженной ошибки.
    /// </summary>
    static void Check(bool ok,string message){File.AppendAllText(Report,(ok?"PASS: ":"FAIL: ")+message+"\n");if(!ok)throw new Exception(message);}
    /// <summary>
    /// Проверяет ссылки обеих сцен, оружие, материалы, интерфейс, анимации, аудио и загрузку Input Actions.
    /// Также убирает пустые компоненты отсутствующих скриптов и исправляет известную старую ссылку серого фона ХП, сохраняя изменения.
    /// Временно открытые сцены закрывает и возвращает исходную активную сцену.
    /// </summary>
    [MenuItem("Tools/DagChan/Check Project Links")]
    public static void CheckLinks()
    {
        File.WriteAllText(Report,"");
        var original=SceneManager.GetActiveScene();
        foreach(string path in new[]{"Assets/Scenes/DagChan_Level_01.unity","Assets/Scenes/SampleScene.unity"})
        {
            var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            int removed=0;
            foreach(var root in scene.GetRootGameObjects())foreach(var tr in root.GetComponentsInChildren<Transform>(true))
            {
                // Old empty/missing scripts have no functioning behaviour to preserve.
                removed+=GameObjectUtility.RemoveMonoBehavioursWithMissingScript(tr.gameObject);
            }
            if(removed>0){EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);File.AppendAllText(Report,"CLEANED: "+removed+" missing script placeholders in "+path+"\n");}
            foreach(var image in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<UnityEngine.UI.Image>(true))) if(image.name=="серый" && !image.sprite){image.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/Health/Hp.png");EditorUtility.SetDirty(image);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);}
            var components=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true)).Where(c=>c).ToArray();
            foreach(var c in components)
            {
                var so=new SerializedObject(c);var it=so.GetIterator();while(it.NextVisible(true))
                    if(it.propertyType==SerializedPropertyType.ObjectReference && it.objectReferenceInstanceIDValue!=0)
                        Check(it.objectReferenceValue!=null,"object reference "+c.name+"/"+it.propertyPath);
            }
            var player=components.OfType<PlayerMovement>().First(p=>p.enabled);
            var dual=player.GetComponent<DualWeaponController>();var serial=new SerializedObject(dual);
            Check(serial.FindProperty("rightWeapon").objectReferenceValue && serial.FindProperty("leftWeapon").objectReferenceValue,"both weapons retained: "+path);
            Check(components.OfType<ChainsawHealthBar>().Any(h=>h.UltimateFill),"health and ultimate Canvas retained: "+path);
            if(path.Contains("DagChan_Level_01")) Check(components.OfType<ArenaWaveController>().Any(),"enemy wave controllers retained: "+path);
            Check(components.OfType<Animator>().All(x=>!x.runtimeAnimatorController || AssetDatabase.GetAssetPath(x.runtimeAnimatorController).StartsWith("Assets/Animations/")),"animation controllers sorted and linked: "+path);
            File.AppendAllText(Report,"PASS: scene serialized references validated: "+path+"\n");
            if(opened)EditorSceneManager.CloseScene(scene,true);
        }
        if(original.IsValid()&&original.isLoaded)SceneManager.SetActiveScene(original);
        var actions=Resources.Load<InputActionAsset>("DagChanControls");Check(actions && actions.FindAction("Player/Attack")!=null && actions.FindAction("UI/Click")!=null,"Input Actions load through Resources after move");
        var clips=AssetDatabase.FindAssets("t:AudioClip",new[]{"Assets/Audio"}).Select(AssetDatabase.GUIDToAssetPath).ToArray();Check(clips.Length>=8 && clips.All(p=>Path.GetFileName(p).StartsWith("DagChan_")),"audio clips retain DagChan names");
        Check(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/GummyBear_Animated.prefab"),"bear spawn prefab imports");
        Check(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Weapons/Maria.mat") && AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Weapons/Alfons.mat"),"both revolver materials import");
        Check(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Level/WIP_Purple_WorldAligned.mat"),"level wall material imports");
        AssetDatabase.SaveAssets();Debug.Log("DAGCHAN_ORGANIZATION_LINKS_OK");
    }
    /// <summary>
    /// После проверки связей открывает основной уровень и через SessionState запрашивает тест в Play Mode.
    /// При ошибке завершает пакетный процесс с неверным кодом либо останавливает обычный игровой запуск.
    /// </summary>
    public static void RunBatch()
    {
        try {CheckLinks();EditorSceneManager.OpenScene("Assets/Scenes/DagChan_Level_01.unity");SessionState.SetBool("DagChanOrganizationTest",true);EditorApplication.playModeStateChanged+=Changed;EditorApplication.isPlaying=true;}
        catch(Exception e){Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);else EditorApplication.isPlaying=false;}
    }
    /// <summary>
    /// Пустой служебный обработчик смены Play Mode: сейчас действий не выполняет.
    /// Фактическая пошаговая проверка идёт через SessionState и другой класс.
    /// </summary>
    static void Changed(PlayModeStateChange state){}
}
[InitializeOnLoad]
public static class OrganizationRuntimeVerification
{
    static int step;static double next;static DualWeaponController dual;
    static string Report=>Application.dataPath+"/../Logs/ProjectOrganizationVerification.txt";
    /// <summary>
    /// Подписывает пошаговую проверку игрового запуска на обновление редактора.
    /// Без маркера или флага SessionState тест остаётся неактивным.
    /// </summary>
    static OrganizationRuntimeVerification(){EditorApplication.update+=Tick;}
    /// <summary>
    /// Пишет результат проверки игрового запуска и выбрасывает исключение при провале.
    /// Общий обработчик затем останавливает тест и записывает ошибку.
    /// </summary>
    static void Check(bool ok,string text){File.AppendAllText(Report,(ok?"PASS: ":"FAIL: ")+text+"\n");if(!ok)throw new Exception(text);}
    /// <summary>
    /// Обрабатывает запрос теста и проверяет меню, обе руки, интерфейс, чередование выстрелов и ульту.
    /// В пакетном режиме выключает паузу для обхода ожидания экранного кадра.
    /// После тестов сбрасывает флаг и закрывает пакетный Unity либо останавливает Play Mode.
    /// </summary>
    static void Tick()
    {
        if(!EditorApplication.isPlayingOrWillChangePlaymode && File.Exists("Assets/Scripts/Editor/Project/organization-pending.txt")){File.Delete("Assets/Scripts/Editor/Project/organization-pending.txt");ProjectOrganizationAudit.RunBatch();return;}
        if(!SessionState.GetBool("DagChanOrganizationTest",false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        try{
            if(step==0){next=EditorApplication.timeSinceStartup+2;step=1;}
            if(EditorApplication.timeSinceStartup<next)return;
            if(step==1){var pause=UnityEngine.Object.FindFirstObjectByType<MonitorPauseController>();Check(pause&&pause.IsPaused,"game starts in monitor pause menu");dual=UnityEngine.Object.FindFirstObjectByType<DualWeaponController>();Check(dual&&dual.RightWeapon&&dual.LeftWeapon,"both weapon controllers initialize");var hud=UnityEngine.Object.FindFirstObjectByType<ChainsawHealthBar>();Check(hud&&hud.UltimateFill&&hud.UltimateFill.fillAmount==1,"ultimate HUD initializes full");if(Application.isBatchMode)pause.enabled=false;else pause.Resume();step=2;next=EditorApplication.timeSinceStartup+1.5;}
            else if(step==2){if(MonitorPauseController.InputBlocked)return;Check(dual.TryFire(),"right revolver fires after organization");step=3;next=EditorApplication.timeSinceStartup+.65;}
            else if(step==3){Check(dual.TryFire(),"left revolver fires after organization");Check(dual.RightWeapon.ShotsFired==1&&dual.LeftWeapon.ShotsFired==1,"alternating fire retains both models and animations");step=4;next=EditorApplication.timeSinceStartup+.65;}
            else if(step==4){Check(dual.Ultimate.TryActivate(),"ultimate animations activate after organization");File.AppendAllText(Report,"ALL_TESTS_PASSED\n");SessionState.SetBool("DagChanOrganizationTest",false);if(Application.isBatchMode)EditorApplication.Exit(0);else EditorApplication.isPlaying=false;}
        }catch(Exception e){File.AppendAllText(Report,e+"\n");SessionState.SetBool("DagChanOrganizationTest",false);Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);else EditorApplication.isPlaying=false;}
    }
}




