using Firebase;
using Firebase.Auth; // <-- Added for Authentication
using Firebase.Database;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Handles Firebase SDK initialization, Anonymous Authentication, and provides central access to the Realtime Database.
/// Attach this script to a persistent GameObject in your scene.
/// </summary>
public class FirebaseManager : MonoBehaviour
{
    // Static references
    public static DatabaseReference DbReference { get; private set; }
    public static bool IsInitialized { get; private set; } = false;
    public static FirebaseManager Instance { get; private set; }
    // Private Auth instance
    private FirebaseAuth auth;

    // Static Property for authenticated User ID (Replaces the hardcoded string)
    /// <summary>
    /// Returns the authenticated Firebase User ID (UID). Returns null if not signed in.
    /// </summary>
    public static string UserId
    {
        get
        {
            // Safely retrieve the current authenticated user's UID
            return FirebaseAuth.DefaultInstance.CurrentUser?.UserId;
        }
    }

    // --- MULTIPLAYER PROPERTY ---
    public static int CurrentGameSeed { get; set; } = 0;

    public struct GameSummary
    {
        public float score;
        public float difficultyLevel; // Corresponds to coneTimeout
        public long timestamp;

        public GameSummary(float s, float d, long t)
        {
            score = s;
            difficultyLevel = d;
            timestamp = t;
        }
    }
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Makes the object persist
            InitializeFirebase(); // Start the async process immediately
        }
        else
        {
            Destroy(gameObject); // Destroy duplicates
        }
    }
    void AuthStateChanged(object sender, EventArgs eventArgs)
    {
        if (auth.CurrentUser != null)
        {
            UnityEngine.Debug.Log("Anonymous User successfully signed in/restored. UID: " + auth.CurrentUser.UserId);
            IsInitialized = true;
        }
    }
    private async void InitializeFirebase()
    {
        UnityEngine.Debug.Log("Checking Firebase dependencies...");
        DependencyStatus dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();

        if (dependencyStatus == DependencyStatus.Available)
        {
            // Initialize Firebase Components
            FirebaseApp app = FirebaseApp.DefaultInstance;
            DbReference = FirebaseDatabase.DefaultInstance.RootReference;
            auth = FirebaseAuth.DefaultInstance;

            // Add the listener for Auth state changes (only once)
            auth.StateChanged += AuthStateChanged;

            UnityEngine.Debug.Log("Firebase components initialized. Checking user status...");

            // Check for a PRE-EXISTING, persistent user.
            if (auth.CurrentUser != null)
            {
                // The Firebase SDK successfully loaded the previously signed-in user (even if anonymous).
                // We just wait for AuthStateChanged to confirm and log this.
                return;
            }

            // If no user is signed in (first run or persistence failed), sign in anonymously.
            try
            {
                UnityEngine.Debug.Log("No existing user found. Signing in anonymously...");
                await auth.SignInAnonymouslyAsync();
                // AuthStateChanged will fire upon successful sign-in.
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("Error during Anonymous Sign-in: " + e.Message);
            }
        }
        else
        {
            UnityEngine.Debug.LogError($"Could not resolve Firebase dependencies: {dependencyStatus}");
        }
    }
    

    /// <summary>
    /// A Task that completes when Firebase has finished its initial setup and a user is signed in.
    /// </summary>
    public async Task WaitUntilUserSignedIn()
    {
        int maxWaitTimeMs = 10000; // 10 seconds max wait time
        int checkIntervalMs = 500;

        // Use the static IsInitialized and UserId properties to check status
        while ((!IsInitialized || string.IsNullOrEmpty(UserId)) && maxWaitTimeMs > 0)
        {
            UnityEngine.Debug.Log("UserStatsManager is waiting for Firebase connection...");
            await Task.Delay(checkIntervalMs);
            maxWaitTimeMs -= checkIntervalMs;
        }

        if (IsInitialized && !string.IsNullOrEmpty(UserId))
        {
            UnityEngine.Debug.Log("Firebase is ready and user is signed in.");
        }
        else
        {
            UnityEngine.Debug.LogError("Firebase initialization or user sign-in failed/timed out.");
        }
    }
    private async Task SignInAnonymously()
    {
        try
        {
            AuthResult userCredential = await auth.SignInAnonymouslyAsync();
            UnityEngine.Debug.Log($"Anonymous User signed in successfully: {userCredential.User.UserId}");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Anonymous Sign-in failed: {e}");
        }
    }

    // --- FIREBASE DATA OPERATIONS ---

    /// <summary>
    /// Fetches the last 'limit' game history entries for the current user.
    /// </summary>
    /// <param name="limit">The number of entries to fetch.</param>
    /// <returns>A list of GameSummary structs.</returns>
    public async Task<List<GameSummary>> GetLastGameHistory(int limit = 20) // Default to 20
    {
        List<GameSummary> summaries = new List<GameSummary>();
        string userId = UserId;

        if (!IsInitialized || string.IsNullOrEmpty(userId))
        {
            UnityEngine.Debug.LogError("Firebase not initialized or User ID is missing. Cannot fetch stats.");
            return summaries;
        }

        // Query: Order by timestamp and limit to the last 'limit'
        Query historyQuery = DbReference.Child("game_history")
                                        .Child(userId)
                                        .OrderByChild("timestamp")
                                        .LimitToLast(limit); // Use the limit parameter

        try
        {
            DataSnapshot snapshot = await historyQuery.GetValueAsync();

            if (snapshot.Exists && snapshot.HasChildren)
            {
                foreach (var childSnapshot in snapshot.Children)
                {
                    IDictionary<string, object> data = childSnapshot.Value as IDictionary<string, object>;
                    if (data != null)
                    {
                        float score = Convert.ToSingle(data["score"]);
                        float difficulty = Convert.ToSingle(data["difficultyLevel"]);
                        long timestamp = Convert.ToInt64(data["timestamp"]);

                        summaries.Add(new GameSummary(score, difficulty, timestamp));
                    }
                }
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to retrieve game history: {e.Message}");
        }

        // The query returns oldest-to-newest. Reverse it to show newest games first.
        summaries.Reverse();

        return summaries;
    }
    // Function for Player 1 (Creator) to start a challenge
    /// <summary>
    /// Generates a game seed, creates a challenge entry in Firebase with status "waiting", and returns the code.
    /// </summary>
    public async Task<(string challengeCode, int seed)> CreateChallengeSession()
    {
        if (string.IsNullOrEmpty(UserId))
        {
            UnityEngine.Debug.LogError("User not logged in. Cannot create challenge.");
            // Use default/error values for the tuple return
            return (null, 0);
        }

        // Generate a 5-digit code (This will be the key/challengeCode)
        int seed = new System.Random().Next(10000, 99999);
        string challengeCode = seed.ToString();

        var challengeData = new Dictionary<string, object>
    {
        { "gameSeed", seed },
        { "status", "waiting" }, // Initial status
        { "creatorId", UserId }
    };

        // Path: /challenges/{challengeCode}
        await DbReference.Child("challenges").Child(challengeCode).SetValueAsync(challengeData);

        UnityEngine.Debug.Log($"Challenge created with code: {challengeCode}");
        return (challengeCode, seed);
    }

    // Function for Player 2 (Joiner) to enter a challenge
    /// <summary>
    /// Checks challenge existence/status, updates status to "active" (which signals Player 1), and returns the game seed.
    /// </summary>
    public async Task<(bool success, string message, int seed)> JoinChallengeSession(string challengeCode)
    {
        if (string.IsNullOrEmpty(UserId))
        {
            return (false, "User not logged in. Please sign in.", 0);
        }

        DatabaseReference challengeRef = DbReference.Child("challenges").Child(challengeCode);
        DataSnapshot snapshot = await challengeRef.GetValueAsync();

        if (!snapshot.Exists)
        {
            return (false, "Invalid Challenge Code.", 0);
        }

        string status = snapshot.Child("status").Value?.ToString();

        if (status == "active")
        {
            return (false, "Challenge is already in progress.", 0);
        }

        if (status == "waiting")
        {
            // Success! Player 2 is joining.
            // Update status to 'active' (TRIGGERS P1's LISTENER)
            await challengeRef.Child("status").SetValueAsync("active");

            // Record Player 2's ID
            await challengeRef.Child("opponentId").SetValueAsync(UserId);

            // Get the game seed
            int seed = Convert.ToInt32(snapshot.Child("gameSeed").Value);

            return (true, "Joining...", seed);
        }

        // Handles "finished" or other unexpected statuses
        return (false, "Challenge is currently unavailable.", 0);
    }


    /// <summary>
    /// Returns a DatabaseReference to the 'scores' node for a specific challenge.
    /// </summary>
    public DatabaseReference GetChallengeScoresReference(int challengeSeed)
    {
        string challengeCode = challengeSeed.ToString();
        return DbReference.Child("challenges").Child(challengeCode).Child("scores");
    }

    /// <summary>
    /// Listens for a change in a challenge's status.
    /// Used by Player 1 (Creator) to wait for Player 2 to join.
    /// </summary>
    /// <param name="challengeCode">The code for the challenge to monitor.</param>
    /// <param name="onOpponentJoined">The action to execute when status changes to "active".</param>
    public void ListenForOpponentToJoin(string challengeCode, Action onOpponentJoined)
    {
        DatabaseReference challengeRef = DbReference.Child("challenges").Child(challengeCode);

        challengeRef.ValueChanged += HandleChallengeStatusChange;

        void HandleChallengeStatusChange(object sender, ValueChangedEventArgs args)
        {
            if (args.DatabaseError != null)
            {
                UnityEngine.Debug.LogError(args.DatabaseError.Message);
                challengeRef.ValueChanged -= HandleChallengeStatusChange; // Stop listening
                return;
            }

            if (args.Snapshot.Exists)
            {
                // Check if the status is "active" (meaning Player 2 just joined)
                if (args.Snapshot.Child("status").Value.ToString() == "active")
                {
                    UnityEngine.Debug.Log($"[FirebaseManager] Opponent joined challenge {challengeCode}!");

                    // Stop listening, we are done.
                    challengeRef.ValueChanged -= HandleChallengeStatusChange;

                    // Trigger the callback (which will load the AR scene for P1)
                    onOpponentJoined?.Invoke();
                }
            }
        }
    }

    // Function to upload challenge scores
    /// <summary>
    /// Uploads a score for a completed challenge session.
    /// </summary>
    public async void UploadScoreForChallenge(int challengeSeed, string playerId, int score)
    {
        string challengeCode = challengeSeed.ToString();
        // Use the player's ID to store their score under the challenge
        DatabaseReference challengeRef = DbReference.Child("challenges").Child(challengeCode).Child("scores").Child(playerId);

        var scoreData = new Dictionary<string, object>
    {
        { "score", score },
        { "timestamp", ServerValue.Timestamp }
    };

        await challengeRef.SetValueAsync(scoreData);
        UnityEngine.Debug.Log($"Uploaded score for challenge {challengeCode}. Player: {playerId}, Score: {score}");
    }
    /// <summary>
    /// Uploads a summarized score and difficulty level for a personal training session.
    /// </summary>
    /// <param name="score">The total score achieved in the game session.</param>
    /// <param name="difficultyLevel">The difficulty parameter (coneTimeout) for the game.</param>
    public async void UploadPersonalGameSummary(int score, float difficultyLevel)
    {
        // UserId is now securely retrieved from the authenticated session
        string userId = UserId;
        if (!IsInitialized || string.IsNullOrEmpty(userId))
        {
            UnityEngine.Debug.LogError("Firebase not initialized or User ID is missing. Cannot upload game summary.");
            return;
        }

        // Path: /game_history/{userId}/{autoId} - autoId created by .Push()
        DatabaseReference historyRef = DbReference.Child("game_history")
                                                  .Child(userId)
                                                  .Push(); // .Push() creates a unique auto-ID

        // Data structure: { score: 50, difficultyLevel: 3.5, timestamp: ... }
        var sessionSummary = new Dictionary<string, object>
        {
            { "score", score },
            { "difficultyLevel", difficultyLevel },
            { "timestamp", ServerValue.Timestamp }
        };

        await historyRef.SetValueAsync(sessionSummary);
        UnityEngine.Debug.Log($"Uploaded personal game summary. Score: {score}, Difficulty: {difficultyLevel:F2}");
    }
}