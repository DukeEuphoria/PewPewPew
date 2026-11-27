using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using PewPewPew.Core;
using PewPewPew.World;
using UnityEngine;

namespace PewPewPew.Ships
{
    internal sealed class TractorBeams
    {
        private readonly Ship m_Ship;
        private readonly Dictionary<int, TractorSubSystem> m_Tractors = new Dictionary<int, TractorSubSystem>();
        private readonly Dictionary<int, TractorVisual> m_Visuals = new Dictionary<int, TractorVisual>();

        internal TractorBeams(Ship ship) => m_Ship = ship;
        internal bool HasTractors => m_Tractors.Count > 0;

        internal void Add(int mountIndex, TractorSubSystem system)
        {
            TractorSubSystemDef def = system.Def;
            if (!float.IsFinite(def.TractorRange) || def.TractorRange < 0f ||
                !float.IsFinite(def.TractorHalfAngle) || def.TractorHalfAngle < 0f || def.TractorHalfAngle > 180f ||
                !float.IsFinite(def.TractorForce) || def.TractorForce < 0f ||
                (def.TractorRange > 0f && (def.TractorForce <= 0f || def.Mode != ActivationMode.Passive ||
                    !float.IsFinite(def.ActivePowerDrain) || def.ActivePowerDrain < 0f)))
                throw new InvalidOperationException($"{def.name} has invalid automatic tractor settings.");
            if (def.TractorRange > 0f) m_Tractors.Add(mountIndex, system);
        }

        internal uint[] Step(float deltaTime, PowerBank power, int mountCount, uint[] previousTargets)
        {
            if (m_Tractors.Count == 0) return previousTargets;
            var targets = new uint[mountCount];
            foreach (KeyValuePair<int, TractorSubSystem> entry in m_Tractors)
            {
                TractorSubSystem system = entry.Value;
                if (!system.IsRunning) continue;
                Treasure target = FindTarget(system);
                if (target == null || !power.TryDraw(system.Def.ActivePowerDrain * deltaTime)) continue;
                Vector2 pull = (Vector2)system.System.Point.position - (Vector2)target.transform.position;
                if (pull.sqrMagnitude <= 0f) continue;
                target.Pull(pull.normalized * system.Def.TractorForce);
                targets[entry.Key] = target.netId;
            }
            return previousTargets != null && previousTargets.SequenceEqual(targets) ? previousTargets : targets;
        }

        private Treasure FindTarget(TractorSubSystem system)
        {
            Transform mount = system.System.Point;
            Vector2 origin = mount.position;
            Vector2 forward = mount.up;
            float rangeSquared = system.Def.TractorRange * system.Def.TractorRange;
            float minimumDot = Mathf.Cos(system.Def.TractorHalfAngle * Mathf.Deg2Rad);
            int free = m_Ship.FreeCargo;
            if (free == 0) return null;
            float nearest = float.PositiveInfinity;
            Treasure best = null;
            foreach (SpaceObject spaceObject in SpaceObject.ActiveObjects)
            {
                if (!(spaceObject is Treasure pickup) || !pickup.isServer || !pickup.IsAvailable ||
                    pickup.Units > free) continue;
                Vector2 delta = (Vector2)pickup.transform.position - origin;
                float distance = delta.sqrMagnitude;
                if (distance <= 0f || distance > rangeSquared || distance > nearest ||
                    Vector2.Dot(forward, delta.normalized) < minimumDot) continue;
                if (distance == nearest && best != null && pickup.netId >= best.netId) continue;
                if (Blocked(origin, delta, Mathf.Sqrt(distance))) continue;
                nearest = distance;
                best = pickup;
            }
            return best;
        }

        private bool Blocked(Vector2 origin, Vector2 delta, float distance)
        {
            foreach (RaycastHit2D hit in Physics2D.RaycastAll(origin, delta / distance, distance))
            {
                Collider2D collider = hit.collider;
                if (collider == null || collider.isTrigger || collider.transform.IsChildOf(m_Ship.transform) ||
                    collider.GetComponentInParent<Treasure>() != null) continue;
                Ship ship = collider.GetComponentInParent<Ship>();
                if ((ship != null && ship != m_Ship) || collider.GetComponentInParent<Asteroid>() != null ||
                    collider.GetComponentInParent<OrbitalBody>() != null) return true;
            }
            return false;
        }

        internal void BuildVisuals()
        {
            if (m_Tractors.Count == 0 || m_Visuals.Count > 0) return;
            foreach (KeyValuePair<int, TractorSubSystem> entry in m_Tractors)
            {
                TractorVisual prefab = entry.Value.Def.Visual;
                if (prefab == null) throw new InvalidOperationException($"{entry.Value.Def.name} has no tractor visual.");
                TractorVisual visual = UnityEngine.Object.Instantiate(prefab, m_Ship.transform);
                visual.gameObject.layer = m_Ship.gameObject.layer;
                m_Visuals.Add(entry.Key, visual);
            }
        }

        internal void Render(uint[] targets)
        {
            foreach (KeyValuePair<int, TractorVisual> entry in m_Visuals)
            {
                uint target = targets != null && entry.Key < targets.Length ? targets[entry.Key] : 0;
                if (target == 0 || !NetworkClient.spawned.TryGetValue(target, out NetworkIdentity identity) || identity == null)
                {
                    entry.Value.Show(null, null);
                    continue;
                }
                if (!identity.TryGetComponent(out Treasure pickup))
                {
                    entry.Value.Show(null, null);
                    Debug.LogError("Tractor target is not a treasure pickup.", identity);
                    continue;
                }
                entry.Value.Show(m_Tractors[entry.Key].System.Point, pickup);
            }
        }
    }
}
