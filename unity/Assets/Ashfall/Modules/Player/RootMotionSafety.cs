using Ashfall.Core;
namespace Ashfall.Modules.Player
{
    public static class RootMotionSafety
    {
        public static RootMotionProposal Plan(Float3 root, Float3 head, Float3 translation, float yaw, bool ownsTranslation)
        {
            if (!root.IsFinite || !head.IsFinite || !translation.IsFinite || !PlayerInput.Finite(yaw))
                throw new System.ArgumentException("root proposal");
            double x = (double)head.X - root.X, z = (double)head.Z - root.Z;
            double reduced = (double)yaw % (2 * System.Math.PI);
            double c = System.Math.Cos(reduced), s = System.Math.Sin(reduced);
            Float3 correction = new Float3((float)(x - (c * x + s * z)), 0, (float)(z - (-s * x + c * z)));
            if (!correction.IsFinite) throw new System.ArgumentException("pivot correction");
            return new RootMotionProposal { RootPosition = root, HeadPosition = head, Translation = translation,
                PivotCorrection = correction, YawRadians = yaw, OwnsTranslation = ownsTranslation };
        }
        // Called before rotating or moving. This validates the port boundary only;
        // the coordinator query must perform actual geometry sweeps/owner arbitration.
        public static RuleResult Approve(RootMotionProposal proposal, bool requireCollisionPort, IRootCollisionPreflight port)
        {
            if (!proposal.RootPosition.IsFinite || !proposal.HeadPosition.IsFinite ||
                !proposal.Translation.IsFinite || !proposal.PivotCorrection.IsFinite ||
                !PlayerInput.Finite(proposal.YawRadians)) return RuleResult.Reject("Nonfinite root motion");
            if (!new Float3(proposal.Translation.X + proposal.PivotCorrection.X,
                proposal.Translation.Y + proposal.PivotCorrection.Y, proposal.Translation.Z + proposal.PivotCorrection.Z).IsFinite)
                return RuleResult.Reject("Combined root motion overflow");
            // Driver adds translation+correction, then CharacterController adds that
            // displacement to the root. The yaw pivot keeps the head fixed before translation.
            if(!Destination(proposal.RootPosition.X,proposal.Translation.X+proposal.PivotCorrection.X)||
               !Destination(proposal.RootPosition.Y,proposal.Translation.Y+proposal.PivotCorrection.Y)||
               !Destination(proposal.RootPosition.Z,proposal.Translation.Z+proposal.PivotCorrection.Z)||
               !Destination(proposal.HeadPosition.X,proposal.Translation.X)||
               !Destination(proposal.HeadPosition.Y,proposal.Translation.Y)||
               !Destination(proposal.HeadPosition.Z,proposal.Translation.Z))return RuleResult.Reject("Root/head destination overflow");
            if (!proposal.OwnsTranslation && (proposal.Translation.X != 0 || proposal.Translation.Y != 0 || proposal.Translation.Z != 0))
                return RuleResult.Reject("External translation owner");
            if (port == null) return requireCollisionPort ? RuleResult.Reject("Collision preflight required") : RuleResult.Accept();
            return port.Validate(proposal);
        }
        private static bool Destination(float start,float displacement)
        {double value=(double)start+displacement;return value<=float.MaxValue&&value>=-float.MaxValue;}
    }
}
