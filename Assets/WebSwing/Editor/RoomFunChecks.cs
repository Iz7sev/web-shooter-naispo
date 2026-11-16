using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace WebSwing.Editor
{
    public static class RoomFunChecks
    {
        static readonly List<string> results=new List<string>();
        static void Check(bool ok,string label){if(!ok)throw new Exception(label);results.Add("PASS: "+label);}
        public static IEnumerator Run(DemoWorld world)
        {
            results.Clear();world.input.enabled=false;world.input.ReleaseAll();world.motor.enabled=false;
            world.motor.SetSpawn(Vector3.zero);world.input.head.localPosition=new Vector3(0,1.6f,0);world.input.head.localRotation=Quaternion.identity;
            world.modes.SelectMode(true);yield return null;
            Check(UnityEngine.Object.FindObjectsByType<MRBreakable>(FindObjectsSortMode.None).Length==3,"MR has three breakable vases");
            Check(RoomRopeGrab.NearSegment(new Vector3(.08f,0,.5f),Vector3.zero,Vector3.forward*3),"palm near reachable rope can grab");
            Check(!RoomRopeGrab.NearSegment(new Vector3(.3f,0,.5f),Vector3.zero,Vector3.forward*3),"distant hand cannot grab rope");
            Check(!RoomRopeGrab.NearSegment(Vector3.zero,Vector3.zero,Vector3.forward*3),"own shooting wrist cannot grab rope endpoint");
            var testRoot=new GameObject("MR fun check area");
            var cargo=GameObject.CreatePrimitive(PrimitiveType.Cube);cargo.transform.SetParent(testRoot.transform,false);cargo.layer=8;
            cargo.transform.position=new Vector3(950,10,3);cargo.transform.localScale=Vector3.one*.4f;
            var body=cargo.AddComponent<Rigidbody>();body.mass=2;body.useGravity=false;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            cargo.AddComponent<CargoReset>().spawn=cargo.transform.position;
            world.shooter.LiftAssist=false;world.shooter.hand.SetPositionAndRotation(new Vector3(950,10,0),Quaternion.identity);Physics.SyncTransforms();
            Check(world.shooter.Fire(1),"right web attaches test cargo");
            var grab=world.input.RopeGrab;Vector3 palm=new Vector3(950.04f,10,.5f);
            grab.SetHand(0,true,false,palm);grab.Step(.02f);grab.SetHand(0,true,true,palm);grab.Step(.02f);
            Check(grab.IsHolding(0)&&world.shooter.RopeHeld,"left fist holds right-hand web");
            float before=world.shooter.ObjectRopeLength;
            for(int i=0;i<5;i++){palm+=Vector3.back*.04f;grab.SetHand(0,true,true,palm);grab.Step(.02f);}
            Check(world.shooter.ObjectRopeLength<before-.2f,"second hand pulling shortens object rope");
            world.shooter.RefreshVisuals();
            bool bend=false;foreach(var line in world.shooter.GetComponentsInChildren<LineRenderer>())
                if(line.name=="Fine silk winding"&&line.enabled&&Vector3.Distance(line.GetPosition((line.positionCount-1)/2),palm)<.025f)bend=true;
            // Let the firing animation finish before checking its held midpoint.
            float until=Time.realtimeSinceStartup+.15f;while(Time.realtimeSinceStartup<until){grab.Step(Time.unscaledDeltaTime);yield return null;}
            world.shooter.RefreshVisuals();bend=false;
            foreach(var line in world.shooter.GetComponentsInChildren<LineRenderer>())if(line.name=="Fine silk winding"&&line.enabled&&Vector3.Distance(line.GetPosition((line.positionCount-1)/2),palm)<.025f)bend=true;
            Check(bend,"rendered web passes through grabbing palm");
            grab.SetHand(0,true,false,palm);grab.Step(.02f);
            Check(!grab.IsHolding(0)&&!world.shooter.RopeHeld&&world.shooter.Active,"opening helper hand releases grip but keeps web");
            grab.SetHand(0,true,true,palm);grab.Step(.02f);grab.SetHand(0,false,false,palm);
            Check(!grab.IsHolding(0)&&!world.shooter.RopeHeld,"tracking loss releases rope grip immediately");
            world.shooter.Release();world.shooter.LiftAssist=true;grab.ResetGrabs();
            var vase=MRBreakable.Create(testRoot.transform,new Vector3(960,10,3),Color.cyan);
            Check(!vase.TryImpact(body,Vector3.forward*.5f)&&!vase.Broken,"gentle contact does not break vase");
            Check(vase.TryImpact(body,Vector3.forward*3)&&vase.Broken,"cargo impact breaks vase");
            Check(vase.GetComponentsInChildren<Rigidbody>().Length==9,"broken vase creates only eight physics shards");
            vase.Restore();Check(!vase.Broken&&vase.GetComponent<Collider>().enabled,"recall restores intact target collision");
            // Verify the real collision callback, not just the impact threshold helper.
            body.position=new Vector3(960,10,1.5f);body.linearVelocity=Vector3.zero;Physics.SyncTransforms();
            until=Time.realtimeSinceStartup+.45f;while(Time.realtimeSinceStartup<until)yield return null;
            body.linearVelocity=Vector3.forward*5;
            until=Time.realtimeSinceStartup+1.5f;while(Time.realtimeSinceStartup<until&&!vase.Broken)yield return null;
            Check(vase.Broken,"real thrown cargo collision shatters target");
            world.modes.RecallObjects();Check(!vase.Broken,"menu recall rebuilds broken vases");
            vase.TryImpact(body,Vector3.forward*3);world.modes.SelectMode(false);
            var roomVases=UnityEngine.Object.FindObjectsByType<MRBreakable>(FindObjectsSortMode.None);
            Check(roomVases.Length==1,"room targets and debris hidden when leaving MR");
            UnityEngine.Object.Destroy(testRoot);yield return null;
            world.modes.SelectMode(true);yield return null;
            InteractionChecks.Preview(world.input.head.GetComponent<Camera>(),"MR-grab-break-1.8.png");
            File.WriteAllLines("ProjectSettings/RoomFunCheck.txt",results);Debug.Log("WEB_ROOM_FUN_CHECK_OK: "+results.Count+" checks");
        }
    }
}
