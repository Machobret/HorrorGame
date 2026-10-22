using UnityEngine;

namespace HorrorEngine
{
    // Solve against one stable elbow pole, then fade bone rotations themselves.
    // Fading the pole/target can flip the bend plane near a straight arm.
    internal static class ReloadArmIK
    {
        public static void Solve(Transform upper, Transform lower, Transform hand, Transform actor,
            Vector3 target, Quaternion wrist, Vector3 localPole, float weight,
            Transform clavicle = null, float shoulderContribution = 0, float preserveWristLocal = 0, float shoulderSwivelDegrees = 0)
        {
            weight = Mathf.Clamp01(weight);
            if (!upper || !lower || !hand || !actor || weight <= .00001f) return;
            Quaternion upperSource = upper.localRotation, lowerSource = lower.localRotation, handSource = hand.localRotation;
            Quaternion clavicleSource = clavicle ? clavicle.localRotation : Quaternion.identity;
            if (clavicle && shoulderContribution > 0)
            {
                // Let the collarbone follow a reach a little, as a real shoulder
                // depresses/protracts. The arm joints still solve the precise hand
                // target; no bone translation or scaling is used to gain reach.
                Vector3 rest = upper.position - clavicle.position;
                Vector3 reach = target - clavicle.position;
                Quaternion follow = Quaternion.FromToRotation(rest, reach);
                follow = Quaternion.RotateTowards(Quaternion.identity, follow, 14 * Mathf.Clamp01(shoulderContribution));
                clavicle.rotation = follow * clavicle.rotation;
            }
            Vector3 shoulder = upper.position, elbow = lower.position;
            float a = Vector3.Distance(shoulder, elbow), b = Vector3.Distance(elbow, hand.position);
            Vector3 direction = target - shoulder;
            float maxReach = a + b - .008f;
            if (clavicle && shoulderContribution > 0)
                maxReach = Mathf.Min(maxReach, Mathf.Sqrt(a * a + b * b - 2 * a * b * Mathf.Cos(155 * Mathf.Deg2Rad)));
            float distance = Mathf.Clamp(direction.magnitude, Mathf.Abs(a - b) + .002f, maxReach);
            if (direction.sqrMagnitude < .000001f) return;
            direction.Normalize();
            Vector3 bend = Vector3.ProjectOnPlane(actor.TransformPoint(localPole) - shoulder, direction);
            if (bend.sqrMagnitude < .000001f) bend = Vector3.ProjectOnPlane(-actor.up, direction);
            if (bend.sqrMagnitude < .000001f) bend = Vector3.ProjectOnPlane(actor.right, direction);
            // Swivel the upper arm around the shoulder-to-hand axis. This turns
            // the elbow forward without moving the wrist off its loading target.
            bend = Quaternion.AngleAxis(shoulderSwivelDegrees, direction) * bend;
            float along = (a * a - b * b + distance * distance) / (2 * distance);
            Vector3 solved = shoulder + direction * along + bend.normalized * Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
            Vector3 reachable = shoulder + direction * distance;
            upper.rotation = Quaternion.FromToRotation(elbow - shoulder, solved - shoulder) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, reachable - lower.position) * lower.rotation;
            hand.rotation = wrist;
            // Pocket reaches should carry the hand with the forearm, instead of
            // forcing the old world-space grip orientation onto a turned elbow.
            hand.localRotation = Quaternion.Slerp(hand.localRotation, handSource, Mathf.Clamp01(preserveWristLocal));
            Quaternion clavicleSolved = clavicle ? clavicle.localRotation : Quaternion.identity;
            Quaternion upperSolved = upper.localRotation, lowerSolved = lower.localRotation, handSolved = hand.localRotation;
            if (clavicle) clavicle.localRotation = Quaternion.Slerp(clavicleSource, clavicleSolved, weight);
            upper.localRotation = Quaternion.Slerp(upperSource, upperSolved, weight);
            lower.localRotation = Quaternion.Slerp(lowerSource, lowerSolved, weight);
            hand.localRotation = Quaternion.Slerp(handSource, handSolved, weight);
        }
    }
}
