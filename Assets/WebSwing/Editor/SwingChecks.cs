using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace WebSwing.Editor
{
    public static class SwingChecks
    {
        static readonly List<string> results=new List<string>();
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);results.Add("PASS: "+message);}
        public static IEnumerator Run(DemoWorld world)
        {
            results.Clear();
            Check(WebPhysics.ReelDistanceFactor(5)==0&&WebPhysics.ReelDistanceFactor(12)==0,"nearby reels retain normal speed");
            Check(Mathf.Abs(WebPhysics.ReelDistanceFactor(36)-.5f)<.001f&&WebPhysics.ReelDistanceFactor(90)==1,"distance smoothly doubles far reeling speed with a cap");
            float lastFactor=0;bool monotonic=true;for(int d=0;d<=100;d++){float factor=WebPhysics.ReelDistanceFactor(d);monotonic&=factor>=lastFactor&&factor<=1;lastFactor=factor;}Check(monotonic,"reel speed smoothly decreases as the player approaches");
            Check(WebActionSound.ShotClip&&WebActionSound.ShotClip.length>.1f&&WebActionSound.BreakClip.length>.2f,"action sound clips generated and nonempty");
            Vector3 pendulumVelocity=new Vector3(3,2,4);
            Vector3 pendulumPoint=WebPhysics.ConstrainPendulum(Vector3.right*2,Vector3.zero,5,ref pendulumVelocity);
            Check(Mathf.Abs(pendulumPoint.magnitude-5)<.001f&&Mathf.Abs(Vector3.Dot(pendulumVelocity,pendulumPoint.normalized))<.001f,"pendulum constraint keeps full radius even during inward motion");
            Vector3 radialVelocity=Vector3.right*4;
            WebPhysics.ConstrainPendulum(Vector3.right*3,Vector3.zero,3,ref radialVelocity,2);
            Check(Vector3.Distance(radialVelocity,Vector3.left*2)<.001f,"reeling changes radius without introducing extra radial flight");
            Vector3 wristReference=new Vector3(1,2,3),index=wristReference+new Vector3(-.03f,0,.08f),little=wristReference+new Vector3(.03f,0,.08f);
            Vector3 expected=Vector3.forward;
            Check(HandAim.TryForwardRay(wristReference,index,little,Vector3.zero,Quaternion.identity,out var ray),"knuckle axis produces valid forward aim");
            Check(Vector3.Dot(ray.direction,expected)>.99999f,"aim stays forward independent of curled fingertips");
            Check(Vector3.Distance(ray.origin,wristReference+expected*.012f)<.0001f,"launch origin stays at wrist skin");
            Quaternion originRotation=Quaternion.Euler(0,71,0);
            HandAim.TryForwardRay(wristReference,index,little,new Vector3(5,4,3),originRotation,out var rotated);
            Check(Vector3.Dot(rotated.direction,originRotation*expected)>.99999f,"forward ray follows XR origin rotation");
            Check(!HandAim.TryForwardRay(wristReference,wristReference,wristReference,Vector3.zero,Quaternion.identity,out _),"collapsed hand axis rejected");
            Check(InteractionMotion.WebPose(1,.4f,.4f,1),"curl gesture remains a separate firing trigger");
            Check(Vector3.Dot(HandAim.RayRotation(Vector3.up)*Vector3.forward,Vector3.up)>.99999f,"vertical forward aim keeps valid rotation");
            Check(HandAim.ColorForHand(0)!=HandAim.ColorForHand(1)&&HandAim.ReticleSize(3)>=.14f&&HandAim.ReticleSize(50)>=.6f,"larger distinct left/right reticles");
            Check(InteractionMotion.WalkTarget(Vector3.forward*.8f,Vector3.back*.8f,Vector3.forward)>4.8f,"normal arm swings support faster walking");
            world.input.enabled=false;world.motor.enabled=false;world.modes.SelectMode(true);
            var block=GameObject.CreatePrimitive(PrimitiveType.Cube);block.layer=8;block.transform.position=new Vector3(500,10,6);
            var wrist=new Vector3(500,10,0);
            HandAim.TryForwardRay(wrist,wrist+new Vector3(-.03f,0,.08f),wrist+new Vector3(.03f,0,.08f),Vector3.zero,Quaternion.identity,out ray);
            world.shooter.hand.SetPositionAndRotation(ray.origin,Quaternion.LookRotation(ray.direction));Physics.SyncTransforms();
            Check(world.shooter.Fire(1),"hand forward ray attaches exact forward surface");
            Check(Vector3.Dot(world.shooter.LastShotRay.direction,ray.direction)>.99999f&&Vector3.Distance(world.shooter.LastShotRay.origin,ray.origin)<.0001f,"visible aim and launch share same ray");
            world.shooter.Release();UnityEngine.Object.Destroy(block);
            world.shooter.AimTracked=false;Check(!world.shooter.Fire(1),"invalid hand axis cannot fire using stale direction");world.shooter.AimTracked=true;
            world.modes.SelectMode(false);world.motor.enabled=true;world.motor.SetComfort(false);world.motor.SetSpawn(new Vector3(500,18,0));
            world.input.head.localRotation=Quaternion.identity;
            world.shooter.hand.position=world.motor.BodyPoint+Vector3.forward*.2f;
            world.leftShooter.hand.position=world.motor.BodyPoint-Vector3.right*.2f;
            world.motor.Attach(world.motor.BodyPoint+new Vector3(0,30,80),1);world.motor.SetGrip(1,true);
            float farLength=world.motor.GetLength(1),farUntil=Time.fixedTime+1.6f;
            while(Time.fixedTime<farUntil)yield return null;
            Check(farLength-world.motor.GetLength(1)>6.4f,"far anchor reels faster than the old fixed four metre cap");
            world.motor.ResetPlayer();
            Vector3 start=world.motor.transform.position;
            world.motor.Attach(new Vector3(500,27,13),1);float length=world.motor.GetLength(1);world.motor.SetGrip(1,true);
            Vector3 swingAnchor=world.motor.GetAnchor(1),initialRadial=world.motor.BodyPoint-swingAnchor;
            float until=Time.realtimeSinceStartup+1.8f,maxSpeed=0;
            while(Time.realtimeSinceStartup<until){maxSpeed=Mathf.Max(maxSpeed,world.motor.Velocity.magnitude);yield return null;}
            Check(world.motor.GetLength(1)<length-2,"closed fist reels rope");
            Check(Vector3.Distance(world.motor.BodyPoint,swingAnchor)<=world.motor.GetLength(1)+.08f&&Vector3.Angle(initialRadial,world.motor.BodyPoint-swingAnchor)>25,"swing follows an arc around the fixed attachment");
            Check(Vector3.ProjectOnPlane(world.motor.Velocity,world.motor.BodyPoint-swingAnchor).magnitude>6,"swing builds strong tangential motion instead of radial-only pulling");
            world.shooter.RefreshVisuals();bool arrow=false;
            foreach(var line in world.shooter.GetComponentsInChildren<LineRenderer>())if(line.name=="Throw trajectory preview")arrow|=line.enabled;
            Check(!arrow,"VR swing shows no pull arrow");
            Check(world.motor.transform.position.z>start.z+3&&maxSpeed>4,"gravity and rope generate forward pendulum motion");
            Vector3 before=world.motor.Velocity;float releaseZ=world.motor.transform.position.z;
            world.input.StepTrackedGesture(1,false,true,Vector3.zero,.13f);
            Check(!world.motor.Attached&&Vector3.Distance(before,world.motor.Velocity)<.001f,"open palm promptly detaches and preserves velocity");
            until=Time.realtimeSinceStartup+.25f;while(Time.realtimeSinceStartup<until)yield return null;
            Check(world.motor.transform.position.z>releaseZ+.5f,"released player keeps flying forward");
            Check(Vector3.Dot(world.input.head.up,Vector3.up)>.999f,"swing camera stays upright");
            world.motor.ResetPlayer();world.shooter.hand.position=world.motor.BodyPoint;world.leftShooter.hand.position=world.motor.BodyPoint;
            world.motor.Attach(new Vector3(495,30,12),0);world.motor.Attach(new Vector3(505,30,12),1);world.motor.SetGrip(0,true);world.motor.SetGrip(1,true);
            until=Time.realtimeSinceStartup+.25f;while(Time.realtimeSinceStartup<until)yield return null;
            world.input.StepTrackedGesture(1,false,true,Vector3.zero,.13f);
            Check(!world.motor.IsAttached(1)&&world.motor.IsAttached(0),"right palm release leaves left tether intact");
            world.input.ReleaseAll();world.motor.enabled=false;
            for(int kind=1;kind<=3;kind++)
            {
                var prop=PullableProps.Spawn(kind,new Vector3(700,8,kind*5),kind==1?4:1);yield return null;
                var rb=prop.GetComponent<Rigidbody>();world.shooter.hand.SetPositionAndRotation(rb.worldCenterOfMass-Vector3.forward*2,Quaternion.identity);Physics.SyncTransforms();
                Check(world.shooter.Fire(1)&&world.shooter.PullingObject,"non-wood prop "+kind+" is web-pullable");
                Check(prop.GetComponent<CargoReset>()!=null,"non-wood prop "+kind+" supports return");
                if(kind==2)Check(prop.GetComponent<Collider>().sharedMaterial.bounciness>.5f,"ball has bouncy material");
                world.shooter.Release();UnityEngine.Object.Destroy(prop);
            }
            world.motor.SetSpawn(new Vector3(0,5.6f,-2));world.input.head.localRotation=Quaternion.identity;
            world.shooter.hand.position=world.input.head.position+Vector3.right*.3f-Vector3.up*.2f;
            world.leftShooter.hand.position=world.input.head.position-Vector3.right*.3f-Vector3.up*.2f;
            world.shooter.hand.rotation=Quaternion.LookRotation(new Vector3(19,16,22)-world.shooter.hand.position);
            world.leftShooter.hand.rotation=Quaternion.LookRotation(new Vector3(-19,16,22)-world.leftShooter.hand.position);
            world.shooter.AimTracked=world.leftShooter.AimTracked=true;yield return null;
            world.shooter.RefreshVisuals();world.leftShooter.RefreshVisuals();
            InteractionChecks.Preview(world.input.head.GetComponent<Camera>(),"VR-reticles-1.6.png");
            var crowd=UnityEngine.Object.FindFirstObjectByType<PedestrianCrowd>();
            Check(crowd!=null&&crowd.Count==6,"six sidewalk pedestrians present");
            Vector3 citizen=crowd.Position(0);until=Time.realtimeSinceStartup+.5f;while(Time.realtimeSinceStartup<until)yield return null;
            Check(Vector3.Distance(citizen,crowd.Position(0))>.1f&&Mathf.Abs(crowd.Position(0).x)>10&&Vector3.Dot(crowd.Forward(0),(crowd.Position(0)-citizen).normalized)>.5f,"pedestrians move forward along sidewalk");
            File.WriteAllLines("ProjectSettings/SwingCheck.txt",results);Debug.Log("WEB_SWING_CHECK_OK: "+results.Count+" checks");
        }
    }
}
