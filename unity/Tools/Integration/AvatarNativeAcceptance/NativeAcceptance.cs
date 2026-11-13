#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Ashfall.Core;
using Ashfall.Modules.Avatar;
using Ashfall.Modules.Player;
using Ashfall.Modules.Sessions;
using Ashfall.Integration.AvatarPose;
using UnityEngine;

// Root-only staged gates. Historical reference hands, synthetic triangle body and
// synthetic 2x2 materials. No approved export, physical tracking or rendered receipt.
public static class AvatarNativeAcceptanceR7
{
    public static UnityPlayerDriver CreateSyntheticRig(string name)
    {
        var host=new GameObject(name);host.SetActive(false);
        try {
            var cc=host.AddComponent<CharacterController>();cc.radius=.25f;cc.height=1.7f;cc.center=new Vector3(0,.85f,0);cc.skinWidth=.02f;
            var space=new GameObject("SyntheticTrackingSpace").transform;space.SetParent(host.transform,false);
            var head=new GameObject("SyntheticHead").transform;head.SetParent(space,false);head.localPosition=new Vector3(0,1.6f,0);
            var left=new GameObject("SyntheticLeft").transform;left.SetParent(space,false);left.localPosition=new Vector3(-.3f,1.1f,.35f);
            var right=new GameObject("SyntheticRight").transform;right.SetParent(space,false);right.localPosition=new Vector3(.3f,1.1f,.35f);
            var driver=host.AddComponent<UnityPlayerDriver>();driver.TrackingSpace=space;driver.Head=head;driver.LeftHand=left;driver.RightHand=right;
            driver.UseXr=false;driver.ExclusivePoseWriterConfirmed=true;driver.Paused=true;driver.BindLifecycle("r7","actor",0);
            host.SetActive(true);Need(driver.isActiveAndEnabled,"synthetic rig Awake admission");return driver;
        }catch{UnityEngine.Object.DestroyImmediate(host);throw;}
        // Root explicitly binds actual approved geometry/motor after construction.
    }
    static void Need(bool ok,string why) { if(!ok)throw new Exception("R7: "+why); }
    static bool Hidden(AvatarView v) { foreach(var r in v.GetComponentsInChildren<Renderer>(true))if(r.enabled&&r.gameObject.activeInHierarchy)return false;return true; }
    static int[] Census() { return new[]{Resources.FindObjectsOfTypeAll<Mesh>().Length,Resources.FindObjectsOfTypeAll<Material>().Length,Resources.FindObjectsOfTypeAll<Texture2D>().Length}; }
    static void Same(int[] expected) { var n=Census();for(int i=0;i<n.Length;i++)Need(n[i]==expected[i],"resource census "+i); }
    static AvatarRenderReceipts BadReceipts(AvatarAssembly a)
    {var material=AvatarR4Fixtures.Material(a);return new AvatarRenderReceipts(new Dictionary<string,ReceiptSkinDependencies>(),new Dictionary<string,HandMaterialReceipt>{{"LeftHand",material},{"WrongHand",material}});}
    static void Reject(Action action) { bool rejected=false;try{action();}catch(InvalidOperationException){rejected=true;}catch(AvatarValidationException){rejected=true;}Need(rejected,"expected native refusal"); }
    sealed class Contacts:IGripContactSource
    { public bool TryRead(bool left,out GripContact c) { c=GripContact.Free;return true; } }
    sealed class SyntheticBody:IBodyPoseSource
    {
        public string AssemblyHash{get;private set;}public string ProfileHash{get;private set;}
        public SyntheticBody(AvatarAssembly a,SourceBodyProfile p) { AssemblyHash=a.ContentSha256;ProfileHash=p.SourceSha256; }
        public bool TrySample(UnityPlayerDriver d,FrozenSkinPose l,FrozenSkinPose r,float delta,out float[][] body)
        { body=new float[13][];for(int i=0;i<13;i++)body[i]=NativePoseBinding.Values(Matrix4x4.Translate(d.transform.position));return true; }
    }
    sealed class Fixture:IDisposable
    {
        public readonly GameObject Host;public readonly AvatarView View;
        public readonly AvatarAssembly Assembly;public readonly AvatarManifest Manifest;public readonly SourceBodyProfile Profile;
        readonly NativePoseBinding binding;readonly VerifiedSkin left,right;
        public readonly Session Session=AcceptanceCpu.NewSession();public string Reconnect;
        public Fixture()
        {
            AvatarR4Fixtures.Source bytes;Manifest=AvatarR4Fixtures.Manifest(out bytes);
            Assembly=AvatarAssembler.Prepare(Manifest,bytes,null,AvatarUse.UnreviewedPilot);
            Profile=SourceBodyProfile.QuestFixed("camo",new string('a',64));
            foreach(var part in Assembly.CopyParts()){if(part.Label=="LeftHand")left=VerifiedSkinReader.Read(part);if(part.Label=="RightHand")right=VerifiedSkinReader.Read(part);}
            Need(!Assembly.ProductionApproved,"fixture must stay quarantined");
            Need(Session.Join(AcceptanceCpu.Context("actor"),new JoinRequest(Protocol.Name,1,"synthetic","actor",null,false),out Reconnect).Accepted,"fixture join");
            Host=new GameObject("R7-synthetic-avatar");View=Host.AddComponent<AvatarView>();binding=new NativePoseBinding(View);
        }
        public NativePoseBinding.Provider Bind(UnityPlayerDriver d,AvatarRenderReceipts receipts)
        {
            var id=new PoseIdentity("r7","actor","actor",0,7,7,Assembly,Manifest,Profile);
            return binding.Bind(d,Session,id,Assembly,Profile,left,right,AvatarR4Fixtures.Grip(left,"left"),AvatarR4Fixtures.Grip(right,"right"),new SyntheticBody(Assembly,Profile),new Contacts(),receipts,true);
        }
        public NativePoseBinding.Provider Bind(UnityPlayerDriver d) { return Bind(d,AvatarR4Fixtures.Receipts(Assembly)); }
        public void Dispose() { if(Host!=null)UnityEngine.Object.DestroyImmediate(Host); }
    }
    // Root supplies two distinct, active, synthetic desktop rigs with actual motors and
    // real approved geometry. No successful collision/authority callback is fabricated.
    // Root must suspend its own gameplay dispatch while running these explicit pumps.
    public static IEnumerator PlayMode(UnityPlayerDriver driver,UnityPlayerDriver other,RootMotionProposal zeroDisplacement)
    {
        Need(Application.isPlaying,"real PlayMode required");
        Need(driver!=other&&!driver.UseXr&&!other.UseXr,"distinct synthetic desktop rigs");
        Need(driver.Motor is UnityExclusivePlayerMotor&&other.Motor is UnityExclusivePlayerMotor,"real installed motor implementations required");
        Need(zeroDisplacement.Translation.X==0&&zeroDisplacement.Translation.Y==0&&zeroDisplacement.Translation.Z==0&&zeroDisplacement.YawRadians==0,"zero displacement holdout");
        // Warm shader/decoder paths before measuring owned object release.
        using(var warm=new Fixture()){warm.Bind(driver).Dispose();}
        yield return null;yield return null;var baseline=Census();
        var fixture=new Fixture();NativePoseBinding.Provider old=null,current=null;GameObject schedulerHost=null;
        IExclusivePlayerMotor restoreMotor=null,fixtureSubstitute=null;
        bool wasPaused=driver.Paused;
        try
        {
            driver.Paused=true;
            current=fixture.Bind(driver);yield return null;
            Need(current.Pump(.016f,true)&&!Hidden(fixture.View),"initial real presenter");
            var root=driver.transform.localToWorldMatrix;var head=driver.Head.localToWorldMatrix;
            var left=driver.LeftHand.localToWorldMatrix;var right=driver.RightHand.localToWorldMatrix;
            yield return null;Need(current.Pump(.016f,true),"repeat pump");
            Need(root==driver.transform.localToWorldMatrix&&head==driver.Head.localToWorldMatrix&&left==driver.LeftHand.localToWorldMatrix&&right==driver.RightHand.localToWorldMatrix,"presentation never writes authoritative transforms");
            // Failed replacement allocates rigid body parts before a missing hand receipt.
            yield return null;int children=fixture.Host.transform.childCount;var census=Census();
            Reject(delegate{fixture.Bind(driver,BadReceipts(fixture.Assembly));});
            Need(current.RefreshLifetime()&&!Hidden(fixture.View),"failed replacement preserves provider and visible prior frame");
            yield return null;yield return null;Same(census);Need(fixture.Host.transform.childCount==children,"failed replacement rolls back hierarchy");
            var field=typeof(UnityPlayerDriver).GetField("PoseSourceInvalidated",BindingFlags.Instance|BindingFlags.NonPublic);
            var retired=(Action)field.GetValue(driver);Need(retired!=null,"captured actual subscribed callback");
            old=current;current=fixture.Bind(driver);yield return null;Need(current.Pump(.016f,true),"provider replacement");
            retired();old.Dispose();Need(!Hidden(fixture.View)&&current.RefreshLifetime(),"retired callback/dispose lacks replacement authority");
            schedulerHost=new GameObject("R7-scheduler");var scheduler=schedulerHost.AddComponent<NativePoseScheduler>();
            var foreign=other.Motor;Reject(delegate{scheduler.Install(other,current);});
            Need(System.Object.ReferenceEquals(foreign,other.Motor)&&current.RefreshLifetime(),"scheduler mismatch has no partial installation");
            var actual=driver.Motor;restoreMotor=actual;
            // The successful scheduler observation needs its own Unity frame;
            // the provider above already Pumped in the previous one.
            yield return null;scheduler.Install(driver,current);
            var result=driver.Motor.Apply(zeroDisplacement,.016f);Need(result.Complete&&result.Velocity.IsFinite,"real motor successful zero proposal");
            Need(Hidden(fixture.View),"post-motor observer hides until LateUpdate");
            yield return new WaitForEndOfFrame();Need(!Hidden(fixture.View),"actual LateUpdate schedules successful motor result");
            yield return null;result=driver.Motor.Apply(zeroDisplacement,float.NaN);
            Need(!result.Complete,"real motor rejects invalid timestep");
            yield return new WaitForEndOfFrame();Need(Hidden(fixture.View),"failed actual motor frame cannot publish even while paused");
            fixtureSubstitute=other.Motor;driver.Motor=fixtureSubstitute;yield return null;yield return new WaitForEndOfFrame();
            Need(Hidden(fixture.View)&&!current.RefreshLifetime(),"motor replacement retires scheduler/provider");
            Need(System.Object.ReferenceEquals(driver.Motor,other.Motor),"retired scheduler preserves newer motor owner");
            driver.Motor=actual;fixtureSubstitute=null;UnityEngine.Object.Destroy(schedulerHost);
            current=fixture.Bind(driver);schedulerHost=new GameObject("R7-reconnected-scheduler");scheduler=schedulerHost.AddComponent<NativePoseScheduler>();scheduler.Install(driver,current);
            string next;old=current;
            Need(fixture.Session.Disconnect(AcceptanceCpu.Context("actor"),false).Accepted,"disconnect");
            Need(fixture.Session.Join(AcceptanceCpu.Context("actor"),new JoinRequest(Protocol.Name,1,"synthetic","actor",fixture.Reconnect,false),out next).Accepted,"same actor reconnect without capture");
            Need(!scheduler.MembershipPublished()&&Hidden(fixture.View)&&!old.RefreshLifetime(),"membership hook retires unobserved reconnect");
            scheduler.Dispose();Need(System.Object.ReferenceEquals(actual,driver.Motor),"scheduler restores exact motor");
            UnityEngine.Object.Destroy(schedulerHost);schedulerHost=null;
            current=fixture.Bind(driver);yield return null;Need(current.Pump(.016f,true),"fresh connection binding");
            old.Dispose();Need(!Hidden(fixture.View),"old connection disposal cannot hide replacement");
            driver.Paused=true;yield return null;Need(current.Pump(.016f,true),"pause preserves physical presentation");driver.Paused=wasPaused;
            driver.enabled=false;Need(Hidden(fixture.View),"disable hides synchronously");driver.enabled=true;
            Need(!current.RefreshLifetime(),"disable/enable cannot revive provider");
            current=fixture.Bind(driver);yield return null;Need(current.Pump(.016f,true),"fresh enabled provider");
            driver.UseXr=true;Need(Hidden(fixture.View)&&!current.RefreshLifetime(),"source switch invalidates immediately");driver.UseXr=false;
            current=fixture.Bind(driver);yield return null;Need(current.Pump(.016f,true),"fresh desktop provider");
            Need(!current.Pump(.016f,true)&&Hidden(fixture.View),"same-frame duplicate Pump refuses and hides");
            yield return null;Need(current.Pump(.016f,true),"duplicate Pump recovery in a fresh frame");
            fixture.View.enabled=false;Need(!current.RefreshLifetime()&&Hidden(fixture.View),"view disable retires provider");
            fixture.View.enabled=true;current=fixture.Bind(driver);yield return null;Need(current.Pump(.016f,true),"view reenable new resources");
            UnityEngine.Object.Destroy(fixture.View);yield return null;yield return null;
            Need(!current.RefreshLifetime(),"destroyed presenter retires provider");
        }
        finally {if(old!=null)old.Dispose();if(current!=null)current.Dispose();if(fixtureSubstitute!=null&&System.Object.ReferenceEquals(driver.Motor,fixtureSubstitute))driver.Motor=restoreMotor;if(schedulerHost!=null)UnityEngine.Object.Destroy(schedulerHost);fixture.Dispose();driver.Paused=wasPaused;driver.UseXr=false;}
        yield return null;yield return null;Same(baseline);
        Debug.Log("R7 PlayMode synthetic lifecycle gate PASS; physical/render/export acceptance OPEN");
    }
    public static void EditorLifetime()
    {
        Need(!Application.isPlaying,"Editor gate required");var baseline=Census();
        var foreignMesh=new Mesh();var foreignTexture=new Texture2D(2,2);
        try {using(var f=new Fixture()) {
            f.View.Assemble(f.Assembly,true,AvatarR4Fixtures.Receipts(f.Assembly));var current=Census();
            Reject(delegate{f.View.Assemble(f.Assembly,true,BadReceipts(f.Assembly));});Same(current);
            f.View.enabled=false;f.View.enabled=true;
            f.View.Assemble(f.Assembly,true,AvatarR4Fixtures.Receipts(f.Assembly));
            f.View.Dispose();f.View.Dispose();Need(foreignMesh!=null&&foreignTexture!=null,"foreign ownership preserved");
        }}finally{UnityEngine.Object.DestroyImmediate(foreignMesh);UnityEngine.Object.DestroyImmediate(foreignTexture);}
        Same(baseline);Debug.Log("R7 Editor ownership/rollback gate PASS");
    }
#if UNITY_EDITOR
    const string ReloadKey="Ashfall.R7.NativeAcceptance.Reload";
    [Serializable]sealed class ReloadReceipt {public string Host,Token;public string[] Resources;}
    static bool CanonicalEntityId(string text)
    {long id;return text!=null&&Int64.TryParse(text,System.Globalization.NumberStyles.Integer,System.Globalization.CultureInfo.InvariantCulture,out id)&&id!=0&&id.ToString(System.Globalization.CultureInfo.InvariantCulture)==text;}
    public static void BeforeDomainReload()
    {
        Need(!Application.isPlaying,"Editor reload only");Need(UnityEditor.SessionState.GetString(ReloadKey,"")=="","unconsumed reload receipt");
        var f=new Fixture();try{f.View.Assemble(f.Assembly,true,AvatarR4Fixtures.Receipts(f.Assembly));
        string token=Guid.NewGuid().ToString("N");f.Host.name="R7-reload-"+token;
        var owned=(List<UnityEngine.Object>)typeof(AvatarView).GetField("resources",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f.View);
        var ids=new List<string>();foreach(var o in owned)ids.Add(o.GetEntityId().ToString());Need(ids.Count>0,"actual native ownership ledger");
        string hostId=f.Host.GetEntityId().ToString();var unique=new HashSet<string>(StringComparer.Ordinal);
        Need(CanonicalEntityId(hostId)&&ids.Count<=4096,"unsupported reload identity format/count");
        foreach(string id in ids)Need(CanonicalEntityId(id)&&id!=hostId&&unique.Add(id),"invalid/duplicate reload resource identity");
        UnityEditor.SessionState.SetString(ReloadKey,JsonUtility.ToJson(new ReloadReceipt{Host=hostId,Token=token,Resources=ids.ToArray()}));
        // Root requests actual reload via its serialized engine lane, then calls After.
        }catch{f.Dispose();throw;}
    }
    public static void AfterDomainReload()
    {
        var saved=UnityEditor.SessionState.GetString(ReloadKey,"");Need(saved.Length>0,"missing before-reload receipt");
        var receipt=JsonUtility.FromJson<ReloadReceipt>(saved);Guid token;
        Need(receipt!=null&&CanonicalEntityId(receipt.Host)&&Guid.TryParseExact(receipt.Token,"N",out token)&&receipt.Token==token.ToString("N")&&receipt.Resources!=null&&receipt.Resources.Length>0&&receipt.Resources.Length<=4096,"invalid before-reload ownership receipt");
        var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(string id in receipt.Resources)Need(CanonicalEntityId(id)&&id!=receipt.Host&&ids.Add(id),"invalid/duplicate reload resource identity");
        foreach(var o in Resources.FindObjectsOfTypeAll<UnityEngine.Object>())Need(!ids.Contains(o.GetEntityId().ToString()),"owned resource survived reload");
        GameObject ownedHost=null;
        foreach(var host in Resources.FindObjectsOfTypeAll<GameObject>())if(host.GetEntityId().ToString()==receipt.Host){Need(ownedHost==null&&host.name=="R7-reload-"+receipt.Token&&host.GetComponent<AvatarView>()!=null,"fixture host identity mismatch");ownedHost=host;}
        Need(ownedHost!=null,"fixture host absent after reload; refusing name-based cleanup");
        Need(ownedHost.transform.childCount==0,"fixture host retains descendants; refusing recursive cleanup");
        UnityEngine.Object.DestroyImmediate(ownedHost);
        UnityEditor.SessionState.EraseString(ReloadKey);Debug.Log("R7 real domain reload gate PASS");
    }
#endif
}
#endif

