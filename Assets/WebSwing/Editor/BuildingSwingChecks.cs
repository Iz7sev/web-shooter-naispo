using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace WebSwing.Editor
{
    public static class BuildingSwingChecks
    {
        static readonly List<string> results=new List<string>();
        static void Check(bool ok,string label){if(!ok)throw new Exception(label);results.Add("PASS: "+label);}
        public static IEnumerator Run(DemoWorld world)
        {
            results.Clear();world.input.enabled=false;world.input.ReleaseAll();world.modes.SelectMode(false);world.motor.SetComfort(false);world.motor.SetWallWalkEnabled(false);
            world.input.head.localPosition=new Vector3(0,1.65f,0);world.input.head.localRotation=Quaternion.identity;
            Vector3 velocity=WebPhysics.BuildingLaunchVelocity(new Vector3(0,35,60),Vector3.zero,.7f,9.81f);
            Check(velocity.y*velocity.y/(2*9.81f)>45&&velocity.magnitude<53,"building launch has roof-height ballistic energy and bounded speed");
            world.motor.SetSpawn(new Vector3(1400,20,0));Vector3 start=world.motor.transform.position;
            world.motor.Attach(world.motor.BodyPoint+new Vector3(0,35,60),1);world.motor.SetGrip(1,true);
            Check(world.motor.LaunchFromPull(1,.7f),"building-height launch accepts gripping player");
            float highest=start.y,until=Time.fixedTime+3.7f;
            while(Time.fixedTime<until){highest=Mathf.Max(highest,world.motor.transform.position.y);yield return null;}
            Check(highest-start.y>45,"player actually rises more than forty-five metres after launch; rise="+(highest-start.y).ToString("F1"));
            Check(world.motor.transform.position.z-start.z>45,"player flies across a building-width distance");
            Check(world.motor.Velocity.y<0,"flight reaches apex then gravity reverses vertical motion");
            world.motor.SetSpawn(new Vector3(1600,100,0));world.input.head.localRotation=Quaternion.identity;
            Vector3 anchor=world.motor.BodyPoint+Vector3.up*12;
            world.motor.Attach(anchor,1);world.motor.SetGrip(1,true);
            float positive=0,negative=0,lastDirection=0;int reversals=0;float error=0;
            until=Time.fixedTime+8.5f;
            while(Time.fixedTime<until)
            {
                float z=world.motor.transform.position.z,velocityZ=world.motor.Velocity.z;
                positive=Mathf.Max(positive,z);negative=Mathf.Min(negative,z);
                if(Mathf.Abs(velocityZ)>.5f){float sign=Mathf.Sign(velocityZ);if(lastDirection!=0&&sign!=lastDirection)reversals++;lastDirection=sign;}
                error=Mathf.Max(error,Mathf.Abs(Vector3.Distance(world.motor.BodyPoint,anchor)-world.motor.GetLength(1)));
                yield return null;
            }
            Check(positive>2&&negative< -2&&reversals>=2,"gravity swing crosses both sides and reverses repeatedly; forward="+positive.ToString("F1")+" back="+negative.ToString("F1")+" reversals="+reversals);
            Check(error<.1f&&world.motor.GetLength(1)>5.9f,"swing retains a useful radius instead of reeling into anchor");
            Vector3 before=world.motor.Velocity;world.motor.Release();
            Check(Vector3.Distance(before,world.motor.Velocity)<.001f,"releasing natural swing preserves its current momentum");
            world.motor.SetComfort(true);world.motor.Attach(world.motor.BodyPoint+Vector3.up*10,1);world.motor.SetGrip(1,true);
            Check(!world.motor.LaunchFromPull(1,1),"comfort mode still disables strong building launch");
            world.motor.SetComfort(false);world.motor.SetSpawn(new Vector3(0,5.6f,-2));world.motor.SetWallWalkEnabled(true);
            var silk=world.shooter.GetComponent<WebRopeVisual>();var camera=world.input.head.GetComponent<Camera>();
            world.motor.enabled=false;
            silk.Draw(camera.transform.TransformPoint(new Vector3(-.35f,-.15f,.5f)),camera.transform.TransformPoint(new Vector3(.45f,.05f,2.2f)),Vector3.back,.35f,1);
            var material=silk.VisualRoot.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            Check(material.mainTexture&&material.GetTexture("_BumpMap")&&silk.VisualRoot.GetComponentInChildren<MeshFilter>().sharedMesh.tangents.Length==876,"silk uses fibre color, normal relief and valid lighting tangents");
            InteractionChecks.Preview(camera,"VR-silk-close-1.14.png");silk.Hide();world.motor.enabled=true;
            File.WriteAllLines("ProjectSettings/BuildingSwingCheck.txt",results);Debug.Log("WEB_BUILDING_SWING_CHECK_OK: "+results.Count+" checks");
        }
    }
}
