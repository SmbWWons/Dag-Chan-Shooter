using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class Level01Builder
{
    public const string ScenePath="Assets/Scenes/DagChan_Level_01.unity";
    const string Folder="Assets/";
    static Transform root;
    static Material wall,cover,route,gateMaterial;
    const float Height=5f,Thickness=.4f,Door=3.6f;
    class Room
    {
        public string name;public Vector2 center,size;public Color color;public Dictionary<string,float[]> doors=new Dictionary<string,float[]>();
        /// <summary>
        /// Сохраняет описание комнаты: имя, центр, размеры и цвет пола.
        /// Словарь дверных проёмов заполняется отдельно при настройке плана.
        /// </summary>
        public Room(string n,Vector2 c,Vector2 s,Color tint){name=n;center=c;size=s;color=tint;}
    }
    /// <summary>
    /// Находит материал уровня по имени либо создаёт ресурс URP Lit.
    /// Обновляет цвет и гладкость и помечает материал изменённым для сохранения.
    /// </summary>
    static Material Material(string name,Color color)
    {
        string path=Folder+"Materials/Level/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
        mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",.12f);EditorUtility.SetDirty(mat);return mat;
    }
    /// <summary>
    /// Создаёт куб нужных позиции, размера и материала в заданной группе или корне уровня.
    /// У декора убирает коллайдер, а твёрдым объектам назначает статические флаги рендера.
    /// </summary>
    static GameObject Box(string name,Vector3 pos,Vector3 size,Material material,Transform parent=null,bool solid=true)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent?parent:root,false);go.transform.position=pos;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;
        if(!solid)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        else GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccludeeStatic|StaticEditorFlags.OccluderStatic);
        return go;
    }
    /// <summary>
    /// Создаёт текстовую подпись на полу с встроенным шрифтом.
    /// Настраивает положение, разворот, размер и материал TextMesh.
    /// </summary>
    static void Label(string text,Vector3 position,float size=1f,Transform parent=null)
    {
        var go=new GameObject("Label_"+text);go.transform.SetParent(parent?parent:root,false);go.transform.position=position;go.transform.rotation=Quaternion.Euler(90,0,0);
        var mesh=go.AddComponent<TextMesh>();mesh.text=text;mesh.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");mesh.fontSize=80;mesh.characterSize=size*.12f;mesh.anchor=TextAnchor.MiddleCenter;mesh.alignment=TextAlignment.Center;mesh.color=new Color(.9f,.95f,1);
        go.GetComponent<MeshRenderer>().sharedMaterial=mesh.font.material;
    }
    /// <summary>
    /// Вычисляет границы стены и сортирует дверные проёмы.
    /// Внутренняя функция segment строит оставшиеся сплошные участки, а над проёмами создаются перемычки.
    /// </summary>
    static void WallSide(Transform group,string side,Room room)
    {
        bool horizontal=side=="N" || side=="S";float min=horizontal?room.center.x-room.size.x/2:room.center.y-room.size.y/2;float max=horizontal?room.center.x+room.size.x/2:room.center.y+room.size.y/2;
        float fixedPos=horizontal?room.center.y+(side=="N"?1:-1)*room.size.y/2:room.center.x+(side=="E"?1:-1)*room.size.x/2;
        var holes=room.doors.ContainsKey(side)?room.doors[side].OrderBy(v=>v).ToArray():new float[0];float cursor=min;
        Action<float,float> segment=(a,b)=>{
            if(b-a<.01f)return;Box("Wall_"+side,new Vector3(horizontal?(a+b)/2:fixedPos,Height/2,horizontal?fixedPos:(a+b)/2),new Vector3(horizontal?b-a:Thickness,Height,horizontal?Thickness:b-a),wall,group);
        };
        foreach(float hole in holes)
        {
            segment(cursor,hole-Door/2);cursor=hole+Door/2;
            var pos=new Vector3(horizontal?hole:fixedPos,Height-.17f,horizontal?fixedPos:hole);
            Box("Door lintel_"+side,pos,new Vector3(horizontal?Door:Thickness,.35f,horizontal?Thickness:Door),wall,group).layer=9;
            Box("Door stripe_"+side,new Vector3(pos.x,.025f,pos.z),new Vector3(horizontal?Door:.45f,.04f,horizontal?.45f:Door),route,group,false);
        }
        segment(cursor,max);
    }
    /// <summary>
    /// Создаёт группу комнаты и пол и вызывает WallSide для четырёх сторон.
    /// Возвращает группу для последующего размещения укрытий и точек появления.
    /// </summary>
    static Transform BuildRoom(Room room)
    {
        var group=new GameObject(room.name).transform;group.SetParent(root,false);
        Box("Floor",new Vector3(room.center.x,-.2f,room.center.y),new Vector3(room.size.x,.4f,room.size.y),Material(room.name+"_Floor",room.color),group);
        foreach(string side in new[]{"N","S","E","W"})WallSide(group,side,room);
        return group;
    }
    /// <summary>
    /// Создаёт перекрывающий проход объект с NavMeshObstacle и carving.
    /// До команды арены оставляет ворота выключенными и возвращает готовый объект.
    /// </summary>
    static GameObject Gate(string name,Vector3 at,bool alongX)
    {
        var go=Box(name,at+Vector3.up*2.4f,new Vector3(alongX?Door:.35f,4.8f,alongX?.35f:Door),gateMaterial);
        var obstacle=go.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;obstacle.size=Vector3.one;obstacle.carving=true;
        GameObjectUtility.SetStaticEditorFlags(go,0);go.SetActive(false);return go;
    }
    /// <summary>
    /// Строит твёрдое укрытие и добавляет расширенный NavMeshModifierVolume непроходимой области.
    /// Благодаря этому рассчитанный путь обходит препятствие с запасом.
    /// </summary>
    static void Cover(Transform parent,Vector2 center,Vector2 size,float height=1.6f)
    {
        var go=Box("Cover",new Vector3(center.x,height/2,center.y),new Vector3(size.x,height,size.y),cover,parent);
        var pad=go.AddComponent<NavMeshModifierVolume>();pad.area=1;pad.center=Vector3.zero;pad.size=new Vector3(1+.5f/size.x,1+4/height,1+.5f/size.y);
    }
    /// <summary>
    /// Размещает штрихи и треугольные стрелки по отрезкам заданного маршрута.
    /// Создаёт либо обновляет меши стрелок с сохранением ссылок существующих ресурсов.
    /// </summary>
    static void DashedRoute(Vector2[] nodes)
    {
        for(int i=1;i<nodes.Length;i++)
        {
            var start=new Vector3(nodes[i-1].x,.035f,nodes[i-1].y);var end=new Vector3(nodes[i].x,.035f,nodes[i].y);var direction=(end-start).normalized;float distance=Vector3.Distance(start,end);
            for(float d=.5f;d<distance;d+=2f){var go=Box("Route dash",start+direction*d,new Vector3(.22f,.04f,.9f),route,null,false);go.transform.rotation=Quaternion.LookRotation(direction);}
            var arrow=new GameObject("Route arrow");arrow.transform.SetParent(root,false);
            var mesh=new Mesh();Vector3 side=Vector3.Cross(Vector3.up,direction);
            mesh.vertices=new[]{end,end-direction*.95f+side*.55f,end-direction*.95f-side*.55f};mesh.triangles=new[]{0,1,2};mesh.RecalculateNormals();
            string arrowPath=Folder+"Models/Level/RouteArrow_"+i+".asset";var oldArrow=AssetDatabase.LoadAssetAtPath<Mesh>(arrowPath);
            if(oldArrow){EditorUtility.CopySerialized(mesh,oldArrow);UnityEngine.Object.DestroyImmediate(mesh);mesh=oldArrow;EditorUtility.SetDirty(mesh);}else AssetDatabase.CreateAsset(mesh,arrowPath);
            arrow.AddComponent<MeshFilter>().sharedMesh=mesh;arrow.AddComponent<MeshRenderer>().sharedMaterial=route;

        }
    }
    /// <summary>
    /// Создаёт триггер комнаты, шесть точек появления и декоративные отметки.
    /// Добавляет ArenaWaveController и передаёт ему префаб, ворота, размеры волн и название арены.
    /// </summary>
    static ArenaWaveController Arena(Transform group,Room room,GameObject prefab,GameObject[] gates,int[] counts,int number)
    {
        var go=new GameObject("Arena_"+number+"_WaveTrigger");go.transform.SetParent(root,false);go.transform.position=new Vector3(room.center.x,2,room.center.y);
        var trigger=go.AddComponent<BoxCollider>();trigger.isTrigger=true;trigger.size=new Vector3(room.size.x-2,4,room.size.y-2);
        var spawns=new List<Transform>();
        foreach(var point in new[]{new Vector2(-.5f,-.5f),new Vector2(.5f,-.5f),new Vector2(-.5f,.5f),new Vector2(.5f,.5f),new Vector2(0,.5f),new Vector2(.5f,0)})
        {
            var spawn=new GameObject("Spawn_"+(spawns.Count+1)).transform;spawn.SetParent(group,false);
            spawn.position=new Vector3(room.center.x+point.x*(room.size.x-7),.05f,room.center.y+point.y*(room.size.y-7));spawns.Add(spawn);
            Box("Spawn tile",new Vector3(spawn.position.x,.015f,spawn.position.z),new Vector3(1.5f,.025f,1.5f),Material("Spawn_Pink",new Color(.52f,.18f,.3f)),group,false);
        }
        var arena=go.AddComponent<ArenaWaveController>();arena.Configure(prefab,spawns.ToArray(),gates,counts,"Арена "+number);return arena;
    }
    /// <summary>
    /// По команде редактора перестраивает Level_01_Blockout из плана комнат: стены, укрытия, маршрут, арены и выход.
    /// Запекает NavMesh, задаёт позицию игрока и сохраняет сцену и вид сверху.
    /// Метод выполняется вне Play Mode и является инструментом построения, а не частью каждого игрового запуска.
    /// </summary>
    [MenuItem("Tools/Level 01/Build Level From Plan")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play Mode before building level");
        Directory.CreateDirectory(Application.dataPath+"/Materials/Level");AssetDatabase.Refresh();
        var scene=SceneManager.GetActiveScene();
        if(scene.path!=ScenePath)
        {
            if(File.Exists(ScenePath))throw new Exception("Level scene already exists; open it before rebuilding");
            if(scene.isDirty)EditorSceneManager.SaveScene(scene);
            EditorSceneManager.SaveScene(scene,ScenePath,true);scene=EditorSceneManager.OpenScene(ScenePath);
        }
        var player=UnityEngine.Object.FindObjectsByType<PlayerMovement>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(p=>p.GetComponent<Camera>() && p.isActiveAndEnabled && p.GetComponent<Camera>().enabled);
        foreach(var go in scene.GetRootGameObjects())
        {
            if(go.name=="Level_01_Blockout"){UnityEngine.Object.DestroyImmediate(go);continue;}
            bool keep=go.GetComponentsInChildren<PlayerMovement>(true).Contains(player) || go.GetComponentInChildren<Canvas>(true) || go.GetComponentInChildren<Light>(true) || go.GetComponentInChildren<Volume>(true);
            if(!keep)go.SetActive(false);
            foreach(var bear in go.GetComponentsInChildren<GummyBearEnemy>(true))bear.gameObject.SetActive(false);
        }
        root=new GameObject("Level_01_Blockout").transform;
        wall=Level01WipMaterial.EnsureMaterial();cover=Material("Cover_Grey",new Color(.31f,.36f,.4f));route=Material("Route_Cyan",new Color(.04f,.65f,.95f));gateMaterial=Material("Gates_Red",new Color(.75f,.15f,.2f));
        var start=new Room("01_Start",new Vector2(-28,-12),new Vector2(8,8),new Color(.28f,.5f,.4f));start.doors["N"]=new[]{-28f};
        var medium=new Room("02_Medium",new Vector2(-28,0),new Vector2(14,16),new Color(.52f,.46f,.3f));medium.doors["S"]=new[]{-28f};medium.doors["E"]=new[]{0f};
        var one=new Room("03_Arena_01",new Vector2(-7,3),new Vector2(28,24),new Color(.48f,.31f,.3f));one.doors["W"]=new[]{0f};one.doors["S"]=new[]{0f};
        var corridor=new Room("04_LongCorridor",new Vector2(20,-11.5f),new Vector2(50,5),new Color(.27f,.38f,.5f));corridor.doors["N"]=new[]{0f,33f};
        var two=new Room("05_Arena_02",new Vector2(33,3),new Vector2(24,24),new Color(.3f,.47f,.36f));two.doors["S"]=new[]{33f};two.doors["E"]=new[]{2f};
        var groups=new[]{start,medium,one,corridor,two}.Select(BuildRoom).ToArray();
        Label("СТАРТ",new Vector3(-28,.035f,-13),1.1f,groups[0]);Label("СРЕДНЯЯ КОМНАТА",new Vector3(-28,.035f,5.8f),.8f,groups[1]);Label("АРЕНА 1 — ВОЛНЫ",new Vector3(-7,.035f,12.8f),1.1f,groups[2]);Label("ДЛИННЫЙ КОРИДОР",new Vector3(16,.04f,-13.25f),.8f,groups[3]);Label("АРЕНА 2 — ВОЛНЫ",new Vector3(33,.035f,12.8f),1.1f,groups[4]);
        Cover(groups[1],new Vector2(-28,4),new Vector2(3,1.5f));Cover(groups[1],new Vector2(-32,-1),new Vector2(1.5f,3));Cover(groups[1],new Vector2(-25,-4),new Vector2(2.5f,1.5f));
        foreach(var item in new[]{new Vector4(-14,9,3,3),new Vector4(1,9,3,3),new Vector4(-7,5,6,2),new Vector4(-15,-2,3,5),new Vector4(-7,-3,6,2),new Vector4(1,-2,3,5)})Cover(groups[2],new Vector2(item.x,item.y),new Vector2(item.z,item.w));
        foreach(var item in new[]{new Vector4(27,9,3,3),new Vector4(39,9,3,3),new Vector4(33,4,6,2),new Vector4(25,-1,2.5f,5),new Vector4(41,-1,2.5f,5),new Vector4(28,-4,3,2),new Vector4(38,-4,3,2)})Cover(groups[4],new Vector2(item.x,item.y),new Vector2(item.z,item.w));
        DashedRoute(new[]{new Vector2(-28,-14),new Vector2(-28,-2),new Vector2(-22,0),new Vector2(-18,0),new Vector2(-18,-6),new Vector2(0,-6),new Vector2(0,-11.5f),new Vector2(33,-11.5f),new Vector2(33,-2),new Vector2(36,-2),new Vector2(37,2),new Vector2(44,2),new Vector2(49,2)});
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/GummyBear_Animated.prefab");if(!prefab)throw new Exception("Bear prefab missing");
        var entry=Gate("Arena1_EntryGate",new Vector3(-21,0,0),false);var firstExit=Gate("Arena1_ExitGate",new Vector3(0,0,-9),true);var secondEntry=Gate("Arena2_EntryGate",new Vector3(33,0,-9),true);var exitGate=Gate("Level_ExitGate",new Vector3(45,0,2),false);
        var a1=Arena(groups[2],one,prefab,new[]{entry,firstExit},new[]{3,5,7},1);var a2=Arena(groups[4],two,prefab,new[]{secondEntry},new[]{5,7,10},2);
        Box("Exit platform",new Vector3(48,-.2f,2),new Vector3(6,.4f,4),Material("Exit_Mint",new Color(.18f,.65f,.48f)));Label("ВЫХОД",new Vector3(48,.035f,2),1);
        var goal=new GameObject("LevelExit");goal.transform.SetParent(root,false);goal.transform.position=new Vector3(48,2,2);var exitTrigger=goal.AddComponent<BoxCollider>();exitTrigger.isTrigger=true;exitTrigger.size=new Vector3(4,4,3.6f);var exit=goal.AddComponent<LevelExitController>();exit.Configure(new[]{a1,a2},exitGate);

        var surface=root.gameObject.AddComponent<NavMeshSurface>();surface.collectObjects=CollectObjects.Children;surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.layerMask=1;surface.BuildNavMesh();
        if(!surface.navMeshData)throw new Exception("NavMesh bake failed");
        var navPath=Folder+"Data/Navigation/Level01NavMesh.asset";var nav=AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
        if(nav){EditorUtility.CopySerialized(surface.navMeshData,nav);surface.RemoveData();surface.navMeshData=nav;surface.AddData();EditorUtility.SetDirty(nav);}else AssetDatabase.CreateAsset(surface.navMeshData,navPath);
        var controller=player.GetComponent<CharacterController>();controller.enabled=false;player.transform.position=new Vector3(-28,player.standEyeHeight+.06f,-13);player.transform.rotation=Quaternion.identity;controller.enabled=true;EditorUtility.SetDirty(player);
        foreach(var listener in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<AudioListener>(true)))listener.enabled=listener.gameObject==player.gameObject;
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Selection.activeGameObject=root.gameObject;if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.LookAt(new Vector3(8,0,0),Quaternion.Euler(90,0,0),48);
        RenderPlan();Debug.Log("LEVEL01_BUILD_OK scene="+ScenePath);
    }
    /// <summary>
    /// Рендерит вид сверху временной ортографической камерой в RenderTexture.
    /// Сохраняет PNG в outputs, восстанавливает активную текстуру и удаляет временные объекты.
    /// </summary>
    static void RenderPlan()
    {
        var go=new GameObject("Temporary Plan Camera");var cam=go.AddComponent<Camera>();cam.transform.position=new Vector3(8,90,-.5f);cam.transform.rotation=Quaternion.Euler(90,0,0);cam.orthographic=true;cam.orthographicSize=23;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.08f,.1f,.13f);cam.cullingMask=1;
        var texture=new RenderTexture(2000,1000,24);var request=new UniversalRenderPipeline.SingleCameraRequest{destination=texture};RenderPipeline.SubmitRenderRequest(cam,request);
        var previous=RenderTexture.active;RenderTexture.active=texture;var image=new Texture2D(2000,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,2000,1000),0,0);image.Apply();File.WriteAllBytes(@"C:\Users\bvdga\Documents\Codex\2026-10-27\new-chat-4\outputs\Level01_Unity_TopDown.png",image.EncodeToPNG());RenderTexture.active=previous;
        UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(go);
    }
}

