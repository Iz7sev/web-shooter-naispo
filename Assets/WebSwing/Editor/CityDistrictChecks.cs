using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace WebSwing.Editor
{
    public static class CityDistrictChecks
    {
        static readonly List<string> results=new List<string>();
        static void Check(bool ok,string label){if(!ok)throw new Exception(label);results.Add("PASS: "+label);}
        public static IEnumerator Run(DemoWorld world)
        {
            results.Clear();world.input.ReleaseAll();world.input.enabled=false;world.motor.enabled=false;world.modes.SelectMode(false);
            world.motor.SetComfort(false);world.input.head.localPosition=new Vector3(0,1.65f,0);world.input.head.localRotation=Quaternion.identity;
            Check(Mathf.Abs(world.motor.reelSpeed-4f)<.001f,"normal reeling cap is four metres per second");
            Check(Mathf.Abs(InteractionMotion.WalkTarget(Vector3.forward*.8f,Vector3.back*.8f,Vector3.forward)-5.589f)<.01f,"same arm swing now produces the updated walking speed");
            Check(CityAssetKit.BuildingCount>=48&&CityAssetKit.VehicleCount>=10,"imported CC0 city buildings and vehicles instantiated");
            var front=GameObject.Find("City district / playable boundary and streets");Check(front&&front.transform.Find("Shop fascia"),"nearby storefront geometry exists");
            Check(GameObject.Find("City far skyline / visual only").GetComponentsInChildren<Collider>().Length==0,"distant skyline has no gameplay collision");
            Check(RenderSettings.fogMode==FogMode.Linear&&RenderSettings.fogEndDistance<world.input.head.GetComponent<Camera>().farClipPlane,"skyline fades before camera clipping");
            var farCity=GameObject.Find("City far skyline / visual only");
            var farRenderers=farCity.GetComponentsInChildren<MeshRenderer>();
            Check(farRenderers.Length==2&&farRenderers[0].sharedMaterial.mainTexture,"distant windows and rooftops use only two batched renderers");
            bool missingTexture=false;int imported=0,lods=0,detailed=0;
            foreach(var renderer in CityEnvironment.Root.GetComponentsInChildren<Renderer>())
            {
                var group=renderer.GetComponentInParent<LODGroup>();
                if(group&&group.name=="City building / imported LOD"&&renderer.name!="Distant building proxy")
                {imported++;missingTexture|=!renderer.sharedMaterial||!renderer.sharedMaterial.mainTexture;}
            }
            foreach(var lod in CityEnvironment.Root.GetComponentsInChildren<LODGroup>())
            {
                if(lod.name=="City building / imported LOD"&&lod.lodCount==2)lods++;
                if(lod.name=="City building / detailed tower LOD"&&lod.lodCount==2)detailed++;
            }
            Check(detailed>=8,"eight nearby towers have merged window frames, glazing and roof equipment");
            bool photo=false;foreach(var renderer in CityEnvironment.Root.GetComponentsInChildren<Renderer>())
                if(renderer.name=="Photo-textured tower shell")photo|=renderer.sharedMaterial&&renderer.sharedMaterial.mainTexture;
            Check(photo,"nearby tower shells use photo facade materials");
            Check(lods+detailed>=48,"city buildings switch between detailed meshes and cheap distant proxies");
            Check(imported>0&&!missingTexture,"imported model renderers have shared colormap textures");
            world.motor.SetSpawn(new Vector3(124,.12f,100));world.input.head.localRotation=Quaternion.Euler(0,90,0);world.motor.enabled=true;world.motor.SetArmWalkSpeed(6.2f);
            float until=Time.fixedTime+1.8f;while(Time.fixedTime<until)yield return null;
            Check(world.motor.transform.position.x>125f&&world.motor.transform.position.x<=127.05f,"east travel boundary stops fast walking; x="+world.motor.transform.position.x.ToString("F3"));
            world.motor.SetArmWalkSpeed(0);world.motor.enabled=false;world.motor.SetSpawn(new Vector3(0,.12f,14));world.input.head.localRotation=Quaternion.identity;
            yield return null;InteractionChecks.Preview(world.input.head.GetComponent<Camera>(),"VR-street-1.10.png");
            world.motor.SetSpawn(new Vector3(0,5.6f,-2));yield return null;InteractionChecks.Preview(world.input.head.GetComponent<Camera>(),"VR-city-1.10.png");
            world.motor.SetSpawn(new Vector3(35,68,80));world.input.head.localRotation=Quaternion.LookRotation(new Vector3(170,35,220)-world.input.head.position);
            yield return null;InteractionChecks.Preview(world.input.head.GetComponent<Camera>(),"VR-skyline-1.12.png");
            world.motor.SetSpawn(new Vector3(0,5.6f,-2));world.input.head.localRotation=Quaternion.identity;
            world.motor.SetComfort(true);Check(Mathf.Abs(world.motor.reelSpeed-1.2f)<.001f,"comfort mode retains its slower reeling");world.motor.SetComfort(false);
            File.WriteAllLines("ProjectSettings/CityDistrictCheck.txt",results);Debug.Log("WEB_CITY_DISTRICT_CHECK_OK: "+results.Count+" checks");
        }
    }
}
