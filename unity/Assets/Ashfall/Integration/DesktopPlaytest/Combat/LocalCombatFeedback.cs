using System;
namespace Ashfall.Integration.DesktopPlaytest.Combat
{
    // One local presentation lifetime, permanently bound to one actual producer.
    // Single main lane only. No subscriber callback can affect committed damage.
    public sealed class LocalCombatFeedback : IDisposable
    {
        readonly LocalRifleSandbox owner;
        long cursor;
        bool disposed;
        public LocalCombatFeedback(LocalRifleSandbox producer)
        { if(producer==null)throw new ArgumentNullException("producer");owner=producer;cursor=producer.FeedbackSequence; }
        public long DisposedThrough { get { return cursor; } }
        public bool Retired { get { return disposed||owner.FeedbackRetired; } }
        public void DiscardPending() { cursor=owner.FeedbackSequence; }
        public bool TryTake(bool suppressed,out LocalRifleSandbox.FeedbackReceipt receipt)
        {
            receipt=null;
            if(disposed||owner.FeedbackRetired||suppressed){DiscardPending();return false;}
            var read=owner.ReadFeedback(cursor,out receipt);
            if(read==LocalRifleSandbox.FeedbackRead.Gap){DiscardPending();receipt=null;return false;}
            if(read!=LocalRifleSandbox.FeedbackRead.Available)return false;
            cursor=receipt.Sequence;return true;
        }
        public void Dispose() { disposed=true;DiscardPending(); }
    }
}
