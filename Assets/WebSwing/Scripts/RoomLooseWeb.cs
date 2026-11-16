using System.Collections.Generic;
using UnityEngine;
namespace WebSwing
{
    // An unheld MR shot leaves only a contact patch on its target.
    public sealed class RoomLooseWeb : MonoBehaviour
    {
        static readonly List<RoomLooseWeb> all=new List<RoomLooseWeb>();
        Rigidbody target;bool hadBody;
        Vector3 localHit,localNormal,point,normal;
        LineRenderer patch;Material material;
        static readonly Vector3[] shape=Shape();
        public Vector3 StartPoint { get; private set; }
        public Vector3 EndPoint=>target?target.transform.TransformPoint(localHit):point;
        public Vector3 Normal=>target?target.transform.TransformDirection(localNormal):normal;
        public static int Count=>all.Count;
        float PickRadius=>target?.26f:.45f;
        void OnEnable(){all.Add(this);}
        void OnDisable(){all.Remove(this);}
        static Vector3[] Shape()
        {
            var result=new List<Vector3>();
            for(int i=0;i<8;i++){float a=i*Mathf.PI/4;result.Add(Vector3.zero);result.Add(new Vector3(Mathf.Cos(a),Mathf.Sin(a),0));result.Add(Vector3.zero);}
            for(int ring=1;ring<=4;ring++)for(int i=0;i<=32;i++)
            {float a=i*Mathf.PI/16;float r=ring/4f*(.94f+.06f*Mathf.Cos(a*8));result.Add(new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,0));}
            return result.ToArray();
        }
        public static RoomLooseWeb Create(Vector3 start,Rigidbody body,Vector3 end,Vector3 hitNormal,Color color)
        {
            if(!RoomWebMarks.Root)return null;
            while(all.Count>=6){var oldest=all[0];oldest.gameObject.SetActive(false);Destroy(oldest.gameObject);}
            var go=new GameObject("MR single-shot contact web");go.transform.SetParent(RoomWebMarks.Root,false);
            var web=go.AddComponent<RoomLooseWeb>();web.StartPoint=start;web.target=body;web.hadBody=body;web.point=end;web.normal=hitNormal;
            if(body){web.localHit=body.transform.InverseTransformPoint(end);web.localNormal=body.transform.InverseTransformDirection(hitNormal);}
            web.patch=go.AddComponent<LineRenderer>();web.patch.positionCount=shape.Length;web.patch.startWidth=web.patch.endWidth=.003f;
            web.material=new Material(Shader.Find("Sprites/Default")){color=Color.Lerp(Color.white,color,.22f)};web.patch.sharedMaterial=web.material;
            web.RefreshPatch();return web;
        }
        public static RoomLooseWeb Closest(Vector3 palm)
        {
            RoomLooseWeb result=null;float nearest=float.PositiveInfinity;
            foreach(var web in all){float distance=Vector3.Distance(palm,web.EndPoint);if(distance<web.PickRadius&&distance<nearest){nearest=distance;result=web;}}
            return result;
        }
        public static RoomLooseWeb Aimed(Ray ray)
        {
            RoomLooseWeb result=null;float best=float.PositiveInfinity;
            foreach(var web in all)
            {
                Vector3 delta=web.EndPoint-ray.origin;float distance=delta.magnitude;
                if(distance<.2f||distance>12)continue;
                float along=Vector3.Dot(delta,ray.direction.normalized);if(along<=0)continue;
                float offset=Vector3.Cross(delta,ray.direction.normalized).magnitude;
                if(offset>Mathf.Max(web.PickRadius,Mathf.Clamp(distance*.075f,.15f,.45f))||offset/distance>=best)continue;
                if(!Physics.Raycast(ray.origin,delta.normalized,out var hit,distance+.08f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))continue;
                if((!web.target||hit.rigidbody!=web.target)&&Vector3.Distance(hit.point,web.EndPoint)>.25f)continue;
                best=offset/distance;result=web;
            }
            return result;
        }
        public bool PickUp(WebShooter shooter,int owner)
        {
            if(!gameObject.activeInHierarchy||!shooter||shooter.Active||!shooter.motor.MovementLocked)return false;
            if(hadBody&&!target)return false;
            shooter.AdoptLooseWeb(target,EndPoint,Normal,owner);gameObject.SetActive(false);Destroy(gameObject);return true;
        }
        public void RefreshPatch()
        {
            Quaternion rotation=Quaternion.FromToRotation(Vector3.forward,Normal.normalized);
            Vector3 center=EndPoint+Normal*.014f;float radius=target?.23f:.42f;
            for(int i=0;i<shape.Length;i++)patch.SetPosition(i,center+rotation*(shape[i]*radius));
        }
        void LateUpdate()
        {
            if(hadBody&&!target){gameObject.SetActive(false);Destroy(gameObject);return;}
            RefreshPatch();
        }
        void OnDestroy(){if(material)Destroy(material);}
    }
}
