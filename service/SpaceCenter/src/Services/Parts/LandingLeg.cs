using System;
using KRPC.Service.Attributes;
using KRPC.Utils;
using KRPC.SpaceCenter.ExtensionMethods;

namespace KRPC.SpaceCenter.Services.Parts
{
    /// <summary>
    /// Obtained by calling <see cref="Part.LandingLeg"/>.
    /// </summary>
    [KRPCClass (Service = "SpaceCenter")]
    public sealed class LandingLeg : Equatable<LandingLeg>
    {
        readonly Part part;
        readonly ModuleLandingLeg leg;

        internal static bool Is (Part part)
        {
            return part.InternalPart.HasModule<ModuleLandingLeg> ();
        }

        internal LandingLeg (Part part)
        {
            this.part = part;
            leg = part.InternalPart.Module<ModuleLandingLeg> ();
            if (leg == null)
                throw new ArgumentException ("Part is not a landing leg");
        }

        /// <summary>
        /// Check if the landing legs are equal.
        /// </summary>
        public override bool Equals (LandingLeg obj)
        {
            return part == obj.part && leg == obj.leg;
        }

        /// <summary>
        /// Hash the landing leg.
        /// </summary>
        public override int GetHashCode ()
        {
            return part.GetHashCode () ^ leg.GetHashCode ();
        }

        /// <summary>
        /// The part object for this landing leg.
        /// </summary>
        [KRPCProperty]
        public Part Part {
            get { return part; }
        }

        /// <summary>
        /// The current state of the landing leg.
        /// </summary>
        [KRPCProperty]
        public LandingLegState State {
            get {
                switch (leg.legState) {
                case ModuleLandingLeg.LegStates.DEPLOYED:
                    return LandingLegState.Deployed;
                case ModuleLandingLeg.LegStates.RETRACTED:
                    return LandingLegState.Retracted;
                case ModuleLandingLeg.LegStates.DEPLOYING:
                    return LandingLegState.Deploying;
                case ModuleLandingLeg.LegStates.RETRACTING:
                    return LandingLegState.Retracting;
                case ModuleLandingLeg.LegStates.BROKEN:
                    return LandingLegState.Broken;
                case ModuleLandingLeg.LegStates.REPAIRING:
                    return LandingLegState.Repairing;
                default:
                    throw new ArgumentException ("Unknown landing leg state");
                }
            }
        }

        /// <summary>
        /// Whether the landing leg is deployed.
        /// </summary>
        [KRPCProperty]
        public bool Deployed {
            get { return State == LandingLegState.Deployed; }
            set {
                if (value)
                    leg.LowerLeg ();
                else
                    leg.RaiseLeg ();
            }
        }
    }
}