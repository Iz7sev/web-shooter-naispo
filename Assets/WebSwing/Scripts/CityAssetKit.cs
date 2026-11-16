using UnityEngine;
using UnityEngine.Rendering;
namespace WebSwing
{
    public static class CityAssetKit
    {
        static readonly string[] buildings={"building-a","building-d","building-g","building-m","building-skyscraper-a","building-skyscraper-c"};
        static readonly string[] vehicles={"sedan","taxi","van"};
        static readonly Material[] materials=new Material[3];
        public static int BuildingCount { get; private set; }
        public static int VehicleCount { get; private set; }
        static Material Material(string pack)
        {
            int i=pack=="commercial"?0:pack=="roads"?1:2;
            if(!materials[i])
            {
                materials[i]=new Material(Shader.Find("Standard")){mainTexture=Resources.Load<Texture2D>("WebSwing/CityKit/"+pack+"/colormap"),enableInstancing=true};
                materials[i].SetFloat("_Glossiness",pack=="cars"?.38f:.16f);
            }
            return materials[i];
        }
        static GameObject Model(Transform parent,string pack,string name,Vector3 center,Vector3 size,Quaternion rotation)
        {
            var prefab=Resources.Load<GameObject>("WebSwing/CityKit/"+pack+"/"+name);if(!prefab)return null;
            var root=new GameObject("Kenney / "+name);root.transform.SetParent(parent,false);root.transform.position=center;
            var model=Object.Instantiate(prefab,root.transform);model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.identity;
            var renderers=model.GetComponentsInChildren<Renderer>();if(renderers.Length==0){Object.Destroy(root);return null;}
            Bounds bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            Vector3 scale=new Vector3(size.x/Mathf.Max(.001f,bounds.size.x),size.y/Mathf.Max(.001f,bounds.size.y),size.z/Mathf.Max(.001f,bounds.size.z));
            model.transform.localScale=Vector3.Scale(model.transform.localScale,scale);model.transform.localPosition=-Vector3.Scale(bounds.center-center,scale);
            root.transform.rotation=rotation;
            foreach(var renderer in renderers)
            {
                renderer.gameObject.layer=8;renderer.sharedMaterials=SingleMaterial(renderer.sharedMaterials.Length,Material(pack));
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
            }
            return root;
        }
        static Material[] SingleMaterial(int count,Material material){var result=new Material[Mathf.Max(1,count)];for(int i=0;i<result.Length;i++)result[i]=material;return result;}
        public static bool Building(Transform parent,Vector3 center,Vector3 size,int seed,Material farMaterial)
        {
            if(Mathf.Abs(center.x)<30&&center.z<100&&CityDetailedTower.Build(parent,center,size,seed,farMaterial)){BuildingCount++;return true;}
            var root=Model(parent,"commercial",buildings[seed%buildings.Length],center,size,Quaternion.identity);if(!root)return false;
            root.name="City building / imported LOD";root.layer=8;
            var near=root.GetComponentsInChildren<Renderer>();var collider=root.AddComponent<BoxCollider>();collider.size=size;
            var proxy=GameObject.CreatePrimitive(PrimitiveType.Cube);proxy.name="Distant building proxy";proxy.transform.SetParent(root.transform,false);proxy.transform.localScale=size;
            proxy.GetComponent<Collider>().enabled=false;Object.Destroy(proxy.GetComponent<Collider>());proxy.GetComponent<Renderer>().sharedMaterial=farMaterial;
            var lod=root.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.08f,near),new LOD(.003f,new[]{proxy.GetComponent<Renderer>()})});lod.RecalculateBounds();
            BuildingCount++;return true;
        }
        public static bool Vehicle(Transform parent,Vector3 position,int seed)
        {
            var root=Model(parent,"cars",vehicles[seed%vehicles.Length],new Vector3(position.x,.82f,position.z),new Vector3(1.9f,1.6f,4.4f),Quaternion.Euler(0,seed%2*180,0));if(!root)return false;
            root.layer=8;root.AddComponent<BoxCollider>().size=new Vector3(1.9f,1.6f,4.4f);
            var lod=root.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.012f,root.GetComponentsInChildren<Renderer>())});lod.RecalculateBounds();VehicleCount++;return true;
        }
        public static GameObject Prop(Transform parent,string name,Vector3 position,Vector3 size,float yaw=0)
        {
            var root=Model(parent,"roads",name,position+Vector3.up*(size.y*.5f),size,Quaternion.Euler(0,yaw,0));if(!root)return null;
            var lod=root.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.015f,root.GetComponentsInChildren<Renderer>())});lod.RecalculateBounds();return root;
        }
    }
}
