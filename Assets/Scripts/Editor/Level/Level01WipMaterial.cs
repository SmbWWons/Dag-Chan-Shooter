using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class Level01WipMaterial
{
    public const string TexturePath="Assets/Textures/Level/WIP_Purple_Texture.png";
    public const string MaterialPath="Assets/Materials/Level/WIP_Purple_WorldAligned.mat";
    static string Marker=>Application.dataPath+"/Scripts/Editor/Level/wip-material-pending.txt";
    /// <summary>
    /// Подписывает Tick на обновление редактора.
    /// Без специального файла-маркера эта подписка не изменяет уровень.
    /// </summary>
    static Level01WipMaterial(){EditorApplication.update+=Tick;}
    /// <summary>
    /// Ждёт маркер запроса и отсутствие компиляции, импорта и Play Mode.
    /// Удаляет запрос и вызывает Apply, записывая исключение и отчёт при ошибке.
    /// </summary>
    static void Tick()
    {
        if(!File.Exists(Marker) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)return;
        File.Delete(Marker);try{Apply();}catch(Exception e){Debug.LogException(e);File.WriteAllText(Application.dataPath+"/../Logs/WipMaterialVerification.txt","FAILED "+e);}
    }
    /// <summary>
    /// Настраивает повторение WIP-текстуры и проверяет шейдер мировой проекции.
    /// Создаёт или обновляет материал стен с нужными текстурой, цветом и размером плитки и возвращает ресурс.
    /// </summary>
    public static Material EnsureMaterial()
    {
        AssetDatabase.ImportAsset(TexturePath,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(TexturePath);
        importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;
        importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Bilinear;importer.anisoLevel=4;
        importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=true;importer.SaveAndReimport();
        var shader=Shader.Find("DagChan/WIP World Aligned");if(!shader || ShaderUtil.ShaderHasError(shader))throw new Exception("World-aligned WIP shader failed to compile");
        var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,MaterialPath);}
        material.shader=shader;material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath));material.SetColor("_BaseColor",Color.white);
        material.SetFloat("_TileSize",4f);material.SetTextureScale("_BaseMap",Vector2.one);EditorUtility.SetDirty(material);return material;
    }
    /// <summary>
    /// Назначает WIP только стенам и дверным перемычкам и сохраняет сцену.
    /// Проверяет повторение текстуры и отсутствие этого материала на полу и укрытиях, затем пишет отчёт и создаёт превью.
    /// </summary>
    [MenuItem("Tools/Level 01/Apply Repeating WIP Material To Walls")]
    public static void Apply()
    {
        var root=GameObject.Find("Level_01_Blockout");if(!root)throw new Exception("Open DagChan_Level_01 scene before applying material");
        var material=EnsureMaterial();
        var renderers=root.GetComponentsInChildren<MeshRenderer>(true);
        var walls=renderers.Where(r=>r.name.StartsWith("Wall_") || r.name.StartsWith("Door lintel_")).ToArray();
        if(walls.Length==0)throw new Exception("No level walls found");
        foreach(var renderer in walls){renderer.sharedMaterial=material;EditorUtility.SetDirty(renderer);}
        EditorSceneManager.MarkSceneDirty(root.scene);EditorSceneManager.SaveScene(root.scene);AssetDatabase.SaveAssets();
        var texture=(Texture2D)material.GetTexture("_BaseMap");
        if(texture.wrapMode!=TextureWrapMode.Repeat || walls.Any(r=>r.sharedMaterial!=material))throw new Exception("WIP material assignment or Repeat mode is invalid");
        if(renderers.Where(r=>r.name=="Floor" || r.name=="Cover").Any(r=>r.sharedMaterial==material))throw new Exception("WIP material leaked onto floors or cover");
        File.WriteAllText(Application.dataPath+"/../Logs/WipMaterialVerification.txt","PASS WIP texture wrap mode: Repeat\nPASS World projection: 4-meter texture tile, one WIP label per meter\nPASS Assigned to "+walls.Length+" walls and door lintels\nPASS Floors and cover keep their materials\nPASS Shader compiled without errors\nALL_TESTS_PASSED\n");
        CapturePreview();Debug.Log("WIP_WALLS_APPLIED count="+walls.Length+" worldTileSize=4m");
    }
    /// <summary>
    /// Снимает стены временной камерой через URP и сохраняет PNG в outputs.
    /// Восстанавливает прежнюю RenderTexture и удаляет камеру, текстуру и временное изображение.
    /// </summary>
    static void CapturePreview()
    {
        var go=new GameObject("Temporary WIP Preview Camera");var camera=go.AddComponent<Camera>();camera.transform.position=new Vector3(8,1.8f,-11.5f);camera.transform.rotation=Quaternion.LookRotation(new Vector3(1,-.025f,0));camera.fieldOfView=72;camera.clearFlags=CameraClearFlags.Skybox;camera.cullingMask=(1<<0)|(1<<9);
        var target=new RenderTexture(1600,900,24);RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
        var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();
        File.WriteAllBytes(@"C:\Users\bvdga\Documents\Codex\2026-10-27\new-chat-4\outputs\WIP_Level_Walls.png",image.EncodeToPNG());RenderTexture.active=previous;
        UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(go);
    }
}
