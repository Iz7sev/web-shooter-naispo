using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace WebSwing.Editor
{
    public static class LooseWebChecks
    {
        static readonly List<string> results=new List<string>();
        static void Check(bool ok,string name){if(!ok)throw new Exception(name);results.Add("PASS: "+name);}
        public static IEnumerator Run(DemoWorld world)
        {
            results.Clear();world.input.enabled=false;world.input.ReleaseAll();world.motor.enabled=false;world.modes.SelectMode(true);yield return null;
            var root=new GameObject("Loose web check area");
            var cargo=GameObject.CreatePrimitive(PrimitiveType.Cube);cargo.layer=8;cargo.transform.SetParent(root.transform,false);
            cargo.transform.position=new Vector3(1000,10,3);cargo.transform.localScale=Vector3.one*.4f;
            var body=cargo.AddComponent<Rigidbody>();body.mass=2;body.useGravity=false;cargo.AddComponent<CargoReset>().spawn=cargo.transform.position;
            world.shooter.LiftAssist=false;world.leftShooter.LiftAssist=false;
            world.shooter.hand.SetPositionAndRotation(new Vector3(1000,10,0),Quaternion.identity);Physics.SyncTransforms();
            Check(world.input.Shoot(1),"MR gesture routes to detached single shot");
            Check(!world.shooter.Active&&!world.leftShooter.Active&&RoomLooseWeb.Count==1,"single hit leaves both hands free and one loose thread");
            var loose=RoomLooseWeb.Aimed(world.shooter.AimRay);Check(loose!=null,"distant contact patch can be aimed at");
            Check(loose.GetComponent<WebRopeVisual>()==null,"unheld MR shot has no long rope renderer");
            Check(RoomLooseWeb.Aimed(new Ray(world.shooter.hand.position,Vector3.back))==null,"fist pointing away cannot grab a contact patch");
            var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.transform.position=new Vector3(1000,10,1.5f);Physics.SyncTransforms();
            Check(RoomLooseWeb.Aimed(world.shooter.AimRay)==null,"occluding geometry blocks remote patch pickup");
            blocker.GetComponent<Collider>().enabled=false;UnityEngine.Object.Destroy(blocker);Physics.SyncTransforms();
            Check(RoomLooseWeb.Closest(loose.EndPoint)!=null,"contact patch can also be grasped directly");
            Vector3 start=loose.EndPoint;world.shooter.hand.position+=Vector3.right*2;
            yield return null;Check(Vector3.Distance(loose.EndPoint,start)<.0001f,"contact patch follows the object instead of the firing hand");
            var grab=world.input.RopeGrab;Vector3 palm=new Vector3(1000,10,.4f);
            world.shooter.hand.position=palm;
            grab.SetHand(1,true,false,palm);grab.Step(.02f);grab.SetHand(1,true,true,palm);grab.Step(.02f);
            Check(grab.IsHolding(1)&&world.shooter.PullingObject&&RoomLooseWeb.Count==0,"same firing hand can later pick up its detached thread");
            float length=world.shooter.ObjectRopeLength,z=body.position.z,last=Time.fixedTime,until=Time.fixedTime+.6f;
            while(Time.fixedTime<until)
            {
                float step=Time.fixedTime-last;last=Time.fixedTime;
                palm+=Vector3.back*(step*.7f);world.shooter.hand.position=palm;
                grab.SetHand(1,true,true,palm);grab.Step(Time.unscaledDeltaTime);yield return null;
            }
            Check(world.shooter.ObjectRopeLength<length-.25f&&body.position.z<z-.05f,"grabbing and hand pulling moves the object toward the hand");
            grab.SetHand(1,true,false,palm);grab.Step(.02f);
            Check(!world.shooter.Active&&!grab.IsHolding(1)&&RoomLooseWeb.Count==1,"opening hand leaves thread in space for another grip");
            loose=RoomLooseWeb.Aimed(world.shooter.AimRay);Check(loose!=null,"released contact patch remains aimable");
            world.leftShooter.hand.SetPositionAndRotation(palm,Quaternion.LookRotation(loose.EndPoint-palm));
            grab.SetHand(0,true,false,palm);grab.Step(.02f);grab.SetHand(0,true,true,palm);grab.Step(.02f);
            Check(grab.IsHolding(0)&&world.leftShooter.PullingObject,"other hand can also pick up the loose thread");
            world.leftShooter.Release();grab.ResetGrabs();
            cargo.transform.position=new Vector3(1000,10,3);body.linearVelocity=Vector3.zero;
            world.shooter.hand.SetPositionAndRotation(new Vector3(1000,10,0),Quaternion.identity);Physics.SyncTransforms();
            Check(world.input.Shoot(1),"direct-pull shot hits target");
            world.shooter.hand.rotation=Quaternion.LookRotation(Vector3.right);
            grab.SetHand(1,true,true,world.shooter.hand.position);grab.Step(.02f);
            Check(grab.IsHolding(1)&&world.shooter.PullingObject,"first fist immediately pulls the fired target without re-aiming");
            grab.SetHand(1,true,false,world.shooter.hand.position);grab.Step(.02f);
            Check(!world.shooter.Active,"opening after direct pull leaves only contact patches");
            world.modes.ShowMenu();Check(RoomLooseWeb.Count>=1&&!world.leftShooter.Active,"opening menu preserves a gripped thread as a loose thread");world.modes.HideMenu();
            for(int i=0;i<8;i++)RoomLooseWeb.Create(new Vector3(1010+i,10,0),null,new Vector3(1010+i,10,3),Vector3.back,Color.cyan);
            Check(RoomLooseWeb.Count==6,"loose threads capped at six");
            world.modes.SelectMode(false);Check(RoomLooseWeb.Count==0,"loose threads hidden and unavailable outside MR");
            world.modes.SelectMode(true);yield return null;Check(RoomLooseWeb.Count==0,"new MR layout starts with no old loose threads");
            UnityEngine.Object.Destroy(root);world.input.ReleaseAll();world.shooter.LiftAssist=world.leftShooter.LiftAssist=true;
            File.WriteAllLines("ProjectSettings/LooseWebCheck.txt",results);Debug.Log("WEB_LOOSE_WEB_CHECK_OK: "+results.Count+" checks");
        }
    }
}
