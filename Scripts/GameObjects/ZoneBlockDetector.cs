using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using op.io.UI.BlockScripts.Blocks;

namespace op.io
{
    /// <summary>
    /// Each frame, checks whether the player is standing on top of a ZoneBlock
    /// game object and activates/deactivates the corresponding Dynamic content
    /// in the Interact block.
    /// </summary>
    public static class ZoneBlockDetector
    {
        private static string _activeZoneKey;

        public static void Reset()
        {
            _activeZoneKey = null;
            InteractBlock.ClearActiveDynamicContent();
        }

        /// <summary>
        /// Call once per frame after physics are resolved.
        /// Uses simple AABB overlap between the player and every ZoneBlock GO.
        /// </summary>
        public static void Update()
        {
            Agent player = Core.Instance?.PlayerOrNull;
            if (player?.Shape == null)
            {
                if (_activeZoneKey != null)
                {
                    _activeZoneKey = null;
                    InteractBlock.ClearActiveDynamicContent();
                }
                return;
            }

            Rectangle playerBounds = GetBounds(player);

            string hitKey = null;

            List<GameObject> gameObjects = Core.Instance.GameObjects;
            for (int i = 0; i < gameObjects.Count; i++)
            {
                GameObject go = gameObjects[i];
                if (!go.IsZoneBlock || go.Shape == null)
                    continue;

                Rectangle zoneBounds = GetBounds(go);
                if (playerBounds.Intersects(zoneBounds))
                {
                    hitKey = go.ZoneBlockDynamicKey;
                    break;
                }
            }

            // Also check static objects (ZoneBlocks are static)
            if (hitKey == null)
            {
                List<GameObject> staticObjects = Core.Instance.StaticObjects;
                if (staticObjects != null)
                {
                    for (int i = 0; i < staticObjects.Count; i++)
                    {
                        GameObject go = staticObjects[i];
                        if (!go.IsZoneBlock || go.Shape == null)
                            continue;

                        Rectangle zoneBounds = GetBounds(go);
                        if (playerBounds.Intersects(zoneBounds))
                        {
                            hitKey = go.ZoneBlockDynamicKey;
                            break;
                        }
                    }
                }
            }

            if (!string.Equals(hitKey, _activeZoneKey, StringComparison.OrdinalIgnoreCase))
            {
                _activeZoneKey = hitKey;
                if (hitKey != null)
                    InteractBlock.SetActiveDynamicContent(hitKey, "ZoneBlockDetector.Update (ZoneBlock overlap)");
                else
                    InteractBlock.ClearActiveDynamicContent();
            }
        }

        private static Rectangle GetBounds(GameObject go)
        {
            int w = go.Shape.Width;
            int h = go.Shape.Height;
            return new Rectangle(
                (int)(go.Position.X - w / 2f),
                (int)(go.Position.Y - h / 2f),
                w, h);
        }
    }
}
