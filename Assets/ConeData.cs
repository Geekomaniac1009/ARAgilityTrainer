using UnityEngine;

/// <summary>
/// A simple component to hold dynamic data for the cone object.
/// Used by AgilityGameManager to assign the cone's score value (1 or 2).
/// </summary>
public class ConeData : MonoBehaviour
{
    // The score value assigned to this specific cone instance.
    public int scoreValue = 1;
}