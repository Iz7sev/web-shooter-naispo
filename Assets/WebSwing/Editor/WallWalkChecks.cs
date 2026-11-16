using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WebSwing.Editor
{
    public static class WallWalkChecks
    {
        [MenuItem("WebSwing/Preview Wall Walk Checks")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying)throw new Exception("Enter Play mode first");
            var world=UnityEngine.Object.FindFirstObjectByType<DemoWorld>();
            world.input.enabled=false; world.input.ReleaseAll(); world.modes.SelectMode(false);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name="Wall walk verification wall"; wall.layer=8;
            wall.transform.position=new Vector3(500,8,.8f); wall.transform.localScale=new Vector3(8,16,.3f);
            var controller=world.motor.GetComponent<CharacterController>();
            controller.enabled=false; world.motor.transform.position=new Vector3(500,2,0); controller.enabled=true;
            world.input.head.localRotation=Quaternion.identity;
            Physics.SyncTransforms();
            world.motor.SetWallWalkEnabled(true); world.motor.SetArmWalkSpeed(1);
            float initial=world.motor.transform.position.y,stoppedY=0,releasedY=0,rise=0;
            bool climbed=false,held=false,released=false,stopped=false;
            double start=EditorApplication.timeSinceStartup;
            EditorApplication.CallbackFunction tick=null;
            tick=()=>
            {
                if(!EditorApplication.isPlaying||!world){ EditorApplication.update-=tick; return; }
                double t=EditorApplication.timeSinceStartup-start;
                if(t<1.8)return;
                if(!stopped){rise=world.motor.transform.position.y-initial;climbed=world.motor.WallAttached&&world.motor.transform.position.y>initial+.6f;world.motor.SetArmWalkSpeed(0);stopped=true;}
                if(t<2.3)return;
                if(stoppedY==0)stoppedY=world.motor.transform.position.y;
                if(t<2.8)return;
                if(!released){held=world.motor.WallAttached&&Mathf.Abs(world.motor.transform.position.y-stoppedY)<.08f;releasedY=world.motor.transform.position.y;world.motor.SetWallWalkEnabled(false);released=true;}
                if(t<3.3)return;
                bool gravity=world.motor.transform.position.y<releasedY-.3f;
                bool upright=Vector3.Dot(world.input.head.up,Vector3.up)>.999f;
                string result=(climbed&&held&&gravity&&upright?"PASS":"FAIL")+": climb="+climbed+"; hold="+held+"; release gravity="+gravity+"; upright view="+upright+"; rise="+rise.ToString("F2");
                File.WriteAllText("ProjectSettings/WallWalkCheck.txt",result);
                world.motor.SetWallWalkEnabled(false); world.modes.ResetPosition(); world.input.enabled=true; world.modes.ShowMenu();
                UnityEngine.Object.Destroy(wall); EditorApplication.update-=tick;
                if(result.StartsWith("PASS"))Debug.Log("WEB_WALL_CHECK_OK: "+result);else Debug.LogError(result);
            };
            EditorApplication.update+=tick;
        }
    }
}
