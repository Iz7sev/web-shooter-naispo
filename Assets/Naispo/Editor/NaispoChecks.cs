using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
namespace WebSwing.Editor
{
    [InitializeOnLoad] public static class NaispoChecks
    {
        const string Flag="Naispo.Checks";static IEnumerator flow;static int frame=-1;
        static readonly List<string> results=new List<string>();
        static NaispoChecks(){EditorApplication.update+=Tick;}
        [MenuItem("Naispo/Run Exhibition Checks")]
        public static void Begin(){flow=null;results.Clear();frame=-1;NaispoBuild.ConfigureSurfaces();SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
        static void Tick()
        {
            if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling||frame==Time.frameCount)return;
            frame=Time.frameCount;
            try
            {
                if(!NaispoSession.Active)return;
                if(flow==null)flow=Run();if(flow.MoveNext())return;
                File.WriteAllLines("ProjectSettings/NaispoCheck.txt",results);SessionState.SetBool(Flag,false);Debug.Log("NAISPO_CHECK_OK: "+results.Count);EditorApplication.isPlaying=false;
            }
            catch(Exception error){File.WriteAllText("ProjectSettings/NaispoCheck.txt","FAIL: "+error+"\n"+string.Join("\n",results));SessionState.SetBool(Flag,false);Debug.LogException(error);EditorApplication.isPlaying=false;}
        }
        static void Check(bool ok,string label){if(!ok)throw new Exception(label);results.Add("PASS: "+label);}
        static void Advance(NaispoSession s,int steps){for(int i=0;i<steps;i++)s.Tick(.1f);}
        static IEnumerator Run()
        {
            yield return null;var s=NaispoSession.Active;s.enabled=false;s.world.input.enabled=false;var motor=s.world.motor;
            Check(s.Level==1&&s.Duration==120&&s.Phase==NaispoPhase.Ready&&s.RouteControlled,"startup is stationary beginner standby");
            Check(!s.Server.Listening,"operator server never runs on development PC");
            s.world.modes.ShowMenu();Check(!s.world.modes.MenuVisible,"participant gesture cannot open operator settings");
            Check(s.Execute(new NaispoCommand{action="start"})&&s.Phase==NaispoPhase.AwaitHands,"start hands control to participant readiness gate");
            s.CheckHands(false);Advance(s,15);Check(s.Phase==NaispoPhase.AwaitHands,"controller-to-hand switch cannot start before both hands appear");
            s.CheckHands(true);Advance(s,9);Check(s.Phase==NaispoPhase.Countdown,"stable hands begin countdown");Advance(s,32);Check(s.Phase==NaispoPhase.Running,"countdown starts hand-tracked play");
            Check(!s.Execute(new NaispoCommand{action="level",level=3}),"difficulty cannot change mid-experience");
            Vector3 start=motor.transform.position;motor.Attach(motor.BodyPoint+new Vector3(0,8,20),1);motor.SetGrip(1,true);
            float until=Time.fixedTime+2;while(Time.fixedTime<until)yield return null;
            Check(motor.transform.position.z>start.z+1&&Mathf.Abs(motor.transform.position.x)<.001f&&motor.transform.position.y>8&&motor.transform.position.y<=22f,"level one has the former level two upward flight");
            s.world.input.head.localRotation=Quaternion.Euler(0,90,0);until=Time.fixedTime+.5f;while(Time.fixedTime<until)yield return null;
            Check(Mathf.Abs(motor.transform.position.x)<.001f,"looking sideways does not steer beginner flight");
            Check(motor.LaunchFromPull(1,1),"same hand pull triggers guided forward launch");until=Time.fixedTime+.8f;while(Time.fixedTime<until)yield return null;
            Check(s.RideSpeed>5.2f&&s.RideSpeed<=8.51f&&motor.transform.position.y<=22f&&!motor.Attached,"level one pull launches upward and forward with bounded speed");
            Check(s.Execute(new NaispoCommand{action="pause"}),"staff can pause immediately");Vector3 paused=motor.transform.position;until=Time.fixedTime+.3f;while(Time.fixedTime<until)yield return null;
            Check(Vector3.Distance(paused,motor.transform.position)<.001f,"pause freezes body without resetting view position");
            float remaining=s.Remaining;s.Execute(new NaispoCommand{action="resume"});Advance(s,42);Check(s.Phase==NaispoPhase.Running&&s.Remaining<=remaining,"resume counts down without repeating the both-hand gate");
            s.CheckOneHand();Advance(s,25);Check(s.Phase==NaispoPhase.Running,"one visible hand continues the experience");
            s.CheckHands(false);Advance(s,17);Check(s.Phase==NaispoPhase.Paused,"tracking loss automatically pauses");
            s.Execute(new NaispoCommand{action="end"});Check(s.Phase==NaispoPhase.Finished,"staff end returns participant to handover state");
            s.Execute(new NaispoCommand{action="level",level=2,duration=90});Check(s.Level==2&&s.Duration==120&&s.Phase==NaispoPhase.Ready,"staff can select next visitor level and duration");
            s.ApplyStaffControls(0,true,false,.02f);Check(s.Phase==NaispoPhase.AwaitHands,"Touch primary button starts readiness handover");s.ApplyStaffControls(0,false,false,.02f);s.CheckHands(true);Advance(s,42);
            s.ApplyStaffControls(0,true,false,.02f);Check(s.Phase==NaispoPhase.Running,"ordinary Touch buttons cannot interrupt active participant");s.ApplyStaffControls(0,false,false,.02f);
            motor.Attach(motor.BodyPoint+new Vector3(0,15,30),1);motor.SetGrip(1,true);
            until=Time.fixedTime+3.5f;while(Time.fixedTime<until)yield return null;
            Check(s.RideSpeed>6.7f&&s.RideSpeed<=7.01f&&motor.transform.position.y>9&&Mathf.Abs(motor.transform.position.x)<.001f,"level two reels upward and travels faster while staying centered");
            float launchHeight=motor.transform.position.y;
            Check(motor.LaunchFromPull(1,1),"level two uses the same pull gesture to launch");
            until=Time.fixedTime+1;while(Time.fixedTime<until)yield return null;
            Check(!motor.Attached&&motor.transform.position.y>launchHeight+2&&s.RideSpeed>7f&&s.RideSpeed<=11.51f,"level two pull detaches and launches forward and upward");
            float peak=motor.transform.position.y;
            until=Time.fixedTime+5;while(Time.fixedTime<until){peak=Mathf.Max(peak,motor.transform.position.y);yield return null;}
            Check(motor.transform.position.y<peak-.5f&&motor.transform.position.y>=6&&Mathf.Abs(motor.transform.position.x)<.001f,"released level two flight descends under bounded gravity");
            s.Execute(new NaispoCommand{action="pause"});paused=motor.transform.position;
            until=Time.fixedTime+.3f;while(Time.fixedTime<until)yield return null;
            Check(Vector3.Distance(paused,motor.transform.position)<.001f,"staff pause also freezes vertical flight");
            s.Execute(new NaispoCommand{action="resume"});Advance(s,42);
            for(int i=0;i<16;i++)s.ApplyStaffControls(0,true,true,.1f);
            Check(s.Phase==NaispoPhase.Finished,"held primary and secondary buttons provide staff emergency end");
            s.ApplyStaffControls(0,false,false,.02f);s.Execute(new NaispoCommand{action="level",level=3});s.Execute(new NaispoCommand{action="start"});Advance(s,42);
            Check(!s.Guided&&!s.RouteControlled&&!motor.ComfortEnabled&&motor.WallWalkEnabled,"experienced level retains original free swing and wall climbing");
            s.Execute(new NaispoCommand{action="end"});s.Execute(new NaispoCommand{action="level",level=4,duration=120});
            Check(s.MixedReality&&s.Duration==60&&s.world.modes.IsMR&&motor.MovementLocked&&!s.world.input.WalkEnabled,"MR challenge uses physical coordinates and a fixed minute");
            s.Execute(new NaispoCommand{action="start"});Advance(s,42);
            var cargo=GameObject.CreatePrimitive(PrimitiveType.Cube);cargo.transform.position=Vector3.up*4;var ball=cargo.AddComponent<Rigidbody>();ball.isKinematic=true;
            var pickup=CityEnvironment.SpawnCrate(new Vector3(-8,1.2f,2),2,0);var web=s.world.shooter;
            pickup.transform.SetParent(null,true); // MR cargo belongs outside the hidden VR city.
            pickup.transform.localScale=Vector3.one*.42f;
            yield return null; // Decorative primitive colliders are destroyed at frame end.
            web.hand.SetPositionAndRotation(new Vector3(-8,1.2f,0),Quaternion.identity);web.AimTracked=true;Physics.SyncTransforms();
            Check(web.Fire(1),"MR easy pickup starts with an actual web hit");
            float pullBegin=Time.fixedTime;Vector3 handBegin=web.hand.position;
            while(Time.fixedTime<pullBegin+.25f){web.hand.position=handBegin-Vector3.forward*(Mathf.Clamp01((Time.fixedTime-pullBegin)/.25f)*.12f);yield return null;}
            until=Time.fixedTime+2.5f;while(Time.fixedTime<until)yield return null;
            Check(web.Carrying&&web.ObjectRopeLength<1,"one small pull automatically retrieves and holds the object");
            float carriedY=pickup.transform.position.y;web.hand.position+=Vector3.up*.3f;
            until=Time.fixedTime+.7f;while(Time.fixedTime<until)yield return null;
            Check(pickup.transform.position.y>carriedY+.1f,"held object follows a simple upward hand movement");web.Release();Object.Destroy(pickup);
            Check(!s.MRGame.TargetHit(ball,Vector3.forward*4),"unthrown contact cannot score");
            s.MRGame.MarkThrown(ball);Check(s.MRGame.TargetHit(ball,Vector3.forward*4)&&s.MRGame.Score==1,"a thrown object hitting a target scores one point");
            Check(!s.MRGame.TargetHit(ball,Vector3.forward*4)&&s.MRGame.Score==1,"bounces cannot score twice for the same throw");
            s.Execute(new NaispoCommand{action="pause"});s.MRGame.MarkThrown(ball);Check(!s.MRGame.TargetHit(ball,Vector3.forward*4),"paused MR round rejects throws and scores");
            s.Execute(new NaispoCommand{action="resume"});Advance(s,42);s.MRGame.MarkThrown(ball);s.MRGame.TargetHit(ball,Vector3.forward*4);Advance(s,610);
            Check(s.Phase==NaispoPhase.Finished&&s.MRGame.Score==2,"MR round ends after one minute and preserves final score");
            s.Execute(new NaispoCommand{action="prepare"});Check(s.MRGame.Score==0&&s.MRGame.Best>=2,"next MR visitor starts at zero while retaining best score");Object.Destroy(cargo);
            s.Execute(new NaispoCommand{action="end"});s.Execute(new NaispoCommand{action="level",level=1,duration=30});s.Execute(new NaispoCommand{action="start"});Advance(s,42);Advance(s,1210);yield return null;
            Check(s.Phase==NaispoPhase.Finished||s.Phase==NaispoPhase.Ending,"session time automatically ends guided experience");
            s.Execute(new NaispoCommand{action="end"});s.Execute(new NaispoCommand{action="prepare"});Check(s.Phase==NaispoPhase.Ready&&Vector3.Distance(motor.transform.position,s.RouteStart)<.01f,"next visitor preparation resets to the center start");
            s.world.input.head.localPosition=new Vector3(1.1f,1.65f,-.7f);s.world.input.head.localRotation=Quaternion.Euler(0,73,0);
            s.Execute(new NaispoCommand{action="start"});Advance(s,9);
            Check(Vector3.Dot(s.world.input.head.forward,Vector3.forward)>.999f&&Mathf.Abs(s.world.input.head.position.x)<.001f&&Mathf.Abs(s.world.input.head.position.z-14)<.001f,"new visitor facing and physical offset align to center route before countdown");
            s.Execute(new NaispoCommand{action="end"});s.world.input.head.localPosition=Vector3.up*1.65f;s.world.input.head.localRotation=Quaternion.identity;s.Execute(new NaispoCommand{action="prepare"});
            s.world.input.head.localRotation=Quaternion.identity;
            InteractionChecks.Preview(s.world.input.head.GetComponent<Camera>(),"Naispo-standby.png");
        }
    }
}
