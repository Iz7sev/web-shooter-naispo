using UnityEngine;

namespace WebSwing
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class SwingMotor : MonoBehaviour
    {
        public Transform head;
        public NaispoSession Exhibition;
        public void StopForExhibition(){Velocity=Vector3.zero;walkSpeed=0;launchUntil=0;Release();launchUntil=0;}
        public float gravity=9.81f,maxSpeed=8f,reelSpeed=4f,reelAcceleration=5.4f,minimumRopeLength=2.5f;
        public Vector3 Velocity { get; private set; }
        public bool Attached => attached[0] || attached[1];
        public Vector3 Anchor => GetAnchor(attached[1]?1:0);
        public float RopeLength => GetLength(attached[1]?1:0);
        public float Tension01 => Mathf.Max(GetTension(0),GetTension(1));
        public Vector3 BodyPoint => transform.TransformPoint(body.center);
        public bool MovementLocked { get; private set; }
        public bool StaticWebMovementEnabled { get; private set; }
        public bool ComfortEnabled { get; private set; }=false;
        public bool WallWalkEnabled { get; private set; }
        public bool WallAttached { get; private set; }
        Vector3 wallNormal,wallVelocity,mantleTarget;
        bool mantling;float mantleUntil,wallDetachUntil,launchUntil,launchGroundGrace;
        public bool Mantling=>mantling;
        public bool LaunchFromPull(int hand,float strength)
        {
            if(Exhibition&&Exhibition.Guided)return Exhibition.GuidedLaunch(hand,strength);
            if(ComfortEnabled||MovementLocked||!attached[hand]||!grip[hand])return false;
            Vector3 launch=WebPhysics.BuildingLaunchVelocity(anchors[hand]-BodyPoint,Velocity,strength,gravity);
            Release();WallAttached=mantling=false;wallVelocity=Vector3.zero;
            wallDetachUntil=Time.time+1.2f;launchUntil=Time.time+10f;launchGroundGrace=Time.time+.35f;
            Velocity=launch;
            WebActionSound.Shoot(BodyPoint);return true;
        }
        public void SetWallWalkEnabled(bool value)
        {
            Release(); WallWalkEnabled=value&&!MovementLocked; WallAttached=false;
            mantling=false;launchUntil=0;wallVelocity=Velocity=Vector3.zero;
        }
        bool TryRoof()
        {
            Vector3 probe=BodyPoint-wallNormal*(body.radius+.85f)+Vector3.up*(body.height+1);
            if(!Physics.Raycast(probe,Vector3.down,out var roof,body.height*2+1.5f,1<<8,QueryTriggerInteraction.Ignore)||roof.normal.y<.85f||
                (roof.rigidbody&&!roof.rigidbody.isKinematic)||roof.point.y-transform.position.y>body.height+.5f||roof.point.y<transform.position.y-.1f)return false;
            Vector3 destination=roof.point-transform.TransformVector(new Vector3(body.center.x,0,body.center.z))+Vector3.up*.09f;
            Vector3 center=destination+body.center;float radius=body.radius*.9f,half=Mathf.Max(0,body.height*.5f-radius);
            if(Physics.CheckCapsule(center+Vector3.up*half,center-Vector3.up*half,radius,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))return false;
            mantleTarget=destination;mantling=true;mantleUntil=Time.time+3;Velocity=wallVelocity=Vector3.zero;return true;
        }
        bool MoveOnWall(float dt)
        {
            if(Time.time<wallDetachUntil)return false;
            if(mantling)
            {
                if(Time.time>mantleUntil){mantling=WallAttached=false;return false;}
                Vector3 before=transform.position;
                Vector3 target=transform.position.y<mantleTarget.y-.015f?new Vector3(transform.position.x,mantleTarget.y,transform.position.z):mantleTarget;
                var flags=body.Move(Vector3.ClampMagnitude(target-transform.position,(ComfortEnabled?1:2.5f)*dt));
                Velocity=(transform.position-before)/dt;
                if((flags&CollisionFlags.Above)!=0){mantling=WallAttached=false;return false;}
                if(Vector3.Distance(transform.position,mantleTarget)<.035f){mantling=WallAttached=false;Velocity=Vector3.zero;walkSpeed=0;wallDetachUntil=Time.time+.5f;}
                return true;
            }
            Vector3 direction=WallAttached?-wallNormal:Vector3.ProjectOnPlane(head.forward,Vector3.up).normalized;
            if(direction.sqrMagnitude<.1f || !Physics.Raycast(BodyPoint,direction,out var hit,1f,
                Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) || Mathf.Abs(hit.normal.y)>.25f ||
                hit.collider.gameObject.layer!=8 || (hit.rigidbody && !hit.rigidbody.isKinematic))
            {
                if(WallAttached&&walkSpeed>.05f&&wallVelocity.y>0&&TryRoof())return true;
                WallAttached=false;wallVelocity=Vector3.zero;return false;
            }
            wallNormal=hit.normal;WallAttached=true;
            Vector3 climbDirection=InteractionMotion.WallDirection(head.forward,wallNormal);
            Vector3 targetVelocity=climbDirection*Mathf.Min(walkSpeed,ComfortEnabled?1f:3f);
            wallVelocity=Vector3.MoveTowards(wallVelocity,targetVelocity,(walkSpeed>.01f?3f:6f)*dt);
            float gap=Mathf.Max(0,hit.distance-body.radius-.025f);
            Vector3 old=transform.position;
            var collision=body.Move(wallVelocity*dt-wallNormal*Mathf.Min(gap,1.5f*dt));
            Velocity=(transform.position-old)/dt;
            if((collision&CollisionFlags.Above)!=0&&wallVelocity.y>0)wallVelocity.y=0;
            if((collision&CollisionFlags.Below)!=0&&wallVelocity.y<0)wallVelocity.y=0;
            return true;
        }
        public void SetComfort(bool value)
        {
            Release(); ComfortEnabled=value; StaticWebMovementEnabled=true;
            maxSpeed=value?3f:11f; reelSpeed=value?1.2f:4f; reelAcceleration=value?1.2f:5.4f;
            minimumRopeLength=value?1.2f:2f;
            walkSpeed=0; Velocity=Vector3.zero;
        }
        readonly bool[] attached=new bool[2],grip=new bool[2];
        public void SetGrip(int hand,bool value)
        {
            bool next=value&&attached[hand];
            if(next&&!grip[hand]&&!grip[1-hand]&&!ComfortEnabled&&!MovementLocked)
            {
                Vector3 look=Vector3.ProjectOnPlane(head.forward,Vector3.up).normalized;
                Vector3 tangent=Vector3.ProjectOnPlane(look,BodyPoint-anchors[hand]).normalized;
                if(tangent.sqrMagnitude>.1f)Velocity+=tangent*Mathf.Max(0,4f-Vector3.Dot(Velocity,tangent));
            }
            grip[hand]=next;
        }
        public bool IsGripping(int hand)=>grip[hand];
        readonly Vector3[] anchors=new Vector3[2];
        readonly float[] initialLengths=new float[2],lengths=new float[2],reelSpeeds=new float[2],pullRequests=new float[2];
        public void RequestPull(int hand,float distance){pullRequests[hand]=Mathf.Min(.12f,pullRequests[hand]+Mathf.Clamp(distance,0,.12f));}
        public void SetSpawn(Vector3 position){spawn=position;ResetPlayer();}
        CharacterController body;
        Vector3 spawn;
        Vector2 move;
        bool jump;
        float walkSpeed;
        public void SetDesktopMove(Vector2 value,bool jumpPressed) { move=value; jump|=jumpPressed; }
        public void SetArmWalkSpeed(float speed) { walkSpeed=Mathf.Clamp(speed,0,ComfortEnabled?1.8f:InteractionMotion.MaxWalkSpeed); }
        public bool IsAttached(int slot) => attached[Mathf.Clamp(slot,0,1)];
        public Vector3 GetAnchor(int slot) => anchors[Mathf.Clamp(slot,0,1)];
        public float GetLength(int slot) => lengths[Mathf.Clamp(slot,0,1)];
        public float GetTension(int slot) => attached[slot]?Mathf.Clamp01((Vector3.Distance(BodyPoint,anchors[slot])/Mathf.Max(.1f,lengths[slot])-.9f)*10):0;
        void Awake()
        {
            body=GetComponent<CharacterController>(); spawn=transform.position;
            Debug.Log("WEB_MOTION_READY: independent two-hand ropes, gravity and arm walking.");
        }
        public void Attach(Vector3 point,int slot=1)
        {
            if(MovementLocked) return;
            mantling=false;
            anchors[slot]=point; lengths[slot]=Mathf.Max(minimumRopeLength,Vector3.Distance(BodyPoint,point));
            initialLengths[slot]=lengths[slot];attached[slot]=true; reelSpeeds[slot]=0;
        }
        public void ReleaseTether(int slot)
        {
            bool was=attached[slot];attached[slot]=grip[slot]=false;reelSpeeds[slot]=pullRequests[slot]=0;
            if(was&&!Attached&&!ComfortEnabled&&!MovementLocked){launchUntil=Time.time+10f;launchGroundGrace=Time.time+.35f;}
        }
        public void Release() { ReleaseTether(0); ReleaseTether(1); }
        public void SetMovementLocked(bool value)
        {
            Release();launchUntil=0; MovementLocked=value; Velocity=Vector3.zero; walkSpeed=0;
            WallAttached=mantling=false; wallVelocity=Vector3.zero; if(value)WallWalkEnabled=false;
            body.enabled=!value;
            transform.position=value?Vector3.zero:spawn;
        }
        public void ResetPlayer()
        {
            Release(); Velocity=Vector3.zero; walkSpeed=0; WallAttached=mantling=false; wallVelocity=Vector3.zero;wallDetachUntil=launchUntil=0;
            body.enabled=false; transform.position=MovementLocked?Vector3.zero:spawn;
            body.enabled=!MovementLocked;
        }
        void FixedUpdate()
        {
            if(MovementLocked) { Velocity=Vector3.zero; return; }
            float dt=Time.fixedDeltaTime;
            float height=Mathf.Clamp(head.localPosition.y,.8f,2.2f);
            body.height=height; body.center=new Vector3(head.localPosition.x,height*.5f,head.localPosition.z);
            if(Exhibition&&Exhibition.RouteControlled)
            {Vector3 destination=Exhibition.StepRide(this,dt,out var routeVelocity);body.Move(destination-transform.position);Velocity=routeVelocity;return;}
            if(WallWalkEnabled && !Attached && MoveOnWall(dt)) { jump=false; return; }
            WallAttached=false;
            if(body.isGrounded&&Time.time>launchGroundGrace)launchUntil=0;
            Vector3 v=Velocity;
            if(ComfortEnabled&&Attached)v=Vector3.MoveTowards(v,Vector3.zero,6*dt);
            if(body.isGrounded && v.y<0) v.y=-1;
            if(jump && body.isGrounded) v.y=6;
            jump=false;
            Vector3 forward=Vector3.ProjectOnPlane(head.forward,Vector3.up).normalized;
            Vector3 right=Vector3.Cross(Vector3.up,forward);
            if(!Attached && body.isGrounded && move.sqrMagnitude<.01f)
            {
                Vector3 planar=Vector3.MoveTowards(new Vector3(v.x,0,v.z),forward*walkSpeed,(walkSpeed>new Vector2(v.x,v.z).magnitude?3.5f:5f)*dt);
                v.x=planar.x; v.z=planar.z;
            }
            v+=(forward*move.y+right*move.x)*(Attached?5:12)*dt;
            if(!(ComfortEnabled&&Attached))v+=Vector3.down*(ComfortEnabled?3f:gravity)*dt;
            if(ComfortEnabled)v.y=Mathf.Max(v.y,-1.8f);
            if(ComfortEnabled&&!Attached&&!body.isGrounded&&v.y<-.4f&&Physics.Raycast(transform.position+Vector3.up*.15f,Vector3.down,.65f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                v.y=Mathf.MoveTowards(v.y,-.4f,5*dt);
            v=Vector3.ClampMagnitude(v,ComfortEnabled?maxSpeed:Attached?28:Time.time<launchUntil?55:maxSpeed);
            float pairBudget=attached[0]&&attached[1]?Mathf.Max(0,lengths[0]+lengths[1]-Vector3.Distance(anchors[0],anchors[1])-.15f)*.5f:float.MaxValue;
            var shortening=Vector2.zero;
            for(int s=0;s<2;s++) if(attached[s])
            {
                float ropeFloor=ComfortEnabled?minimumRopeLength:Mathf.Max(minimumRopeLength,Mathf.Min(18,initialLengths[s]*.5f));
                float remaining=Mathf.Max(0,lengths[s]-ropeFloor);
                float distanceFactor=ComfortEnabled||!grip[s]?0:WebPhysics.ReelDistanceFactor(Vector3.Distance(BodyPoint,anchors[s]));
                float speedLimit=reelSpeed*Mathf.Lerp(1f,2f,distanceFactor);
                float acceleration=reelAcceleration*Mathf.Lerp(1f,1.5f,distanceFactor);
                float target=Mathf.Min(grip[s]?speedLimit:pullRequests[s]/dt,Mathf.Min(speedLimit,Mathf.Min(remaining*.9f,Mathf.Sqrt(2*acceleration*remaining))));
                reelSpeeds[s]=Mathf.MoveTowards(reelSpeeds[s],target,(target<reelSpeeds[s]?8f:acceleration)*dt);
                shortening[s]=Mathf.Min(grip[s]?float.MaxValue:pullRequests[s],Mathf.Min(Mathf.Min(remaining,reelSpeeds[s]*dt),pairBudget));
                pullRequests[s]=Mathf.Max(0,pullRequests[s]-shortening[s]);
                float oldLength=lengths[s];lengths[s]-=shortening[s];
                if(!ComfortEnabled&&!attached[1-s]&&shortening[s]>0)
                {
                    Vector3 radial=(BodyPoint-anchors[s]).normalized;
                    v=Vector3.ProjectOnPlane(v,radial)*(oldLength/Mathf.Max(.1f,lengths[s]))+radial*Vector3.Dot(v,radial);
                }
            }
            Vector3 desired=BodyPoint+v*dt;
            for(int iteration=0;iteration<4;iteration++) for(int s=0;s<2;s++) if(attached[s])
                desired=!ComfortEnabled&&!attached[1-s]
                    ?WebPhysics.ConstrainPendulum(desired,anchors[s],lengths[s],ref v,shortening[s]/dt)
                    :WebPhysics.Constrain(desired,anchors[s],lengths[s],ref v,shortening[s]/dt);
            v=Vector3.ClampMagnitude(v,ComfortEnabled?maxSpeed:Attached?28:Time.time<launchUntil?55:maxSpeed);
            Vector3 before=transform.position;
            CollisionFlags flags=body.Move(desired-BodyPoint);
            if((flags&CollisionFlags.Above)!=0 && v.y>0) v.y=0;
            if((flags&CollisionFlags.Below)!=0 && v.y<0) v.y=-1;
            if((flags&CollisionFlags.Sides)!=0)
            { var actual=(transform.position-before)/dt; v.x=actual.x; v.z=actual.z; }
            Velocity=v;
            if(transform.position.y< -20) ResetPlayer();
        }
    }
}
