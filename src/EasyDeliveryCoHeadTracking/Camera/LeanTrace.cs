using CameraUnlock.Core.Data;
using CameraUnlock.Core.Processing;
using UnityEngine;

namespace EasyDeliveryCoHeadTracking.Camera
{
    /// <summary>
    /// The engine half of the lean clamp: asks Unity's physics what lies between the clean eye
    /// and where the lean wants to put it. Core's LeanClamp, run by the camera controller, owns
    /// what is done with the answer.
    ///
    /// Two casts, because each misses something the other catches. A sphere of the standoff's
    /// radius swept along the lean catches a door frame's edge or a shelf's lip the eye would
    /// pass beside; Unity leaves out every collider the sphere already overlaps where it
    /// starts, which also hides a surface already within the standoff of the eye. A line down
    /// the centre of the lean sees that surface, because a ray only skips a collider its origin
    /// is inside, and for a flat surface met at an angle it gives the exact travel that holds
    /// the eye the standoff off it.
    ///
    /// Distances come back in core's convention: the travel the eye may make plus the
    /// standoff, which the clamp takes off again as its skin.
    /// </summary>
    public sealed class LeanTrace
    {
        // Holding the eye r off a flat surface met at an angle means stopping r / cos short of
        // it along the lean, which is unbounded at grazing incidence. 0.25 is 75 degrees off the
        // normal, past which the eye slides along the surface rather than into it.
        private const float MinApproachCosine = 0.25f;

        // A turned head can put a corner of the near clip plane, not its centre, nearest the
        // wall, and geometry inside the near plane is culled.
        private const float NearPlaneStandoffFactor = 1.25f;

        private readonly float _configuredStandoff;
        private readonly int _mask;

        public LeanTrace(float standoff, int mask)
        {
            _configuredStandoff = standoff;
            _mask = mask;
            Query = Trace;
        }

        /// <summary>Held once, so handing the query to the controller allocates nothing per frame.</summary>
        public LeanQuery Query { get; }

        /// <summary>The standoff in use: the configured one, or more where the near plane needs it.</summary>
        public float Standoff { get; private set; }

        /// <summary>The distance from the eye to a corner of the near clip plane, as of the last update.</summary>
        public float NearPlaneCorner { get; private set; }

        /// <summary>What the last cast that shortened the lean hit. Kept for the log.</summary>
        public Collider LastBlocker { get; private set; }

        /// <summary>How many times the query has run, so the log can tell an open room from a sweep that never runs.</summary>
        public int Queries { get; private set; }

        /// <summary>
        /// Sets the standoff from the camera's live projection. Returns it, for the clamp's
        /// skin, which must be the same number.
        /// </summary>
        public float UpdateStandoff(UnityEngine.Camera camera)
        {
            // m00 and m11 are 1 / tan of the horizontal and vertical half angles, so a corner of
            // the near plane sits near * sqrt(1 + tanH^2 + tanV^2) from the eye.
            Matrix4x4 projection = camera.projectionMatrix;
            float tanH = 1f / projection.m00;
            float tanV = 1f / projection.m11;
            NearPlaneCorner = camera.nearClipPlane * Mathf.Sqrt(1f + tanH * tanH + tanV * tanV);
            Standoff = Mathf.Max(_configuredStandoff, NearPlaneCorner * NearPlaneStandoffFactor);
            return Standoff;
        }

        private LeanObstruction Trace(Vec3 start, Vec3 direction, float maxDistance)
        {
            Queries++;
            float radius = Standoff;
            float lean = maxDistance - radius;
            var origin = new Vector3(start.X, start.Y, start.Z);
            var along = new Vector3(direction.X, direction.Y, direction.Z);

            float travel = lean;
            Collider blocker = null;

            // The swept hit's distance is where the sphere's CENTRE stopped, already one radius
            // off the surface, so it is the eye's travel as it stands.
            RaycastHit sphere;
            if (Physics.SphereCast(origin, radius, along, out sphere, lean, _mask, QueryTriggerInteraction.Ignore)
                && sphere.distance < travel)
            {
                travel = sphere.distance;
                blocker = sphere.collider;
            }

            // Overreaches the lean by the standoff at the steepest angle allowed, or the ray stops
            // where the lean stops and cannot see the surface the eye is about to rest against.
            RaycastHit line;
            if (Physics.Raycast(origin, along, out line, lean + radius / MinApproachCosine, _mask,
                    QueryTriggerInteraction.Ignore))
            {
                float cosine = Mathf.Max(Mathf.Abs(Vector3.Dot(along, line.normal)), MinApproachCosine);
                float lineTravel = line.distance - radius / cosine;
                if (lineTravel < travel)
                {
                    travel = lineTravel;
                    blocker = line.collider;
                }
            }

            if (travel >= lean) return LeanObstruction.Clear;
            LastBlocker = blocker;
            return LeanObstruction.Hit(Mathf.Max(travel, 0f) + radius);
        }
    }
}
