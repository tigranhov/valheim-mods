using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveMapper.ShipCargo
{
    /// <summary>
    /// Crates press their ship down where they stand, so the ship trims toward heavy cargo. The ship's own buoyancy
    /// (Ship.CustomFixedUpdate lifts each end of its float box in proportion to how deep it sits) finds the new balance.
    /// </summary>
    internal static class CargoWeight
    {
        // At full capacity, all cargo together presses down with this share of the ship's own weight.
        private const float FullLoadShare = 0.1f;

        public static void Apply(Ship ship)
        {
            if (!CargoConfig.WeightEnabled.Value || ship.m_nview == null || !ship.m_nview.IsOwner() || ship.m_body == null)
            {
                return;
            }
            Rigidbody body = ship.m_body;
            WaterVolume volume = null;
            Vector3 center = body.worldCenterOfMass;
            // Out of the water the vanilla buoyancy is off too; don't press a beached ship into the ground.
            if (center.y - Floating.GetWaterLevel(center, ref volume) - ship.m_waterLevelOffset > ship.m_disableLevel)
            {
                return;
            }
            float perWeight = body.mass * Physics.gravity.magnitude * FullLoadShare * CargoConfig.WeightStrength.Value / ShipRules.Capacity(ship);
            IReadOnlyList<ShipPassenger> riders = ShipPassenger.All;
            for (int i = 0; i < riders.Count; i++)
            {
                ShipPassenger rider = riders[i];
                if (rider.CurrentShip == ship && rider.Weight > 0f)
                {
                    body.AddForceAtPosition(Vector3.down * (rider.Weight * perWeight), rider.transform.TransformPoint(CrateShape.Center), ForceMode.Force);
                }
            }
        }
    }
}
