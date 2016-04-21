using System;
using UnityEngine;
using KRPC.Service.Attributes;
using KRPC.SpaceCenter.ExtensionMethods;
using KRPC.Utils;
using Tuple3 = KRPC.Utils.Tuple<double, double, double>;
using Tuple4 = KRPC.Utils.Tuple<double, double, double, double>;

namespace KRPC.SpaceCenter.Services.Parts
{
    /// <summary>
    /// The component of an <see cref="Engine"/> or <see cref="RCS"/> part that generates thrust.
    /// Can obtained by calling <see cref="Engine.Thrusters"/> or <see cref="RCS.Thrusters"/>.
    /// </summary>
    /// <remarks>
    /// Engines can consist of multiple thrusters.
    /// For example, the S3 KS-25x4 "Mammoth" has four rocket nozzels, and so consists of four thrusters.
    /// </remarks>
    [KRPCClass (Service = "SpaceCenter")]
    public sealed class Thruster : Equatable<Thruster>
    {
        readonly Part part;
        readonly ModuleEngines engine;
        readonly ModuleRCS rcs;
        readonly ModuleGimbal gimbal;
        readonly int transformIndex;

        internal Thruster (Part part, ModuleEngines engine, ModuleGimbal gimbal, int transformIndex)
        {
            this.part = part;
            this.engine = engine;
            this.gimbal = gimbal;
            this.transformIndex = transformIndex;
        }

        internal Thruster (Part part, ModuleRCS rcs, int transformIndex)
        {
            this.part = part;
            this.rcs = rcs;
            this.transformIndex = transformIndex;
        }

        /// <summary>
        /// Check the thrusters are equal.
        /// </summary>
        public override bool Equals (Thruster obj)
        {
            return part == obj.part && transformIndex == obj.transformIndex;
        }

        /// <summary>
        /// Hash the thruster.
        /// </summary>
        public override int GetHashCode ()
        {
            return part.GetHashCode () ^ transformIndex.GetHashCode ();
        }

        /// <summary>
        /// The <see cref="Part"/> that contains this thruster.
        /// </summary>
        [KRPCProperty]
        public Part Part {
            get { return part; }
        }

        /// <summary>
        /// The position at which the thruster generates thrust, in the given reference frame.
        /// For gimballed engines, this takes into account the current rotation of the gimbal.
        /// </summary>
        /// <param name="referenceFrame"></param>
        [KRPCMethod]
        public Tuple3 ThrustPosition (ReferenceFrame referenceFrame)
        {
            return referenceFrame.PositionFromWorldSpace (WorldTransform.position).ToTuple ();
        }

        /// <summary>
        /// The direction of the force generated by the thruster, in the given reference frame.
        /// This is opposite to the direction in which the thruster expels propellant.
        /// For gimballed engines, this takes into account the current rotation of the gimbal.
        /// </summary>
        /// <param name="referenceFrame"></param>
        [KRPCMethod]
        public Tuple3 ThrustDirection (ReferenceFrame referenceFrame)
        {
            return referenceFrame.DirectionFromWorldSpace (WorldThrustDirection).ToTuple ();
        }

        /// <summary>
        /// The position at which the thruster generates thrust, when the engine is in its
        /// initial position (no gimballing), in the given reference frame.
        /// </summary>
        /// <param name="referenceFrame"></param>
        /// <remarks>
        /// This position can move when the gimbal rotates. This is because the thrust position and
        /// gimbal position are not necessarily the same.
        /// </remarks>
        [KRPCMethod]
        public Tuple3 InitialThrustPosition (ReferenceFrame referenceFrame)
        {
            StashGimbalRotation ();
            var position = WorldTransform.position;
            RestoreGimbalRotation ();
            return referenceFrame.PositionFromWorldSpace (position).ToTuple ();
        }

        /// <summary>
        /// The direction of the force generated by the thruster, when the engine is in its
        /// initial position (no gimballing), in the given reference frame.
        /// This is opposite to the direction in which the thruster expels propellant.
        /// </summary>
        /// <param name="referenceFrame"></param>
        [KRPCMethod]
        public Tuple3 InitialThrustDirection (ReferenceFrame referenceFrame)
        {
            StashGimbalRotation ();
            var direction = WorldThrustDirection;
            RestoreGimbalRotation ();
            return referenceFrame.DirectionFromWorldSpace (direction).ToTuple ();
        }

        /// <summary>
        /// A reference frame that is fixed relative to the thruster and orientated with
        /// its thrust direction (<see cref="ThrustDirection"/>).
        /// For gimballed engines, this takes into account the current rotation of the gimbal.
        /// <list type="bullet">
        /// <item><description>
        /// The origin is at the position of thrust for this thruster (<see cref="ThrustPosition"/>).
        /// </description></item>
        /// <item><description>
        /// The axes rotate with the thrust direction.
        /// This is the direction in which the thruster expels propellant, including any gimballing.
        /// </description></item>
        /// <item><description>The y-axis points along the thrust direction.</description></item>
        /// <item><description>The x-axis and z-axis are perpendicular to the thrust direction.</description></item>
        /// </list>
        /// </summary>
        [KRPCProperty]
        public ReferenceFrame ThrustReferenceFrame {
            get { return ReferenceFrame.Thrust (this); }
        }

        /// <summary>
        /// Whether the thruster is gimballed.
        /// </summary>
        [KRPCProperty]
        public bool Gimballed {
            get { return gimbal != null; }
        }

        void CheckGimballed ()
        {
            if (!Gimballed)
                throw new InvalidOperationException ("The engine is not gimballed");
        }

        /// <summary>
        /// Position around which the gimbal pivots.
        /// </summary>
        [KRPCMethod]
        public Tuple3 GimbalPosition (ReferenceFrame referenceFrame)
        {
            CheckGimballed ();
            return referenceFrame.PositionFromWorldSpace (gimbal.gimbalTransforms [transformIndex].position).ToTuple ();
        }

        /// <summary>
        /// The current gimbal angle in the pitch, yaw and roll axes.
        /// </summary>
        [KRPCProperty]
        public Tuple3 GimbalAngle {
            get {
                CheckGimballed ();
                return new Vector3d (
                    gimbal.gimbalAnglePitch,
                    gimbal.gimbalAngleYaw,
                    gimbal.gimbalAngleRoll)
                        .ToTuple ();
            }
        }

        /// <summary>
        /// Transform of the thrust vector in world space
        /// </summary>
        internal Transform WorldTransform {
            get { return (engine != null ? engine.thrustTransforms : rcs.thrusterTransforms) [transformIndex]; }
        }

        /// <summary>
        /// The direction of the thrust vector in world space
        /// </summary>
        internal Vector3d WorldThrustDirection {
            get { return (rcs != null && !rcs.useZaxis) ? -WorldTransform.up : -WorldTransform.forward; }
        }

        /// <summary>
        /// A direction perpendicular to <see cref="WorldThrustDirection"/>
        /// </summary>
        internal Vector3d WorldThrustPerpendicularDirection {
            get { return WorldTransform.right; }
        }

        Quaternion savedRotation;

        /// <summary>
        /// Save the gimbal rotation and set it to its initial position
        /// </summary>
        void StashGimbalRotation ()
        {
            savedRotation = gimbal.gimbalTransforms [transformIndex].localRotation;
            gimbal.gimbalTransforms [transformIndex].localRotation = gimbal.initRots [transformIndex];
        }

        /// <summary>
        /// Restore the previously saved gimbal rotation
        /// </summary>
        void RestoreGimbalRotation ()
        {
            gimbal.gimbalTransforms [transformIndex].localRotation = savedRotation;
        }
    }
}