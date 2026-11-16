using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace WebSwing.Editor
{
    public static class WallLaunchChecks
    {
        static readonly List<string> results=new List<string>();
        static void Check(bool ok,string name){if(!ok)throw new Exception(name);results.Add("PASS: "+name);}
        public static IEnumerator Run(DemoWorld world)
        {
            results.Clear();
            Check(InteractionMotion.ClimbTarget(Vector3.up*.8f,Vector3.down*.8f,Vector3.forward)>2,"vertical alternating arm strokes climb without forward arm movement");
            Check(InteractionMotion.ClimbTarget(Vector3.zero,Vector3.zero,Vector3.forward)==0,"stationary hands do not climb");
            Check(InteractionMotion.ClimbTarget(Vector3.up,Vector3.up,Vector3.forward)==0,"moving both hands together does not count as walking");
            var gesture=new PullLaunchGesture();float strength;bool triggered=false;
            for(int i=0;i<60;i++)triggered|=gesture.Step(Vector3.back*i*.004f,Vector3.forward*10,true,.02f,out strength);
            Check(!triggered,"slow reeling and hand drift cannot launch");
            gesture.Reset();triggered=false;
            for(int i=0;i<8;i++)triggered|=gesture.Step(Vector3.back*i*.035f,Vector3.forward*10,true,.02f,out strength);
            Check(triggered,"deliberate fast eighteen centimetre pull launches");
            gesture.Reset();gesture.Step(Vector3.zero,Vector3.forward*10,true,.02f,out strength);
            Check(!gesture.Step(Vector3.back*.4f,Vector3.forward*10,true,.02f,out strength),"tracking position jump rejected");
            gesture.Reset();triggered=false;
            for(int i=0;i<8;i++)triggered|=gesture.Step(Vector3.back*i*.035f,Vector3.forward*10,false,.02f,out strength);
            Check(!triggered,"open hand never triggers pull launch");
            world.input.enabled=false;world.input.ReleaseAll();world.motor.enabled=true;world.modes.SelectMode(false);world.motor.SetComfort(false);
            world.input.head.localPosition=new Vector3(0,1.65f,0);world.input.head.localRotation=Quaternion.identity;
            var building=GameObject.CreatePrimitive(PrimitiveType.Cube);building.name="Wall climb and roof fixture";building.layer=8;
            building.transform.position=new Vector3(700,2.5f,2.7f);building.transform.localScale=new Vector3(8,5,4);
            Physics.SyncTransforms();world.motor.SetSpawn(new Vector3(700,1,0));world.motor.SetWallWalkEnabled(true);world.motor.SetArmWalkSpeed(3);
            float until=Time.fixedTime+.65f;while(Time.fixedTime<until)yield return null;
            Check(world.motor.WallAttached&&world.motor.transform.position.y>1.4f,"near wall attaches and arm motion climbs faster");
            world.motor.SetArmWalkSpeed(0);until=Time.fixedTime+.6f;while(Time.fixedTime<until)yield return null;
            float stopY=world.motor.transform.position.y;until=Time.fixedTime+.3f;while(Time.fixedTime<until)yield return null;
            Check(world.motor.WallAttached&&Mathf.Abs(world.motor.transform.position.y-stopY)<.03f,"stopping arms holds position on wall");
            world.motor.SetWallWalkEnabled(false);until=Time.fixedTime+.35f;while(Time.fixedTime<until)yield return null;
            Check(world.motor.transform.position.y<stopY-.25f,"disabling wall mode restores gravity");
            world.motor.SetSpawn(new Vector3(700,1,0));world.motor.SetWallWalkEnabled(true);world.motor.SetArmWalkSpeed(3);
            until=Time.fixedTime+5;while(Time.fixedTime<until&&(world.motor.transform.position.z<.8f||world.motor.Mantling))yield return null;
            Check(world.motor.transform.position.y>=5&&world.motor.transform.position.z>.8f&&!world.motor.Mantling,"climbing past edge smoothly mounts rooftop; position="+world.motor.transform.position+" mantle="+world.motor.Mantling);
            world.motor.SetArmWalkSpeed(0);until=Time.fixedTime+.5f;while(Time.fixedTime<until)yield return null;
            Check(world.motor.transform.position.y>=4.95f&&Vector3.Dot(world.input.head.up,Vector3.up)>.999f,"rooftop supports body with upright view");
            world.motor.SetSpawn(new Vector3(900,20,0));world.motor.SetWallWalkEnabled(false);
            var anchor=GameObject.CreatePrimitive(PrimitiveType.Cube);anchor.layer=8;anchor.transform.position=new Vector3(900,30,20);anchor.transform.localScale=Vector3.one*2;
            world.shooter.hand.position=world.motor.BodyPoint+Vector3.forward*.2f;world.shooter.hand.rotation=Quaternion.LookRotation(anchor.transform.position-world.shooter.hand.position);Physics.SyncTransforms();
            Check(world.shooter.Fire(1),"launch fixture web attaches");world.motor.SetGrip(1,true);
            Vector3 startHand=world.shooter.hand.localPosition,axis=world.motor.transform.InverseTransformDirection(world.motor.GetAnchor(1)-world.shooter.hand.position).normalized;
            float next=Time.fixedTime;for(int i=0;i<8&&world.shooter.Active;i++)
            {
                world.shooter.hand.localPosition=startHand-axis*(i*.035f);next=Time.fixedTime;
                while(Time.fixedTime<=next)yield return null;
            }
            Check(!world.shooter.Active&&!world.motor.Attached&&world.motor.Velocity.magnitude>10,"hand pull automatically releases web and launches body");
            Vector3 launchPosition=world.motor.transform.position;until=Time.fixedTime+.25f;while(Time.fixedTime<until)yield return null;
            Check(world.motor.transform.position.z>launchPosition.z+1.5f,"launch continues flying after automatic release");
            var visual=world.shooter.GetComponent<WebRopeVisual>();visual.Draw(world.shooter.hand.position,world.shooter.hand.position+Vector3.forward*4,Vector3.back,.8f,1);
            Check(visual.VisualRoot.GetComponentsInChildren<LineRenderer>().Length==3&&visual.VisualRoot.GetComponentInChildren<MeshFilter>().sharedMesh.vertexCount==876,"silk core and fine fibers use four renderers per hand");
            InteractionChecks.Preview(world.input.head.GetComponent<Camera>(),"VR-launch-silk-1.13.png");visual.Hide();
            UnityEngine.Object.Destroy(anchor);UnityEngine.Object.Destroy(building);world.input.ReleaseAll();world.motor.SetSpawn(new Vector3(0,5.6f,-2));world.input.head.localRotation=Quaternion.identity;world.motor.SetWallWalkEnabled(true);
            world.modes.SelectMode(true);Check(!world.motor.WallWalkEnabled&&!world.motor.Mantling,"MR disables wall locomotion");world.modes.SelectMode(false);
            File.WriteAllLines("ProjectSettings/WallLaunchCheck.txt",results);Debug.Log("WEB_WALL_LAUNCH_CHECK_OK: "+results.Count+" checks");
        }
    }
}
