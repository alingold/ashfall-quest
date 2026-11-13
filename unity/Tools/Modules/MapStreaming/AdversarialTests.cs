using System;
using System.IO;
using Ashfall.Modules.MapStreaming;
class AdversarialTests : ITriangleSink
{
    public Action Callback;
    public void Accept(string id,TriangleBatch batch){if(Callback!=null)Callback();}
    static int checks;
    static void Check(bool v,string s){checks++;if(!v)throw new Exception(s);}
    static void Main(string[] args)
    {
        byte[] valid=File.ReadAllBytes(args[0]);using(var map=MapStream.OpenPilot(valid,MapStream.Hash(valid))) {
            bool lockDenied=false;try{using(var f=new FileStream(args[1],FileMode.Open,FileAccess.Write,FileShare.ReadWrite)){} }catch(IOException){lockDenied=true;}Check(lockDenied,"verified source handle blocks writing");
            var sink=new AdversarialTests();bool nested=false,dispose=false;
            sink.Callback=()=>{try{map.StreamAll(sink,1);}catch(InvalidDataException){nested=true;}try{map.Dispose();}catch(InvalidDataException){dispose=true;}};
            map.StreamAll(sink,1);Check(nested&&dispose,"reentry/dispose in callback rejected");
            sink.Callback=()=>{throw new OperationCanceledException();};bool cancelled=false;try{map.StreamAll(sink,1);}catch(OperationCanceledException){cancelled=true;}Check(cancelled,"caller cancellation propagated");
            sink.Callback=null;map.StreamAll(sink,1);Check(true,"stream reusable after cancellation");
        }
        for(int i=2;i<args.Length;i++) {bool rejected=false;try{byte[] b=File.ReadAllBytes(args[i]);using(var map=MapStream.OpenPilot(b,MapStream.Hash(b)))map.StreamAll(new AdversarialTests(),1);}catch(InvalidDataException){rejected=true;}Check(rejected,args[i]);Console.WriteLine("REJECT "+Path.GetFileName(args[i]));}
        Console.WriteLine("PASS "+checks+" adversarial assertions");
    }
}
