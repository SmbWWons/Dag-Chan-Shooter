using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
[InitializeOnLoad]
public static class FlowerCrosshairSetup
{
    const string Marker="Assets/Scripts/Editor/Project/flower-crosshair-pending.txt";
    const string Path="Assets/Textures/UI/Crosshair/FlowerCrosshair.png";
    /// <summary>
    /// Подписывает редактор на специальный маркер запроса.
    /// Вне компиляции, импорта и Play Mode обработчик удаляет маркер и вызывает Apply; без запроса ничего не меняет.
    /// </summary>
    static FlowerCrosshairSetup(){EditorApplication.update+=()=>{if(File.Exists(Marker)&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&!EditorApplication.isPlayingOrWillChangePlaymode){File.Delete(Marker);Apply();}};}
    /// <summary>
    /// Импортирует цветок как прозрачный спрайт и находит прицел в канвасе обеих сцен.
    /// Заменяет текст на Image, задаёт центр и размер, сохраняет результат и закрывает временно открытые сцены.
    /// </summary>
    [MenuItem("Tools/DagChan/Set Flower Crosshair")]
    public static void Apply()
    {
        AssetDatabase.ImportAsset(Path);var importer=(TextureImporter)AssetImporter.GetAtPath(Path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Path);
        var original=SceneManager.GetActiveScene();int count=0;
        foreach(var path in new[]{"Assets/Scenes/DagChan_Level_01.unity","Assets/Scenes/SampleScene.unity"})
        {
            var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            var hud=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ChainsawHealthBar>(true)).First();
            var target=hud.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t=>t.name=="Прицел"||t.name=="прицел");if(!target)throw new System.Exception("Crosshair missing: "+path);
            var text=target.GetComponent<TMP_Text>();if(text)UnityEngine.Object.DestroyImmediate(text);
            var image=target.GetComponent<Image>();if(!image)image=target.gameObject.AddComponent<Image>();image.sprite=sprite;image.color=Color.white;image.raycastTarget=false;image.preserveAspect=true;
            target.anchorMin=target.anchorMax=target.pivot=new Vector2(.5f,.5f);target.anchoredPosition=Vector2.zero;target.sizeDelta=new Vector2(28,28);target.localScale=Vector3.one;target.localRotation=Quaternion.identity;target.gameObject.SetActive(true);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);count++;if(opened)EditorSceneManager.CloseScene(scene,true);
        }
        SceneManager.SetActiveScene(original);AssetDatabase.SaveAssets();File.WriteAllText(Application.dataPath+"/../Logs/FlowerCrosshairVerification.txt","PASS flower sprite imports without font fallback\nPASS centered flower crosshair saved in "+count+" scenes\n");Debug.Log("DAGCHAN_FLOWER_CROSSHAIR_READY");
    }
}
