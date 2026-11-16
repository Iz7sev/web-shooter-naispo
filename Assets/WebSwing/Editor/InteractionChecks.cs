using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WebSwing.Editor
{
    [InitializeOnLoad]
    public static class InteractionChecks
    {
        static IEnumerator flow;
        static int frame=-1;
        static readonly List<string> results=new List<string>();
        const string Flag="WebSwing.InteractionChecks";
        static InteractionChecks() { EditorApplication.update+=Tick; }
        [MenuItem("WebSwing/Run Interaction Checks")]
        public static void Begin()
        {
            SessionState.SetBool(Flag,true);
            EditorApplication.isPlaying=true;
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling) return;
            if(frame==Time.frameCount)return;frame=Time.frameCount;
            try
            {
                if(flow==null)flow=Run();
                if(flow.MoveNext())return;
                SessionState.SetBool(Flag,false);
                File.WriteAllLines("ProjectSettings/InteractionCheck.txt",results);
                Debug.Log("WEB_INTERACTION_CHECK_OK: "+string.Join("; ",results));
                if(Application.isBatchMode)EditorApplication.Exit(0);else EditorApplication.isPlaying=false;
            }
            catch(Exception error)
            {
                SessionState.SetBool(Flag,false);
                File.WriteAllText("ProjectSettings/InteractionCheck.txt","FAIL: "+error+"\n"+string.Join("\n",results));
                Debug.LogException(error);if(Application.isBatchMode)EditorApplication.Exit(1);else EditorApplication.isPlaying=false;
            }
        }
        static void Check(bool condition,string name)
        { if(!condition)throw new Exception(name);results.Add("PASS: "+name); }
        static IEnumerator Run()
        {
            Check(InteractionMotion.MenuPeace(.96f,.96f,.4f,.4f),"peace sign recognized as menu pose");
            Check(!InteractionMotion.MenuPeace(.4f,.4f,.4f,.4f),"closed fist never opens menu");
            Check(!InteractionMotion.MenuPeace(.96f,.4f,.4f,.96f),"web pose never opens menu");
            Check(!InteractionMotion.MenuPeace(.96f,.96f,.96f,.96f),"open palm never opens menu");
            Check(!InteractionMotion.MenuPeace(.96f,.4f,.4f,.4f),"pointing never opens menu");
            var menuHold=new MenuPoseHold();bool fired=false;
            for(int i=0;i<14;i++)fired|=menuHold.Step(true,false,.1f);
            Check(!fired,"menu requires continuous hold longer than 1.4 seconds");
            fired=false;for(int i=0;i<16;i++)fired|=menuHold.Step(false,true,.1f);
            Check(fired,"either hand opens menu after 1.5 seconds");
            fired=false;for(int i=0;i<30;i++)fired|=menuHold.Step(false,true,.1f);
            Check(!fired,"held peace sign only activates once");
            menuHold.Step(false,false,.1f);
            for(int i=0;i<8;i++)menuHold.Step(true,false,.1f);
            menuHold.Step(false,false,.1f);fired=false;
            for(int i=0;i<8;i++)fired|=menuHold.Step(true,false,.1f);
            Check(!fired,"tracking loss or pose break resets menu hold");
            menuHold.ResetProgress();fired=false;
            for(int i=0;i<20;i++)fired|=menuHold.Step(i%2==0,i%2!=0,.1f);
            Check(!fired,"switching hands cannot accumulate menu time");
            var history=new ThrowMotionMemory();
            history.Sample(new Vector3(.2f,.4f,2f),Vector3.forward,true,.02f);
            history.Sample(Vector3.down*.3f,Vector3.forward,true,.16f);
            Check(history.Velocity==new Vector3(.2f,.4f,2f),"release slowdown preserves push direction and strength");
            history.Sample(Vector3.zero,Vector3.forward,true,.1f);
            Check(history.Velocity==Vector3.zero,"stale throw motion expires");
            history.Sample(Vector3.forward*2,Vector3.forward,true,.02f);history.Clear();
            Check(history.Velocity==Vector3.zero,"tracking-loss history reset");
            history.Sample(Vector3.forward*2,Vector3.forward,false,.02f);
            Check(history.Velocity==Vector3.zero,"pull motion before carry cannot throw");
            yield return null;
            var world=UnityEngine.Object.FindFirstObjectByType<DemoWorld>();
            Check(world!=null,"world initialized");
            world.input.enabled=false;world.input.ReleaseAll();world.motor.SetMovementLocked(true);world.shooter.EasyThrow=false;
            var hand=world.shooter.hand;
            var target=GameObject.CreatePrimitive(PrimitiveType.Cube);target.layer=8;target.transform.position=new Vector3(500,5,0);
            hand.SetPositionAndRotation(new Vector3(500,5,-3),Quaternion.identity);Physics.SyncTransforms();
            world.input.StepTrackedGesture(1,false,true,Vector3.zero,.02f);
            world.input.StepTrackedGesture(1,true,false,Vector3.zero,.12f);
            Check(world.shooter.Active,"pose attaches");
            world.input.StepTrackedGesture(1,false,false,Vector3.zero,.2f);
            world.input.StepTrackedGesture(1,true,false,Vector3.zero,.12f);
            Check(world.shooter.Active,"repeat pose does not detach");
            float staticUntil=Time.realtimeSinceStartup+.85f;
            while(Time.realtimeSinceStartup<staticUntil)yield return null;
            Check(world.shooter.Active,"stationary web persists until explicit release");
            world.input.StepTrackedGesture(1,false,true,Vector3.zero,.6f);
            Check(world.shooter.Active,"open palm below threshold keeps web");
            world.input.StepTrackedGesture(1,false,true,Vector3.zero,.11f);
            Check(!world.shooter.Active,"open palm 0.7s releases");
            UnityEngine.Object.Destroy(target);
            // Spawn a nearby crate and let the real FixedUpdate carry assist take ownership.
            var crate=CityEnvironment.SpawnCrate(new Vector3(500,5,0),2,90);var body=crate.GetComponent<Rigidbody>();
            yield return null; // SpawnCrate removes primitive detail colliders at frame end.
            hand.SetPositionAndRotation(new Vector3(500,5,-.7f),Quaternion.identity);Physics.SyncTransforms();
            Check(world.shooter.Fire(1),"cargo attaches");
            float until=Time.realtimeSinceStartup+.25f;
            while(Time.realtimeSinceStartup<until)yield return null;
            Check(world.shooter.Carrying,"cargo carry assist");
            world.input.StepTrackedGesture(1,false,false,Vector3.forward*2,.02f);
            world.input.StepTrackedGesture(1,false,true,Vector3.down*.2f,.1f);
            Check(!world.shooter.Active && body.linearVelocity.z>3.4f && body.linearVelocity.z<3.8f,"delayed open palm uses earlier forward push, not downward slowdown");
            Check(InteractionMotion.ThrowVelocity(Vector3.one*100).magnitude<=7.001f,"throw speed bounded");
            Check(!InteractionMotion.ThrowGesture(Vector3.zero,Vector3.forward)&&!InteractionMotion.ThrowGesture(Vector3.back*2,Vector3.forward),"stationary and backward palms do not throw");
            body.position=new Vector3(500,5,0);body.linearVelocity=Vector3.zero;
            hand.SetPositionAndRotation(new Vector3(500,5,-.7f),Quaternion.identity);Physics.SyncTransforms();world.shooter.Fire(1);
            until=Time.realtimeSinceStartup+.2f;while(Time.realtimeSinceStartup<until)yield return null;
            Check(world.shooter.Carrying,"tracking-loss test carrying");
            body.linearVelocity=Vector3.forward*5;world.shooter.ReleaseTrackingLost();
            Check(!world.shooter.Active && body.linearVelocity.sqrMagnitude<.001f,"tracking loss drops without throw");
            UnityEngine.Object.Destroy(crate);
            world.modes.ShowMenu();yield return null;
            var menu=GameObject.Find("Hand menu");
            Check(menu.transform.Find("Menu page").childCount==12,"main page has six buttons and labels");
            var settings=menu.transform.Find("Menu page/SETTINGS");
            Ray ray=new Ray(settings.position-world.input.head.forward*.4f,world.input.head.forward);
            until=Time.realtimeSinceStartup+1.15f;
            while(Time.realtimeSinceStartup<until){world.modes.Point(1,ray,false);yield return null;}
            Check(menu.transform.Find("Menu page/COMFORT")!=null,"one-second dwell opens settings");
            var comfort=menu.transform.Find("Menu page/COMFORT");
            ray=new Ray(comfort.position-world.input.head.forward*.4f,world.input.head.forward);
            bool before=world.modes.ComfortEnabled;
            until=Time.realtimeSinceStartup+.7f;
            while(Time.realtimeSinceStartup<until){world.modes.Point(1,ray,false);yield return null;}
            Check(world.modes.ComfortEnabled==before,"no selection before one second");
            world.modes.ClearPointer(1);
            until=Time.realtimeSinceStartup+.4f;
            while(Time.realtimeSinceStartup<until){world.modes.Point(1,ray,false);yield return null;}
            Check(world.modes.ComfortEnabled==before,"tracking loss resets dwell");
            world.modes.ClearPointer(1);
            until=Time.realtimeSinceStartup+1.2f;
            while(Time.realtimeSinceStartup<until){world.modes.Point(1,ray,false);yield return null;}
            Check(world.modes.ComfortEnabled!=before,"complete dwell selects once");
            until=Time.realtimeSinceStartup+1.2f;
            while(Time.realtimeSinceStartup<until){world.modes.Point(1,ray,false);yield return null;}
            Check(world.modes.ComfortEnabled!=before,"continued pointing does not repeatedly toggle");
            world.modes.ClearPointer(1);world.modes.HideMenu();world.modes.SelectMode(false);world.motor.ResetPlayer();
            world.motor.enabled=false;yield return null;
            Preview(world.input.head.GetComponent<Camera>(),"VR-city-1.4.png");
            world.modes.ShowMenu();yield return null;Preview(world.input.head.GetComponent<Camera>(),"Menu-1.4.png");
            world.modes.SelectMode(true);yield return null;
            Check(UnityEngine.Object.FindFirstObjectByType<RoomToy>()!=null,"MR chandelier initialized");
            var toy=UnityEngine.Object.FindFirstObjectByType<RoomToy>();
            var camera=world.input.head.GetComponent<Camera>();
            camera.transform.position=toy.transform.position+new Vector3(0,.05f,-1.3f);camera.transform.LookAt(toy.transform.position);
            Preview(camera,"MR-chandelier-1.4.png");
            var beginner=BeginnerChecks.Run(world);while(beginner.MoveNext())yield return null;
            Preview(camera,"VR-training-1.5.png");
            var swing=SwingChecks.Run(world);while(swing.MoveNext())yield return null;
            var roomPlay=RoomPlayChecks.Run(world);while(roomPlay.MoveNext())yield return null;
            var roomFun=RoomFunChecks.Run(world);while(roomFun.MoveNext())yield return null;
            var looseWeb=LooseWebChecks.Run(world);while(looseWeb.MoveNext())yield return null;
            var cityDistrict=CityDistrictChecks.Run(world);while(cityDistrict.MoveNext())yield return null;
            var wallLaunch=WallLaunchChecks.Run(world);while(wallLaunch.MoveNext())yield return null;
            var buildingSwing=BuildingSwingChecks.Run(world);while(buildingSwing.MoveNext())yield return null;
        }
        public static void Preview(Camera camera,string name)
        {
            var rt=new RenderTexture(1280,800,24);var old=camera.targetTexture;var active=RenderTexture.active;
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var texture=new Texture2D(1280,800,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,800),0,0);texture.Apply();
            Directory.CreateDirectory("Builds/Previews");
            File.WriteAllBytes(Path.Combine("Builds/Previews",name),texture.EncodeToPNG());
            camera.targetTexture=old;RenderTexture.active=active;UnityEngine.Object.Destroy(texture);rt.Release();UnityEngine.Object.Destroy(rt);
        }
    }
}
