using UnityEngine;
namespace WebSwing
{
    public static class MRFurniture
    {
        static GameObject Part(Transform parent,string name,Vector3 position,Vector3 size,bool metal=false,int layer=8)
        {
            var part=GameObject.CreatePrimitive(PrimitiveType.Cube);part.name=name;part.layer=layer;
            part.transform.SetParent(parent,false);part.transform.localPosition=position;part.transform.localScale=size;
            part.GetComponent<Renderer>().sharedMaterial=metal?CityEnvironment.MetalSurface:CityEnvironment.WoodSurface;return part;
        }
        static Rigidbody Body(GameObject root,float mass)
        {
            var rb=root.AddComponent<Rigidbody>();rb.mass=mass;rb.linearDamping=.12f;rb.angularDamping=1.5f;
            rb.interpolation=RigidbodyInterpolation.Interpolate;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            var reset=root.AddComponent<CargoReset>();reset.spawn=root.transform.position;reset.spawnRotation=root.transform.rotation;return rb;
        }
        public static GameObject Chair(Transform parent,Vector3 position,Quaternion rotation)
        {
            var root=new GameObject("MR pullable chair");root.transform.SetParent(parent,false);root.transform.SetPositionAndRotation(position,rotation);
            Part(root.transform,"Seat",new Vector3(0,.48f,0),new Vector3(.66f,.09f,.62f));
            Part(root.transform,"Back",new Vector3(0,.91f,.27f),new Vector3(.63f,.33f,.07f));
            foreach(float x in new[]{-.26f,.26f})
            {
                Part(root.transform,"Back post",new Vector3(x,.77f,.27f),new Vector3(.06f,.65f,.06f),true);
                foreach(float z in new[]{-.24f,.24f})Part(root.transform,"Leg",new Vector3(x,.23f,z),new Vector3(.06f,.46f,.06f),true);
            }
            Body(root,4);return root;
        }
        public static GameObject Door(Transform parent,Vector3 position,Quaternion rotation)
        {
            var frame=new GameObject("MR door frame");frame.transform.SetParent(parent,false);frame.transform.SetPositionAndRotation(position,rotation);
            foreach(float x in new[]{-.49f,.49f})Part(frame.transform,"Frame",new Vector3(x,.98f,0),new Vector3(.08f,1.96f,.13f),true,9);
            Part(frame.transform,"Lintel",new Vector3(0,1.97f,0),new Vector3(1.06f,.08f,.13f),true,9);
            var leaf=new GameObject("MR hinged door");leaf.transform.SetParent(frame.transform,false);leaf.transform.localPosition=new Vector3(0,.97f,0);
            Part(leaf.transform,"Door panel",Vector3.zero,new Vector3(.86f,1.85f,.065f));
            Part(leaf.transform,"Handle",new Vector3(.31f,0,-.075f),new Vector3(.12f,.045f,.09f),true);
            var rb=Body(leaf,6);rb.angularDamping=2;
            var hinge=leaf.AddComponent<HingeJoint>();hinge.axis=Vector3.up;hinge.anchor=new Vector3(-.43f,0,0);
            hinge.autoConfigureConnectedAnchor=false;hinge.connectedAnchor=leaf.transform.TransformPoint(hinge.anchor);
            hinge.useLimits=true;hinge.limits=new JointLimits{min=-110,max=110};return leaf;
        }
    }
}
