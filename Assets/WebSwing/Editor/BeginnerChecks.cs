using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace WebSwing.Editor
{
    public static class BeginnerChecks
    {
        static readonly List<string> results=new List<string>();
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);results.Add("PASS: "+message);}
        public static IEnumerator Run(DemoWorld world)
        {
            results.Clear();
            Check(ExperienceMotion.BallisticVelocity(new Vector3(0,1.2f,0),new Vector3(0,1.9f,2.2f),5.5f,out var v),"reachable ballistic target");
            float flight=2.2f/v.z;Vector3 end=new Vector3(0,1.2f,0)+v*flight+Physics.gravity*.5f*flight*flight;
            Check(Vector3.Distance(end,new Vector3(0,1.9f,2.2f))<.005f&&Mathf.Abs(v.magnitude-5.5f)<.001f,"fixed-speed trajectory reaches aim point");
            Check(!ExperienceMotion.BallisticVelocity(Vector3.zero,Vector3.forward*50,5.5f,out v),"unreachable target handled");
            var camera=world.input.head.GetComponent<Camera>();camera.transform.localPosition=new Vector3(0,1.65f,0);camera.transform.localRotation=Quaternion.identity;
            world.modes.SelectMode(true);world.input.enabled=false;yield return null;
            var toy=UnityEngine.Object.FindFirstObjectByType<RoomToy>();camera.transform.LookAt(toy.transform.position);
            var cargo=CityEnvironment.SpawnCrate(camera.transform.position+camera.transform.forward*.8f-Vector3.up*.2f,2,98);
            cargo.transform.SetParent(toy.transform.parent,true); // MR cargo must leave the inactive VR city root.
            cargo.transform.localScale=Vector3.one*.42f;var body=cargo.GetComponent<Rigidbody>();var reset=cargo.GetComponent<CargoReset>();
            yield return null;
            var hand=world.shooter.hand;hand.SetPositionAndRotation(body.position-camera.transform.forward*.65f,Quaternion.LookRotation(camera.transform.forward));Physics.SyncTransforms();
            world.shooter.EasyThrow=true;
            Physics.Raycast(hand.position,hand.forward,out var diagnostic,65,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            Check(world.shooter.Fire(1),"easy-mode crate attaches; hit="+(diagnostic.collider?diagnostic.collider.name+" layer "+diagnostic.collider.gameObject.layer:"none")+"; hand="+hand.position+"; cargo="+body.position);
            float until=Time.realtimeSinceStartup+2f;while(!world.shooter.Carrying&&Time.realtimeSinceStartup<until)yield return null;
            Check(world.shooter.Carrying,"stable carry acquired");
            world.input.StepTrackedGesture(1,false,true,Vector3.zero,.2f);
            Check(world.shooter.Active&&!world.shooter.ThrowArmed,"already-open hand cannot accidentally throw on pickup");
            world.input.StepTrackedGesture(1,false,false,Vector3.zero,.15f);
            Check(world.shooter.ThrowArmed,"close fingers arms easy throw");
            camera.transform.LookAt(toy.transform.position);world.shooter.UpdateThrowAim(1);
            Check(Mathf.Abs(world.shooter.PlannedThrow.magnitude-5.5f)<.01f,"easy throw has consistent strength");
            Check(world.shooter.AimAssisted,"nearby visible target receives narrow aim assist");
            yield return null;
            InteractionChecks.Preview(camera,"MR-aim-1.5.png");
            int hits=toy.Hits;
            world.input.StepTrackedGesture(1,false,true,Vector3.zero,.13f);
            Check(!world.shooter.Active&&Mathf.Abs(body.linearVelocity.magnitude-5.5f)<.01f,"open palm throws without fast arm movement");
            until=Time.realtimeSinceStartup+.8f;while(Time.realtimeSinceStartup<until)yield return null;
            Check(toy.Hits>hits,"virtual projectile hits chandelier and scores");
            reset.ScheduleRecall(.08f);until=Time.realtimeSinceStartup+.18f;while(Time.realtimeSinceStartup<until)yield return null;
            Check(Vector3.Distance(body.position,reset.spawn)<.25f&&!reset.RecallPending,"thrown cargo automatically returns");
            UnityEngine.Object.Destroy(cargo);
            world.modes.SelectMode(false);world.motor.enabled=true;world.motor.SetComfort(true);world.motor.SetSpawn(new Vector3(500,4,0));
            world.motor.head.localRotation=Quaternion.identity;
            Vector3 start=world.motor.transform.position;world.motor.Attach(new Vector3(500,10,8),1);float length=world.motor.GetLength(1);
            until=Time.realtimeSinceStartup+.5f;while(Time.realtimeSinceStartup<until)yield return null;
            Check(Mathf.Abs(length-world.motor.GetLength(1))<.001f&&Vector3.Distance(start,world.motor.transform.position)<.03f,"comfort attachment alone does not reel or move player");
            until=Time.realtimeSinceStartup+1.3f;while(Time.realtimeSinceStartup<until){world.motor.RequestPull(1,.85f*Time.deltaTime);yield return null;}
            Check(world.motor.GetLength(1)<length-.5f&&Vector3.Distance(start,world.motor.transform.position)>.3f,"manual pulls reel and move player gradually");
            until=Time.realtimeSinceStartup+.35f;while(Time.realtimeSinceStartup<until)yield return null;float stopped=world.motor.GetLength(1);
            until=Time.realtimeSinceStartup+.3f;while(Time.realtimeSinceStartup<until)yield return null;
            Check(Mathf.Abs(stopped-world.motor.GetLength(1))<.01f,"stop pulling stops reeling");
            world.motor.Release();until=Time.realtimeSinceStartup+1;while(Time.realtimeSinceStartup<until)yield return null;
            Check(world.motor.Velocity.y>=-1.81f&&world.motor.Velocity.y<0,"beginner fall speed capped");
            world.modes.ShowMenu();start=world.motor.transform.position;
            until=Time.realtimeSinceStartup+.25f;while(Time.realtimeSinceStartup<until)yield return null;
            Check(Vector3.Distance(start,world.motor.transform.position)<.001f,"menu pauses airborne movement");
            Check(Vector3.Dot(world.input.head.up,Vector3.up)>.999f,"camera remains upright");
            world.modes.HideMenu();world.motor.SetSpawn(new Vector3(0,.12f,-38));world.motor.enabled=false;
            Check(UnityEngine.Object.FindFirstObjectByType<PracticeCourse>()!=null,"low practice course available");
            var course=UnityEngine.Object.FindFirstObjectByType<PracticeCourse>();
            PracticeCourse.Attached(new Vector3(0,0,-30),null);
            Check(course.Stage==0,"unrelated hit does not skip first practice task");
            var post=GameObject.Find("Practice web post");PracticeCourse.Attached(post.transform.position,post.GetComponent<Collider>());
            Check(course.Stage==1,"attaching practice post advances tutorial");
            File.WriteAllLines("ProjectSettings/BeginnerCheck.txt",results);Debug.Log("WEB_BEGINNER_CHECK_OK: "+results.Count+" checks");
        }
    }
}
