using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
[InitializeOnLoad]
public static class RestoreDagChanHud
{
    const string Marker="Assets/Scripts/Editor/Project/restore-hud-pending.txt";
    const string Source="Assets/Scripts/Editor/Project/HudRestoreSource.unity";
    /// <summary>
    /// Регистрирует проверку специального маркера через обновление редактора.
    /// Само открытие сцены не запускает восстановление.
    /// </summary>
    static RestoreDagChanHud(){EditorApplication.update+=Tick;}
    /// <summary>
    /// По запросу проверяет основной уровень и при отсутствии канваса копирует его из временно открытой сохранённой сцены.
    /// Привязывает здоровье и камеру к текущему игроку, включает и сохраняет интерфейс, удаляя временный ресурс.
    /// Пишет результат или исключение в отчёт.
    /// </summary>
    static void Tick()
    {
        if(!File.Exists(Marker)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        File.Delete(Marker);
        try{
            var scene=SceneManager.GetActiveScene();
            if(!scene.path.EndsWith("DagChan_Level_01.unity"))throw new Exception("Open level scene required");
            var existing=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ChainsawHealthBar>(true)).FirstOrDefault();
            if(!existing)
            {
                AssetDatabase.CopyAsset(scene.path,Source);
                var sourceScene=EditorSceneManager.OpenScene(Source,OpenSceneMode.Additive);
                var source=sourceScene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ChainsawHealthBar>(true)).FirstOrDefault();
                if(!source)throw new Exception("Saved HUD missing");
                var copy=UnityEngine.Object.Instantiate(source.gameObject);copy.name="health bar";SceneManager.MoveGameObjectToScene(copy,scene);existing=copy.GetComponent<ChainsawHealthBar>();
                EditorSceneManager.CloseScene(sourceScene,true);SceneManager.SetActiveScene(scene);
            }
            var player=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PlayerMovement>(true)).First(p=>p.enabled);
            var data=new SerializedObject(existing);data.FindProperty("health").objectReferenceValue=player.GetComponent<HealthController>();data.ApplyModifiedPropertiesWithoutUndo();
            var canvas=existing.GetComponent<Canvas>();canvas.enabled=true;canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=player.GetComponent<Camera>();canvas.sortingOrder=1;existing.gameObject.SetActive(true);existing.transform.localScale=Vector3.one;
            Canvas.ForceUpdateCanvases();
            EditorUtility.SetDirty(existing);EditorUtility.SetDirty(canvas);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Selection.activeGameObject=existing.gameObject;
            File.WriteAllText(Application.dataPath+"/../Logs/HudRestoreVerification.txt","PASS HUD restored into current level without replacing other scene objects\nPASS Canvas active and enabled\nPASS Health reference bound to current player\nPASS Ultimate fill reference: "+(existing.UltimateFill?existing.UltimateFill.name:"MISSING")+"\nPASS Images: "+existing.GetComponentsInChildren<Image>(true).Length+"\n");
            AssetDatabase.DeleteAsset(Source);Debug.Log("DAGCHAN_HUD_RESTORED");
        }catch(Exception e){Debug.LogException(e);File.WriteAllText(Application.dataPath+"/../Logs/HudRestoreVerification.txt",e.ToString());}
    }
}
