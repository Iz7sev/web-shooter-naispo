using UnityEngine;
namespace WebSwing
{
    public static class CityDistrict
    {
        public const float HalfWidth=128,South=-60,North=294;
        static readonly string[] shops={"COFFEE","MARKET","HOTEL","BOOKS","BAKERY","WEB GEAR"};
        static Material stone,metal,glass,sign;
        static Transform root;
        static GameObject Box(string name,Vector3 p,Vector3 size,Material material,bool collider=false)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root,false);go.transform.position=p;go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=material;
            if(!collider){go.GetComponent<Collider>().enabled=false;Object.Destroy(go.GetComponent<Collider>());}return go;
        }
        static void Label(string text,Vector3 p,float yaw,float size=.13f)
        {
            var go=new GameObject("District sign / "+text);go.transform.SetParent(root,false);go.transform.SetPositionAndRotation(p,Quaternion.Euler(0,yaw,0));
            var mesh=go.AddComponent<TextMesh>();mesh.text=text;mesh.anchor=TextAnchor.MiddleCenter;mesh.fontSize=64;mesh.characterSize=size;mesh.color=new Color(.96f,.92f,.8f);
            mesh.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");go.GetComponent<Renderer>().sharedMaterial=CityEnvironment.FontMaterial(mesh.font);
        }
        public static void Build(Transform parent,Material concrete,Material frame,Material glazing,Material panel)
        {
            root=new GameObject("City district / playable boundary and streets").transform;root.SetParent(parent,false);stone=concrete;metal=frame;glass=glazing;sign=panel;
            for(int side=-1;side<=1;side+=2)
            {
                for(int i=0;i<9;i++)
                {
                    float z=22+i*25+(i>=5?22:0);float x=side*12.25f;
                    Box("Shop fascia",new Vector3(x,3.7f,z),new Vector3(.15f,.95f,8.2f),sign);
                    Label(shops[(i+(side+1))%shops.Length],new Vector3(x-side*.11f,3.7f,z),side*90);
                    Box("Store canopy",new Vector3(x-side*.7f,3.05f,z),new Vector3(1.5f,.14f,8.4f),metal);
                    Box("Store display window",new Vector3(x-side*.03f,1.7f,z),new Vector3(.12f,2.25f,7.6f),glass);
                    for(int j=-2;j<=2;j++)Box("Store window frame",new Vector3(x-side*.11f,1.7f,z+j*1.5f),new Vector3(.1f,2.3f,.055f),metal);
                }
                foreach(float z in new[]{15f,27f,135f,159f,275f})
                    CityAssetKit.Prop(root,"traffic-light",new Vector3(side*9.65f,0,z),new Vector3(.75f,5.2f,.65f),side>0?90:-90);
                foreach(float z in new[]{55f,112f,192f,250f})
                    CityAssetKit.Prop(root,"dumpster",new Vector3(side*12,.05f,z),new Vector3(1.1f,1.2f,1.5f),side*90);
                foreach(float z in new[]{83f,210f})BusStop(side,z);
                for(int i=0;i<7;i++)
                {
                    float z=37+i*32;
                    CityAssetKit.Vehicle(root,new Vector3(side*7.4f,.7f,z),i+(side+1));
                    Box("Parking bay",new Vector3(side*8.8f,.026f,z),new Vector3(.06f,.015f,5.5f),stone);
                    Box("Parking end stripe",new Vector3(side*7.5f,.026f,z+2.8f),new Vector3(2.6f,.015f,.06f),stone);
                }
                Boundary("East west city limit",new Vector3(side*HalfWidth,1.6f,117),new Vector3(2,3.2f,354));
                foreach(float z in new[]{10f,145f,270f})
                    Label("PLAY AREA",new Vector3(side*(HalfWidth-1.1f),2.4f,z),side*90,.16f);
            }
            Boundary("South city limit",new Vector3(0,1.6f,South),new Vector3(256,3.2f,2));
            Boundary("North city limit",new Vector3(0,1.6f,North),new Vector3(256,3.2f,2));
            Label("END OF PLAY AREA",new Vector3(0,2.2f,North-1.1f),0,.17f);
            Label("END OF PLAY AREA",new Vector3(0,2.2f,South+1.1f),180,.17f);
            CityAssetKit.Prop(root,"sign-highway",new Vector3(0,0,132),new Vector3(.8f,7,13),90);
            for(int i=0;i<6;i++)CityAssetKit.Prop(root,"construction-barrier",new Vector3(-3+i*1.2f,0,281),new Vector3(.4f,1,1.4f),90);
            Backdrop(parent);
        }
        static void BusStop(int side,float z)
        {
            float x=side*11.6f;
            Box("Bus shelter roof",new Vector3(x,2.7f,z),new Vector3(2,.18f,5.5f),metal);
            foreach(float end in new[]{-2.4f,2.4f})Box("Bus shelter post",new Vector3(x+side*.75f,1.35f,z+end),new Vector3(.1f,2.7f,.1f),metal,true);
            Box("Bus shelter back",new Vector3(x+side*.8f,1.5f,z),new Vector3(.08f,1.9f,4.8f),glass);
            Box("Bus stop sign",new Vector3(x-side*.95f,2.3f,z-2.2f),new Vector3(.12f,.75f,.7f),sign);
            Label("BUS",new Vector3(x-side*1.02f,2.35f,z-2.2f),side*90,.075f);
        }
        static void Boundary(string name,Vector3 p,Vector3 size)
        {
            Box(name+" / visible barrier",p,size,stone,true);
            var wall=new GameObject(name+" / travel limit");wall.transform.SetParent(root,false);wall.transform.position=new Vector3(p.x,100,p.z);
            wall.AddComponent<BoxCollider>().size=new Vector3(size.x,200,size.z);
        }
        static void FarBuilding(CityDetailedTower.Geometry shell,CityDetailedTower.Geometry roof,Vector3 center,Vector3 size,int seed)
        {
            shell.Box(center,size,true);
            Vector3 top=center+Vector3.up*size.y*.5f;
            roof.Box(top+Vector3.up*.22f,new Vector3(size.x+.3f,.44f,size.z+.3f));
            if(seed%3==0)shell.Box(top+Vector3.up*3,new Vector3(size.x*.65f,6,size.z*.65f),true);
            roof.Box(top+new Vector3(-3,1.3f,2),new Vector3(3,2.6f,4));
            if(seed%2==0)roof.Box(top+new Vector3(4,3.2f,-4),new Vector3(.2f,6.4f,.2f));
        }
        static void Backdrop(Transform parent)
        {
            var old=root;root=new GameObject("City far skyline / visual only").transform;root.SetParent(parent,false);
            var facade=new Material(Shader.Find("Standard")){color=new Color(.84f,.87f,.9f),enableInstancing=true};
            facade.mainTexture=Resources.Load<Texture2D>("WebSwing/CityPhoto/SkylineFacade");facade.mainTextureScale=Vector2.one*.25f;
            facade.SetFloat("_Glossiness",.27f);
            var shell=new CityDetailedTower.Geometry();var roof=new CityDetailedTower.Geometry();
            int seed=0;
            for(int side=-1;side<=1;side+=2)for(int row=0;row<3;row++)for(int i=0;i<12;i++)
            {
                float height=28+(i*17+row*13)%65;
                FarBuilding(shell,roof,new Vector3(side*(157+row*38),height*.5f,-65+i*35),new Vector3(20,height,24),seed++);
            }
            for(int i=0;i<15;i++)
            {
                float height=35+(i*23)%60;
                FarBuilding(shell,roof,new Vector3(-250+i*36,height*.5f,340+i%3*28),new Vector3(24,height,20),seed++);
                FarBuilding(shell,roof,new Vector3(-250+i*36,height*.5f,-110-i%3*28),new Vector3(24,height,20),seed++);
            }
            var meshes=new Mesh[2];
            for(int i=0;i<2;i++)
            {
                var go=new GameObject(i==0?"Merged distant window facades":"Merged distant rooftops",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);
                meshes[i]=(i==0?shell:roof).Mesh("Far city merged geometry");go.GetComponent<MeshFilter>().sharedMesh=meshes[i];
                var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=i==0?facade:metal;
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            }
            root.gameObject.AddComponent<CityTowerMeshLifetime>().meshes=meshes;
            root=old;
        }
    }
}
