using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WebSwing.Editor
{
    public static class ExhibitionChecks
    {
        [MenuItem("WebSwing/Preview Exhibition Checks")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying) throw new Exception("Enter Play mode first.");
            var world=UnityEngine.Object.FindFirstObjectByType<DemoWorld>();
            if(!world) throw new Exception("No world");
            world.input.enabled=false; world.input.ReleaseAll(); world.modes.SelectMode(false);
            world.motor.SetComfort(true);
            var hand=world.input.aim;
            var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube); obstacle.layer=8;
            obstacle.transform.position=new Vector3(500,5,0);
            hand.SetPositionAndRotation(new Vector3(500,5,-3),Quaternion.identity);
            Physics.SyncTransforms();
            world.input.ToggleShot(1);
            bool still=world.shooter.Active&&world.motor.IsAttached(1);
            world.input.ToggleShot(1); bool tap=still&&!world.shooter.Active;
            UnityEngine.Object.Destroy(obstacle);
            Rigidbody cargo=null;
            foreach(var reset in UnityEngine.Object.FindObjectsByType<CargoReset>(FindObjectsSortMode.None))
                if(reset.spawn.y>5.5f&&reset.name=="Cargo crate / 2 kg") cargo=reset.GetComponent<Rigidbody>();
            if(!cargo) throw new Exception("No roof cargo");
            float initialY=cargo.position.y;
            Vector3 initialHand=cargo.worldCenterOfMass+Vector3.back*.6f+Vector3.up*1.4f;
            hand.SetPositionAndRotation(initialHand,Quaternion.LookRotation(cargo.worldCenterOfMass-initialHand));
            bool attached=world.shooter.Fire(1);
            var packet=WebHaptics.Packet(7,3,100,1);
            bool protocol=WebHaptics.Valid(packet); packet[4]^=1; protocol&=!WebHaptics.Valid(packet);
            double start=EditorApplication.timeSinceStartup;
            bool lifted=false,released=false; float releaseY=0;
            EditorApplication.CallbackFunction tick=null;
            tick=()=>
            {
                if(!EditorApplication.isPlaying||!world) { EditorApplication.update-=tick; return; }
                double t=EditorApplication.timeSinceStartup-start;
                hand.position=initialHand+Vector3.up*(Mathf.Clamp01((float)t)*.65f);
                if(t<1.6) return;
                if(!released) { released=true; lifted=attached&&world.shooter.Carrying&&cargo.position.y>initialY+.3f; releaseY=cargo.position.y; world.shooter.Release(); }
                if(t<2.1) return;
                bool falls=cargo.position.y<releaseY-.15f;
                bool pose=InteractionMotion.WebPose(.78f,.65f,.64f,.75f)&&!InteractionMotion.WebPose(.9f,.9f,.9f,.9f);
                string result=(tap&&lifted&&falls&&pose&&protocol?"PASS":"FAIL")+": manual static attachment="+still+"; desktop tap toggle="+tap+"; lift="+lifted+"; release gravity="+falls+"; relaxed pose="+pose+"; CRC="+protocol;
                File.WriteAllText("ProjectSettings/ExhibitionCheck.txt",result);
                world.modes.ResetPosition(); world.input.enabled=true; world.modes.ShowMenu();
                EditorApplication.update-=tick;
                if(result.StartsWith("PASS")) Debug.Log("WEB_EXHIBITION_CHECK_OK: "+result); else Debug.LogError(result);
            };
            EditorApplication.update+=tick;
        }
    }
}
