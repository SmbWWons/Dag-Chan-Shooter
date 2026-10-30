using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PlayerDeathSetup
{
    const string Marker="Assets/Scripts/Editor/Project/player-death-pending.txt",Flag="DagChan.DeathVerify";
    const string ClipPath="Assets/Audio/DagChan/Player/DagChan_Death_NuSmertTakSmert.mp3";
    static string Report=>Application.dataPath+"/../Logs/PlayerDeathVerification.txt";
    static int step,sceneHandle;static double next,deadline;static HealthController health;static PlayerDeathController death;static PlayerMovement player;static float initialY;static MonitorPauseController pause;

    /// <summary>
    /// Подключает запрос настройки и запуск проверки после входа в Play Mode.
    /// Без файла-маркера и флага SessionState сцены и здоровье игрока не меняются.
    /// </summary>
    static PlayerDeathSetup(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Flag,false)){SessionState.SetBool(Flag,false);step=1;next=EditorApplication.timeSinceStartup+2;}};}

    /// <summary>
    /// Пишет результат проверки в отчёт и останавливает её исключением при провале.
    /// </summary>
    static void Check(bool ok,string text){File.AppendAllText(Report,(ok?"PASS: ":"FAIL: ")+text+"\n");if(!ok)throw new Exception(text);}

    /// <summary>
    /// Импортирует реплику и добавляет компонент смерти основному игроку обеих сцен, сохраняя текущие изменения.
    /// Включает сцены в Build Settings, чтобы перезапуск по пути работал и в собранной игре, затем запрашивает тест.
    /// </summary>
    static void Install()
    {
        File.WriteAllText(Report,"");AssetDatabase.ImportAsset(ClipPath);
        var importer=(AudioImporter)AssetImporter.GetAtPath(ClipPath);var sample=importer.defaultSampleSettings;sample.loadType=AudioClipLoadType.DecompressOnLoad;sample.preloadAudioData=true;importer.defaultSampleSettings=sample;importer.loadInBackground=false;importer.SaveAndReimport();
        var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);Check(clip,"renamed supplied death voice imports");
        var original=SceneManager.GetActiveScene();var builds=EditorBuildSettings.scenes.ToList();
        foreach(var path in new[]{"Assets/Scenes/DagChan_Level_01.unity","Assets/Scenes/SampleScene.unity"})
        {
            var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            var target=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PlayerMovement>(true)).First(p=>p.enabled&&p.GetComponent<Camera>());
            var component=target.GetComponent<PlayerDeathController>();if(!component)component=target.gameObject.AddComponent<PlayerDeathController>();
            var so=new SerializedObject(component);so.FindProperty("deathVoice").objectReferenceValue=clip;so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(component);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);if(opened)EditorSceneManager.CloseScene(scene,true);
            var entry=builds.FirstOrDefault(b=>b.path==path);if(entry==null)builds.Add(new EditorBuildSettingsScene(path,true));else entry.enabled=true;
            Check(true,"player death component and audio linked: "+path);
        }
        EditorBuildSettings.scenes=builds.ToArray();SceneManager.SetActiveScene(original);AssetDatabase.SaveAssets();SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;
    }

    /// <summary>
    /// По маркеру выполняет установку, затем проверяет смертельный урон, блокировку ввода, опускание камеры, звук и затемнение.
    /// Дожидается настоящей перезагрузки сцены и проверяет полное ХП и отсутствие стартовой паузы; завершает Play Mode после результата.
    /// </summary>
    static void Tick()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        try{
            if(File.Exists(Marker)&&!EditorApplication.isPlayingOrWillChangePlaymode){File.Delete(Marker);Install();}
            if(step==0||EditorApplication.timeSinceStartup<next)return;
            if(step==1)
            {
                death=UnityEngine.Object.FindFirstObjectByType<PlayerDeathController>();player=death.GetComponent<PlayerMovement>();health=death.GetComponent<HealthController>();pause=UnityEngine.Object.FindFirstObjectByType<MonitorPauseController>();
                Check(health.CurrentHealth==health.MaxHealth&&!PlayerDeathController.IsDying,"living player starts with full health");pause.Resume();Application.targetFrameRate=-1;QualitySettings.vSyncCount=0;step=2;next=EditorApplication.timeSinceStartup+1.2;
            }
            else if(step==2)
            {
                if(MonitorPauseController.InputBlocked)return;initialY=player.transform.position.y;sceneHandle=SceneManager.GetActiveScene().handle;
                Check(health.TakeDamage(health.MaxHealth+5),"lethal damage accepted by HealthController");
                Check(health.CurrentHealth==0&&health.IsDead&&PlayerDeathController.IsDying,"zero health event starts player death");
                Check(!player.enabled&&!player.GetComponent<CharacterController>().enabled&&MonitorPauseController.InputBlocked,"death disables movement and blocks gameplay input");
                Check(!player.GetComponent<DualWeaponController>().enabled&&!player.GetComponent<UltimateAttackController>().enabled,"death cancels firing and ultimate");
                Check(death.DeathVoice&&death.DeathVoice.clip.name=="DagChan_Death_NuSmertTakSmert"&&death.DeathVoice.isPlaying,"provided death voice plays once");
                Check(death.DeathVoice.GetComponent<DagChanEffectVolume>(),"death voice follows effects volume");Check(!health.TakeDamage(1),"repeated damage does not restart death");pause.TogglePause();Check(!pause.IsPaused,"Escape cannot open pause during death");
                step=3;next=EditorApplication.timeSinceStartup+.6;
            }
            else if(step==3)
            {
                Check(player.transform.position.y<initialY-.15f,"camera descends toward ground");Check(death.FadeOpacity>0&&death.FadeOpacity<1,"screen fades gradually to black");step=4;next=EditorApplication.timeSinceStartup+.45;
            }
            else if(step==4)
            {
                Check(death.FadeOpacity>.45f,"blackout progresses while voice finishes");step=5;deadline=EditorApplication.timeSinceStartup+15;
            }
            else if(step==5)
            {
                if(SceneManager.GetActiveScene().handle==sceneHandle){if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Scene restart timed out");return;}
                step=6;next=EditorApplication.timeSinceStartup+.4;
            }
            else if(step==6)
            {
                var reborn=UnityEngine.Object.FindFirstObjectByType<PlayerDeathController>();var hp=reborn.GetComponent<HealthController>();var menu=UnityEngine.Object.FindFirstObjectByType<MonitorPauseController>();
                Check(reborn&&hp.CurrentHealth==hp.MaxHealth&&!hp.IsDead,"same level restarts with full health");Check(!PlayerDeathController.IsDying&&!MonitorPauseController.InputBlocked&&!menu.IsPaused&&Time.timeScale==1,"respawn restores input without startup menu");Check(reborn.GetComponent<PlayerMovement>().enabled&&reborn.GetComponent<CharacterController>().enabled,"respawn restores movement and collision");
                File.AppendAllText(Report,"ALL_TESTS_PASSED\n");step=0;EditorApplication.isPlaying=false;
            }
        }catch(Exception e){File.AppendAllText(Report,e+"\n");Debug.LogException(e);step=0;if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;}
    }
}
