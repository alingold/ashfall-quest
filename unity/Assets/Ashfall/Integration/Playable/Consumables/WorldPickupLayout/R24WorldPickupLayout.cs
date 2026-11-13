using System;
using Ashfall.Core;
namespace Ashfall.Integration.Playable.Consumables.WorldPickupLayout
{
    public sealed class R24CheckpointPickup
    {
        public readonly string Id,Kind;public readonly int SourceIndex;public readonly bool IsSupply;public readonly Float3 UnityOffset;
        internal R24CheckpointPickup(string id,string kind,int index,bool supply,Float3 offset){Id=id;Kind=kind;SourceIndex=index;IsSupply=supply;UnityOffset=offset;}
        public Float3 At(Float3 checkpointUnity){if(!checkpointUnity.IsFinite)throw new ArgumentException("Actual finite checkpoint required");return new Float3(checkpointUnity.X+UnityOffset.X,checkpointUnity.Y+UnityOffset.Y,checkpointUnity.Z+UnityOffset.Z);}
    }
    public enum R24PickupSettleOutcome { Pending,Settled,UnchangedNoHitOrMesh,Cancelled,Refused,Preserved,Unavailable,WallMounted,ArchiveAssigned }
    public sealed class R24PickupSettleResult
    {
        public readonly string Id;public readonly R24PickupSettleOutcome Outcome;public readonly Float3 RequestedPosition,Position;
        public R24PickupSettleResult(string id,R24PickupSettleOutcome outcome,Float3 requestedPosition,Float3 position){Id=id;Outcome=outcome;RequestedPosition=requestedPosition;Position=position;}
    }
    public sealed class R24PickupLayoutResult
    {
        public readonly int Created,Relocated,Preserved;
        public R24PickupLayoutResult(int created,int relocated,int preserved){Created=created;Relocated=relocated;Preserved=preserved;}
    }
    public static class R24WorldPickupLayout
    {
        // field_consumables.reset_supplies: source KINDS order, not catalog order.
        public static R24CheckpointPickup[] FoodsAndBackpacks()
        {
            var rows=new R24CheckpointPickup[12];var kinds=new[]{"shaker","soda","banana"};
            for(int i=0;i<12;i++)
            {
                string kind=i<9?kinds[i%3]:"backpack";
                // Keep the three existing real starter action IDs; additional source
                // instances have independent stable IDs, never a cloned owner alias.
                string id=i<3?"r23-starter/"+kind:i<9?"r24-checkpoint/food/"+i:"r24-checkpoint/backpack/"+i;
                rows[i]=new R24CheckpointPickup(id,kind,i,i>=9,new Float3((float)(-3.4+(i%4)*.65),.75f,-(float)(1.8+(i/4)*.65)));
            }
            return rows;
        }
        public static R24CheckpointPickup[] Ammo()
        {
            var families=new[]{"rifle","pistol","shells","smg","sniper","builds"};var rows=new R24CheckpointPickup[6];
            for(int i=0;i<rows.Length;i++)
            {
                // main.gd first3; ammo_pickups.setup fallback uses FIRST position+i*.55.
                float x=i<3?1.5f+i*.5f:1.5f+(float)(i*.55);
                rows[i]=new R24CheckpointPickup("r24-checkpoint/ammo/"+families[i],families[i],i,true,new Float3(x,.3f,2));
            }
            return rows;
        }
        public const double SourceHoverHeight=.75-.3048;
        public const float HoverHeight=(float)SourceHoverHeight,RayUp=2,RayLength=20;
        public static bool TryLowestCornerY(Float3 min,Float3 max,float yFromX,float yFromY,float yFromZ,float yOffset,out float lowest)
        {
            lowest=Single.PositiveInfinity;if(!min.IsFinite||!max.IsFinite||min.X>max.X||min.Y>max.Y||min.Z>max.Z)return false;
            if(!new Float3(yFromX,yFromY,yFromZ).IsFinite||Single.IsNaN(yOffset)||Single.IsInfinity(yOffset))return false;
            for(int i=0;i<8;i++){float y=yFromX*((i&1)==0?min.X:max.X)+yFromY*((i&2)==0?min.Y:max.Y)+yFromZ*((i&4)==0?min.Z:max.Z)+yOffset;if(Single.IsNaN(y)||Single.IsInfinity(y))return false;lowest=Math.Min(lowest,y);}return true;
        }
        public static bool TrySettleY(float originalY,float hitY,float visibleLowestY,out float settledY)
        {
            settledY=originalY;if(Single.IsNaN(originalY)||Single.IsInfinity(originalY)||Single.IsNaN(hitY)||Single.IsInfinity(hitY)||Single.IsNaN(visibleLowestY)||Single.IsInfinity(visibleLowestY))return false;
            settledY=(float)((double)originalY+hitY+SourceHoverHeight-visibleLowestY);return true;
        }
    }
}
