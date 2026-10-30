using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
[InitializeOnLoad]
public static class PlayerWallCollisionVerification
{
    const string Marker="Assets/Scripts/Editor/Project/wall-collision-pending.txt",Flag="DagChan.WallVerify";
    static string Report=>Application.dataPath+"/../Logs/PlayerWallCollisionVerification.txt";
    static int step;static double next;
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    /// <summary>Подключает одноразовый тест физических столкновений к редактору и ожидает запрошенного входа в игру.</summary>
    static PlayerWallCollisionVerification(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Flag,false)){SessionState.SetBool(Flag,false);step=1;next=EditorApplication.timeSinceStartup+2;}};}
    /// <summary>Выбрасывает исключение с параметрами позы, если камера или её ближняя плоскость пересекли стену.</summary>
    static void AssertSafe(GameObject go,CharacterController body,Camera camera,Vector3 wallPosition,Vector3 forward,float height,float eye,string label)
    {
        var center=body.bounds.center-go.transform.position;
        if(Mathf.Abs(center.x)>.002f||Mathf.Abs(center.z)>.002f||Mathf.Abs(center.y-(height*.5f-eye))>.002f)throw new Exception("Capsule center shifted: "+label+" "+center);
        float distance=Vector3.Dot(wallPosition-go.transform.position,forward)-.2f;
        if(distance<.12f)throw new Exception("Camera too close or inside wall: "+label+" distance="+distance);
        foreach(var uv in new[]{new Vector3(0,0,camera.nearClipPlane),new Vector3(0,1,camera.nearClipPlane),new Vector3(1,0,camera.nearClipPlane),new Vector3(1,1,camera.nearClipPlane)})
        {
            var corner=camera.ViewportToWorldPoint(uv);if(Vector3.Dot(wallPosition-corner,forward)-.2f<=.005f)throw new Exception("Near plane cuts wall: "+label);
        }
    }
    /// <summary>Создаёт временную площадку вдали от уровня и проверяет подход к стене стоя и в подкате при 112 сочетаниях наклона и направления. Дополнительно проверяет большой шаг рывка и вращение вплотную к стене.</summary>
    static void RunCases()
    {
        DagChanInput.Actions.devices=new InputDevice[0];
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Wall test floor";floor.transform.position=new Vector3(3000,-.25f,3000);floor.transform.localScale=new Vector3(30,.5f,30);
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Wall test barrier";wall.transform.localScale=new Vector3(12,5,.4f);
        var go=new GameObject("Wall test player",typeof(CharacterController));go.transform.position=new Vector3(3000,1.6f,3000);var movement=go.AddComponent<PlayerMovement>();movement.enabled=false;movement.gravity=0;
        typeof(PlayerMovement).GetMethod("Start",Private).Invoke(movement,null);var body=go.GetComponent<CharacterController>();var camera=go.AddComponent<Camera>();camera.enabled=false;camera.nearClipPlane=.035f;camera.fieldOfView=60;camera.aspect=16f/9;
        var apply=typeof(PlayerMovement).GetMethod("ApplyCapsule",Private);var update=typeof(PlayerMovement).GetMethod("Update",Private);int cases=0;
        try{
            foreach(bool crouched in new[]{false,true})foreach(float yaw in new[]{0,45,90,135,180,225,270,315})foreach(float pitch in new[]{-89,-60,-30,0,30,60,89})
            {
                float height=crouched?movement.slideHeight:movement.standHeight,eye=crouched?movement.slideEyeHeight:movement.standEyeHeight;var forward=Quaternion.Euler(0,yaw,0)*Vector3.forward;
                body.enabled=false;go.transform.position=new Vector3(3000,eye,3000);go.transform.rotation=Quaternion.Euler(pitch,yaw,0);
                typeof(PlayerMovement).GetField("currentHeight",Private).SetValue(movement,height);typeof(PlayerMovement).GetField("currentEye",Private).SetValue(movement,eye);typeof(PlayerMovement).GetField("pitch",Private).SetValue(movement,pitch);typeof(PlayerMovement).GetField("yaw",Private).SetValue(movement,yaw);
                apply.Invoke(movement,null);body.enabled=true;wall.transform.position=new Vector3(3000,2.5f,3000)+forward*3;wall.transform.rotation=Quaternion.Euler(0,yaw,0);Physics.SyncTransforms();
                if(!crouched)update.Invoke(movement,null);
                for(int i=0;i<45;i++)body.Move(forward*.15f);
                Physics.SyncTransforms();AssertSafe(go,body,camera,wall.transform.position,forward,height,eye,"walk pitch="+pitch+" yaw="+yaw+" crouch="+crouched);
                body.Move(forward*12);Physics.SyncTransforms();AssertSafe(go,body,camera,wall.transform.position,forward,height,eye,"dash pitch="+pitch+" yaw="+yaw+" crouch="+crouched);
                cases++;
            }
            File.AppendAllText(Report,"PASS: camera and near plane stay outside wall in "+cases+" standing/crouched pitch/yaw combinations, including 12m swept moves\n");
            body.enabled=false;go.transform.position=new Vector3(3000,1.6f,3000);go.transform.rotation=Quaternion.identity;typeof(PlayerMovement).GetField("currentHeight",Private).SetValue(movement,movement.standHeight);typeof(PlayerMovement).GetField("currentEye",Private).SetValue(movement,movement.standEyeHeight);typeof(PlayerMovement).GetField("yaw",Private).SetValue(movement,0f);apply.Invoke(movement,null);body.enabled=true;wall.transform.position=new Vector3(3000,2.5f,3003);wall.transform.rotation=Quaternion.identity;Physics.SyncTransforms();body.Move(Vector3.forward*6);
            for(int i=-89;i<=89;i++){typeof(PlayerMovement).GetField("pitch",Private).SetValue(movement,(float)i);update.Invoke(movement,null);body.Move(Vector3.forward*.1f);Physics.SyncTransforms();AssertSafe(go,body,camera,wall.transform.position,Vector3.forward,movement.standHeight,movement.standEyeHeight,"continuous pitch="+i);}
            File.AppendAllText(Report,"PASS: turning camera from -89 to +89 degrees while pressed against wall preserves collision clearance\nALL_TESTS_PASSED\n");
        }finally{UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(wall);UnityEngine.Object.DestroyImmediate(floor);}
    }
    /// <summary>Запускает тест по маркеру, выходит из стартового меню и после проверок останавливает Play Mode без сохранения временных объектов.</summary>
    static void Tick()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        try{
            if(File.Exists(Marker)&&!EditorApplication.isPlayingOrWillChangePlaymode){File.Delete(Marker);File.WriteAllText(Report,"");SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
            if(step==0||EditorApplication.timeSinceStartup<next)return;
            if(step==1){UnityEngine.Object.FindFirstObjectByType<MonitorPauseController>().Resume();Application.targetFrameRate=-1;QualitySettings.vSyncCount=0;step=2;next=EditorApplication.timeSinceStartup+1.2;}
            else if(step==2){if(MonitorPauseController.InputBlocked)return;RunCases();step=0;EditorApplication.isPlaying=false;}
        }catch(Exception e){File.AppendAllText(Report,"FAIL: "+e+"\n");Debug.LogException(e);step=0;if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;}
    }
}
