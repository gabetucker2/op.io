using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace op.io
{
    public static class FarmObjectLoader
    {
        // Load the farm objects based on the prototypes and farm data
        public static List<GameObject> LoadFarmObjects(List<GameObject> farmPrototypes, List<FarmData> farmDataList)
        {
            List<GameObject> farmObjects = [];

            try
            {
                DebugLogger.PrintDatabase($"Loading {farmDataList.Count} farm objects from data...");

                if (farmDataList.Count == 0)
                {
                    DebugLogger.PrintWarning("No farm data found to create farm objects.");
                    return farmObjects;
                }

                foreach (var farmData in farmDataList)
                {
                    try
                    {
                        // Find the prototype corresponding to the current farm data entry
                        GameObject prototype = farmPrototypes.Find(proto => proto.ID == farmData.ID);
                        if (prototype == null)
                        {
                            DebugLogger.PrintWarning($"Farm prototype with ID {farmData.ID} not found, skipping.");
                            continue;
                        }

                        if (!SimpleGameObject.TryFromGameObject(prototype, out SimpleGameObject baseArchetype))
                        {
                            DebugLogger.PrintWarning($"Farm prototype with ID {farmData.ID} is missing shape data. Skipping.");
                            continue;
                        }

                        FarmGameObject farmArchetype = new(baseArchetype, farmData);

                        // Create the farm objects based on the prototype and the count
                        for (int i = 0; i < farmData.Count; i++)
                        {
                            // Clone the prototype and set unique properties (like position)
                            GameObject farmObject = CloneFarmPrototype(farmArchetype);

                            if (farmObject == null)
                            {
                                DebugLogger.PrintError($"Failed to create farm object from prototype ID {prototype.ID}. Skipping.");
                                continue;
                            }

                            // Add farm object to the list
                            farmObjects.Add(farmObject);
                        }
                    }
                    catch (Exception exRow)
                    {
                        DebugLogger.PrintError($"Error processing farm data row: {exRow.Message}");
                    }
                }

                DebugLogger.PrintDatabase($"Successfully created {farmObjects.Count} farm objects.");
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to load farm objects: {ex.Message}");
            }

            return farmObjects;
        }

        // Clone the farm prototype and set specific properties for the farm object
        private static GameObject CloneFarmPrototype(FarmGameObject archetype)
        {
            try
            {
                SimpleGameObject baseObject = archetype.BaseObject;
                Vector2 position = new Vector2(archetype.FarmData.Count * 50, 100);
                SimpleGameObject instance = archetype.CreateInstance(archetype.FarmData.ID, position, baseObject.Transform.Rotation);
                GameObject farmObject = instance.ToGameObject();

                // Load the shape content (e.g., texture) for this farm object
                farmObject.LoadContent(Core.Instance.GraphicsDevice);

                return farmObject;
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to clone farm prototype: {ex.Message}");
                return null;
            }
        }
    }
}
