using System;
using System.Collections.Generic;

namespace op.io
{
    public static class FarmDataLoader
    {
        // Cache to store the loaded farm data
        private static List<FarmData> _cachedFarmData = null;

        // Function to get the farm data (cached if available)
        public static List<FarmData> GetFarmData()
        {
            // If the data has been cached already, return it
            if (_cachedFarmData != null)
            {
                return _cachedFarmData;
            }

            // If not cached, load it from the database and cache it
            return LoadFarmData();
        }

        // Load farm data from the database and cache it
        private static List<FarmData> LoadFarmData()
        {
            List<FarmData> farmDataList = [];

            try
            {
                DebugLogger.PrintDatabase("Loading farm data from database...");

                // Query to load farm count data
                string query = "SELECT ID, Count FROM FarmData";
                var results = DatabaseQuery.ExecuteQuery(query);

                if (results.Count == 0)
                {
                    DebugLogger.PrintWarning("No farm data found in database.");
                    return farmDataList;
                }

                DebugLogger.PrintDatabase($"Loaded {results.Count} farm data entries.");

                foreach (var row in results)
                {
                    try
                    {
                        int id = Convert.ToInt32(row["ID"]);
                        int count = Convert.ToInt32(row["Count"]);
                        
                        if (count <= 0)
                        {
                            DebugLogger.PrintWarning($"Farm with ID {id} has non-positive count ({count}). Skipping.");
                            continue;
                        }

                        farmDataList.Add(new FarmData
                        {
                            ID = id,
                            Count = count
                        });
                    }
                    catch (Exception exRow)
                    {
                        DebugLogger.PrintError($"Error parsing farm data row: {exRow.Message}");
                    }
                }

                // Cache the data for future use
                _cachedFarmData = farmDataList;

            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to load farm data: {ex.Message}");
            }

            return farmDataList;
        }
    }
}
