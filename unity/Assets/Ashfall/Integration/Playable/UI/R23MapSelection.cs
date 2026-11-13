using System;
using System.Collections.Generic;

namespace Ashfall.Integration.Playable.UI
{
    public sealed class R23MapEntry
    {
        public readonly string Id;
        public readonly bool GameSpawn;
        public R23MapEntry(string id,bool gameSpawn){Id=id;GameSpawn=gameSpawn;}
    }

    // Pending menu choice is separate from physical map entry. No loading or world ownership.
    public sealed class R23MapSelection
    {
        R23MapOption[] options=new R23MapOption[0];
        public R23MapOption Selected {get;private set;}
        public bool ChoosingSpawn {get;private set;}
        public int Count {get{return options.Length;}}
        public R23MapOption Option(int index){return options[index];}
        public void SetOptions(IEnumerable<R23MapOption> values)
        {
            var next=new List<R23MapOption>();var ids=new HashSet<string>(StringComparer.Ordinal);
            if(values!=null)foreach(var option in values)
            {if(option==null||!ids.Add(option.Id))throw new ArgumentException("Maps require unique nonnull options");next.Add(option);}
            string previous=Selected==null?null:Selected.Id;options=next.ToArray();Selected=null;
            foreach(var option in options)if(option.Id==previous){Selected=option;break;}
            if(Selected==null){Selected=options.Length==0?null:options[0];CancelSpawn();}
        }
        public bool Select(string id)
        {
            foreach(var option in options)if(option.Id==id){Selected=option;CancelSpawn();return true;}
            return false;
        }
        // Canonical Unity map choice is always lobby-first; rules launch separately.
        public R23MapEntry LobbyEntry(){if(Selected==null)return null;CancelSpawn();return new R23MapEntry(Selected.Id,false);}
        public bool BeginSpawn(){ChoosingSpawn=Selected!=null;return ChoosingSpawn;}
        public void CancelSpawn(){ChoosingSpawn=false;}
        public R23MapEntry ConfirmSpawn(bool gameSpawn)
        {
            if(!ChoosingSpawn||Selected==null)return null;
            ChoosingSpawn=false;return new R23MapEntry(Selected.Id,gameSpawn);
        }
    }
}
