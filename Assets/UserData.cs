using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A static class to manage all user historical data using PlayerPrefs.
/// Stores time taken and the score value of each cone hit.
/// </summary>
public static class UserData
{
    // Key to store all hit data (time,score;time,score;...)
    private const string AllHitsDataKey = "AllHitsData";

    /// <summary>
    /// Records a new successful hit time and the score earned.
    /// </summary>
    public static void RecordHit(float time, int scoreValue)
    {
        string allData = PlayerPrefs.GetString(AllHitsDataKey, "");
        string newEntry = $"{time:F2},{scoreValue}";

        if (string.IsNullOrEmpty(allData))
        {
            allData = newEntry;
        }
        else
        {
            allData += ";" + newEntry; 
        }

        PlayerPrefs.SetString(AllHitsDataKey, allData);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Gets all recorded hit data as a list of tuples (time, scoreValue).
    /// </summary>
    public static List<(float time, int score)> GetAllHits()
    {
        string allData = PlayerPrefs.GetString(AllHitsDataKey, "");
        var hits = new List<(float time, int score)>();

        if (string.IsNullOrEmpty(allData))
        {
            return hits;
        }

        // Split by hit (;) and then split each hit by comma (,)
        string[] hitEntries = allData.Split(';');

        foreach (string entry in hitEntries)
        {
            string[] values = entry.Split(',');
            if (values.Length == 2 && float.TryParse(values[0], out float time) && int.TryParse(values[1], out int score))
            {
                hits.Add((time, score));
            }
        }
        return hits;
    }

    /// <summary>
    /// Deletes all saved user data. Useful for testing.
    /// </summary>
    public static void ResetData()
    {
        PlayerPrefs.DeleteKey(AllHitsDataKey);
        UnityEngine.Debug.Log("User data has been reset.");
    }
}