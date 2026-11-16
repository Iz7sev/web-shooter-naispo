using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace WebSwing.Editor
{
    public static class RoomPlayChecks
    {
        static readonly List<string> results=new List<string>();
        static void Check(bool ok,string name){if(!ok)throw new Exception(name);results.Add("PASS: "+name);}
        public static IEnumerator Run(DemoWorld world)
        {
            results.Clear();world.input.enabled=false;world.motor.enabled=false;
            world.motor.SetSpawn(new Vector3(0,0,0));world.input.head.localPosition=new Vector3(0,1.6f,0);world.input.head.localRotation=Quaternion.identity;
            world.modes.SelectMode(true);yield return null;
            var chair=GameObject.Find("MR pullable chair");var door=GameObject.Find("MR hinged door");
            Check(chair&&chair.GetComponentsInChildren<Collider>().Length>=7,"MR chair has compound physical shape");
            Check(door&&door.GetComponent<HingeJoint>(),"MR door has a fixed hinge");
            var marks=UnityEngine.Object.FindFirstObjectByType<RoomWebMarks>();
            Check(marks!=null,"MR surface web manager active");
            var surface=GameObject.CreatePrimitive(PrimitiveType.Cube);surface.layer=9;surface.transform.position=new Vector3(900,2,4);
            world.shooter.hand.SetPositionAndRotation(new Vector3(900,2,0),Quaternion.identity);Physics.SyncTransforms();
            Check(world.shooter.Fire(1)&&world.shooter.RoomSurfaceWeb,"MR static shot creates a reusable surface tether");
            Check(marks.Count==1,"shot leaves one persistent spiderweb");
            world.shooter.Release();Check(marks.Count==1,"web patch survives tether release");
            for(int i=0;i<20;i++)RoomWebMarks.Add(new Vector3(900+i*.02f,2,3.5f),Vector3.back,null);
            yield return null;Check(marks.Count==16,"persistent webs capped at sixteen");
            bool webColliders=false;foreach(var line in marks.GetComponentsInChildren<LineRenderer>())if(line.name=="Surface spiderweb")webColliders|=line.GetComponent<Collider>()!=null;
            Check(!webColliders,"web decoration adds no collision geometry");
            UnityEngine.Object.Destroy(surface);
            // Isolate mechanics from the furniture layout and room floor.
            var testRoot=new GameObject("Furniture check area");
            var testChair=MRFurniture.Chair(testRoot.transform,new Vector3(800,20,3),Quaternion.Euler(0,30,0));
            var rb=testChair.GetComponent<Rigidbody>();rb.useGravity=false;
            Physics.SyncTransforms();world.shooter.hand.SetPositionAndRotation(testChair.transform.TransformPoint(new Vector3(0,.48f,0))-Vector3.forward*1.2f,Quaternion.identity);Physics.SyncTransforms();
            Check(world.shooter.Fire(1)&&world.shooter.PullingObject,"chair accepts an exact web shot");
            for(int i=0;i<30&&!world.shooter.Carrying;i++)
            {
                world.shooter.hand.position=Vector3.MoveTowards(world.shooter.hand.position,rb.worldCenterOfMass,.08f);
                yield return new WaitForFixedUpdate();
            }
            Check(world.shooter.Carrying,"nearby chair can be lifted");world.shooter.RefreshVisuals();
            world.shooter.Release();var reset=testChair.GetComponent<CargoReset>();reset.Recall();
            Check(Quaternion.Angle(rb.rotation,reset.spawnRotation)<.1f,"recall restores furniture rotation");
            var leaf=MRFurniture.Door(testRoot.transform,new Vector3(810,20,3),Quaternion.identity);var body=leaf.GetComponent<Rigidbody>();
            world.shooter.hand.SetPositionAndRotation(leaf.transform.position+new Vector3(.25f,0,-.5f),Quaternion.identity);Physics.SyncTransforms();
            Check(world.shooter.Fire(1),"hinged door accepts web attachment");
            float doorUntil=Time.fixedTime+1f,lastDoorStep=Time.fixedTime;
            while(Time.fixedTime<doorUntil)
            {
                // This editor runner advances enumerators per render frame, not per yielded instruction.
                float physicsStep=Time.fixedTime-lastDoorStep;
                if(physicsStep>0){world.shooter.hand.position+=Vector3.back*physicsStep;lastDoorStep=Time.fixedTime;}
                yield return null;
            }
            Check(!world.shooter.Carrying,"jointed door is never converted to carried cargo");
            Check(Quaternion.Angle(body.rotation,Quaternion.identity)>2,"pull opens door around hinge");
            world.shooter.Release();leaf.GetComponent<CargoReset>().Recall();
            Check(Quaternion.Angle(body.rotation,Quaternion.identity)<.1f,"recall closes door");
            UnityEngine.Object.Destroy(testRoot);yield return null;
            world.input.head.localPosition=new Vector3(0,1.6f,0);world.input.head.localRotation=Quaternion.identity;
            world.modes.ResetPosition();yield return null;
            marks=UnityEngine.Object.FindFirstObjectByType<RoomWebMarks>();Check(marks.Count==0,"MR reset clears old web patches");
            RoomWebMarks.Add(new Vector3(-.5f,1,2.8f),Vector3.back,null);
            InteractionChecks.Preview(world.input.head.GetComponent<Camera>(),"MR-freeplay-1.7.png");
            world.modes.SelectMode(false);Check(!marks.gameObject.activeInHierarchy,"MR patches hidden in VR");
            File.WriteAllLines("ProjectSettings/RoomPlayCheck.txt",results);Debug.Log("WEB_ROOM_PLAY_CHECK_OK: "+results.Count+" checks");
        }
    }
}
