using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Handles generation and sequencing of sports-specific movement patterns.
/// 
/// This module acts as a prototype for future integration with a trained ML model.
/// The ML model would process video rally data (e.g., from tennis or badminton)
/// to extract key movement vectors and store them as sequences.
/// 
/// For the prototype, we simulate this behavior by:
///   1. Allowing mock JSON pattern imports
///   2. Generating placeholder ML-based patterns
///   3. Providing clean APIs for loading, resetting, and sampling movement data
/// </summary>
public static class SportsPattern
{
    /// <summary>
    /// Represents a rally-based movement pattern (a series of movement targets in 3D space).
    /// </summary>
    private static List<Vector3> currentPattern = new List<Vector3>();

    /// <summary>
    /// Index pointer for looping through the current pattern.
    /// </summary>
    private static int patternIndex = 0;

    /// <summary>
    /// A flag indicating whether the data was loaded from a "trained" (mock) ML model.
    /// </summary>
    public static bool isModelBased = false;

    /// <summary>
    /// Fetches the next movement position in the rally sequence.
    /// Loops seamlessly for continuous training.
    /// </summary>
    public static Vector3 GetNextPosition()
    {
        if (currentPattern == null || currentPattern.Count == 0)
        {
            Debug.LogWarning("No pattern loaded — loading default mock pattern.");
            LoadMockPattern("Tennis");
        }

        patternIndex = patternIndex % currentPattern.Count;
        Vector3 nextPosition = currentPattern[patternIndex];
        patternIndex++;

        return nextPosition;
    }

    /// <summary>
    /// Loads a mock pattern that simulates ML-generated sequences for a given sport.
    /// This is the fallback prototype for presentation.
    /// </summary>
    public static void LoadMockPattern(string sportType)
    {
        List<Vector3> mockData = new List<Vector3>();

        switch (sportType.ToLower())
        {
            case "tennis":
                mockData = new List<Vector3>()
                {
                    new Vector3(1.5f, 0, 0.5f),   // Forehand
                    new Vector3(-2.0f, 0, -0.5f), // Backhand
                    new Vector3(0.5f, 0, 1.2f),   // Drop shot
                    new Vector3(-1.2f, 0, -1.0f), // Defensive backhand
                    new Vector3(2.3f, 0, 0.8f)    // Smash return
                };
                break;

            case "badminton":
                mockData = new List<Vector3>()
                {
                    new Vector3(1.2f, 0, 0.7f),   // Net kill
                    new Vector3(-1.8f, 0, 0.3f),  // Deep clear
                    new Vector3(0.6f, 0, -0.9f),  // Drop shot
                    new Vector3(-1.0f, 0, -1.4f), // Backcourt defense
                    new Vector3(2.0f, 0, 1.1f)    // Smash attack
                };
                break;

            default:
                Debug.Log("Loading generic agility pattern...");
                mockData = new List<Vector3>()
                {
                    new Vector3(1f, 0, 0.5f),
                    new Vector3(-1f, 0, -0.5f),
                    new Vector3(0.5f, 0, 1f),
                    new Vector3(-0.5f, 0, -1f)
                };
                break;
        }

        LoadPattern(mockData);
        isModelBased = false;
        Debug.Log($"Mock pattern for {sportType} loaded successfully.");
    }

    /// <summary>
    /// Loads a custom pattern (e.g., from an external JSON or ML model output).
    /// In a real setup, this would be parsed from movement data generated from video analysis.
    /// </summary>
    public static void LoadPatternFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError($"Pattern file not found at path: {filePath}");
            return;
        }

        try
        {
            string jsonData = File.ReadAllText(filePath);
            List<Vector3Serializable> serialized = JsonUtility.FromJson<Vector3ListWrapper>(jsonData).vectors;
            List<Vector3> loadedPattern = new List<Vector3>();

            foreach (var v in serialized)
            {
                loadedPattern.Add(v.ToVector3());
            }

            LoadPattern(loadedPattern);
            isModelBased = true;

            Debug.Log($"Loaded sports pattern from file: {filePath}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to parse pattern file: {ex.Message}");
        }
    }

    /// <summary>
    /// Sets a new active pattern sequence.
    /// </summary>
    public static void LoadPattern(List<Vector3> newPattern)
    {
        currentPattern = newPattern;
        patternIndex = 0;
    }

    /// <summary>
    /// Resets pattern index to the beginning.
    /// </summary>
    public static void ResetPatternIndex()
    {
        patternIndex = 0;
    }

    // === Helper Classes for JSON Serialization === //

    [System.Serializable]
    public class Vector3Serializable
    {
        public float x, y, z;
        public Vector3Serializable(Vector3 v) { x = v.x; y = v.y; z = v.z; }
        public Vector3 ToVector3() => new Vector3(x, y, z);
    }

    [System.Serializable]
    public class Vector3ListWrapper
    {
        public List<Vector3Serializable> vectors;
    }
}
