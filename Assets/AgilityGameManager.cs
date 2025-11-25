    using Firebase.Database;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using TMPro;
    using UnityEngine;
    using UnityEngine.UI;
    using UnityEngine.XR.ARFoundation;
    using UnityEngine.XR.ARSubsystems; 

    public class AgilityGameManager : MonoBehaviour
    {
        // --- MODE & SYNCHRONIZATION CONTROLS (READ FROM GAMECONTROLLER) ---
        private System.Random coneRNG;

        [Header("Mode Selection")]
        [Tooltip("If checked, cone placement follows SportsPattern.cs. Overridden in Challenge Mode.")]
        public bool isSportsPatternMode = false; 

        // --- GAME CONSTANTS ---
        // Default cone timeout for Challenge Mode
        private const float CHALLENGE_TIMEOUT_DEFAULT = 5.0f;

        // Countdown time before the game starts
        private const float START_COUNTDOWN_TIME = 3.0f;

        // Timer Blinking Constants
        private const float LOW_TIME_THRESHOLD = 10f; 

        // --- Unity Links ---
        [Header("AR Components")]
        public ARPlaneManager planeManager;
        public Camera arCamera; 

        // List to track all detected planes
        private List<ARPlane> allPlanes = new List<ARPlane>();

        [Header("Game Objects")]
        public GameObject conePrefab;
        private GameObject currentCone = null;

        
        [Header("UI Elements")]
        public TextMeshProUGUI timerText; 
        public TextMeshProUGUI scoreText; 
        public TextMeshProUGUI feedbackText;

        [Header("End Game Root")]
        public GameObject endGameRootPanel;

        [Header("Training Mode Panel")]
        public GameObject trainingResultsPanel;
        public TextMeshProUGUI trainingTitleText;
        public TextMeshProUGUI trainingScoreText;
        public Button returnToMenuButtonTrain; 

        [Header("Challenge Mode Panel")]
        public GameObject challengeResultsPanel;
        public TextMeshProUGUI challengeTitleText;
        public TextMeshProUGUI player1NameText;
        public TextMeshProUGUI player1ScoreText;
        public TextMeshProUGUI player2NameText;
        public TextMeshProUGUI player2ScoreText;
        public Button returnToMenuButtonChallenge;

        [Header("Game Mode Settings")]
        public float arenaRadius = 4.0f;
        public float totalExerciseTime = 120f;
        public float touchRadius = 0.35f;

        [Header("Difficulty Management")]
        [Tooltip("The time (in seconds) the cone stays active. Adjusts dynamically in Personal Training.")]
        public float coneTimeout = 3.0f;
        public float minTimeout = 1.0f;
        public float maxTimeout = 5.0f;
        public float difficultyAdjustmentFactor = 0.1f;

        public struct FinalChallengeResult
        {
            public int localScore;
            public int opponentScore;
            // The script that calls this function will populate this
            public string opponentName;

            public bool IsDraw => localScore == opponentScore;
            public bool IsWinner => localScore > opponentScore;
            public bool IsLoser => localScore < opponentScore;
        }

        // Define the color variables (for easy adjustment later)
        private Color ColorVictory = new Color(0.1f, 0.8f, 0.1f); 
        private Color ColorDefeat = new Color(0.8f, 0.1f, 0.1f); 
        private Color ColorDraw = Color.yellow;
        private Color ColorDefault = Color.white;

        // --- GAME STATE VARIABLES ---
        private float gameTimer = 0f;
        private float coneTimer = 0f;
        private bool isGameRunning = false;
        private bool isCountingDown = false; 
        private int totalScore = 0;
        private GameController gameController; 
        private FirebaseManager firebaseManager;
        // Blinking variables
        private Color originalTimerColor;
        private Coroutine blinkCoroutine = null;

        // --- AR Tracking state management ---
        private bool isTracking = false;
        private bool centerEstablished = false;
        private GameObject arCenterPoint; 


        // --- SETUP ---

        void Awake()
        {
            UnityEngine.Debug.Log("Awake was called.");
            // 1. Find the persistent GameController instance
            gameController = GameController.Instance;
            if (gameController == null)
            {
                UnityEngine.Debug.LogError("AgilityGameManager could not find GameController instance.");
                enabled = false;
                return;
            }

            // 2. Initialize coneRNG based on GameController's seed.
            int seed = gameController.currentGameSeed;
            // Use seeded random for Challenge, unseeded for Personal Training.
            coneRNG = (seed != 0) ? new System.Random(seed) : new System.Random();

            // 3. AR Events subscription
            if (planeManager != null)
            {
                planeManager.planesChanged += OnPlanesChanged;
            }
        }

        void OnDestroy()
        {
            if (planeManager != null)
            {
                planeManager.planesChanged -= OnPlanesChanged;
            }
        }

        void Start()
        {
            UnityEngine.Debug.Log("Start was called.");
            InitializeGameMode();
            gameController = GameController.Instance;
            firebaseManager = FindObjectOfType<FirebaseManager>();
            // Save the original color of the timer text for the blinking effect
            originalTimerColor = timerText.color;
            if (returnToMenuButtonTrain != null)
            {
                returnToMenuButtonTrain.onClick.AddListener(OnReturnToMenuClicked);
            }
            if (returnToMenuButtonChallenge != null)
            {
                returnToMenuButtonChallenge.onClick.AddListener(OnReturnToMenuClicked);
            }
            // Wait for AR tracking to stabilize and find a plane.
            SetFeedbackText("Scanning for a suitable flat surface...");
            gameTimer = totalExerciseTime;
            UpdateTimerUI();
            StartCoroutine(AutoStartFallback());

    }

    // Helper to set mode-dependent variables
    private void InitializeGameMode()
        {
            if (gameController.currentMode == GameController.GameMode.PersonalTraining)
            {
                // Load the last difficulty level from PlayerPrefs
                coneTimeout = PlayerPrefs.GetFloat("LastDifficulty", coneTimeout);
                isSportsPatternMode = false; // Personal Training is random adaptive by default
                UnityEngine.Debug.Log($"Mode: Personal Training. Starting Difficulty: {coneTimeout:F2}s");
            }
            else if (gameController.currentMode == GameController.GameMode.Challenge)
            {
                // Challenge Mode: Fixed difficulty and seeded random placement
                coneTimeout = CHALLENGE_TIMEOUT_DEFAULT;
                isSportsPatternMode = false; // Challenge is fixed random
                UnityEngine.Debug.Log($"Mode: Challenge. Seed: {gameController.currentGameSeed}. Fixed Difficulty: {coneTimeout:F2}s");
            }
            else
            {
                UnityEngine.Debug.LogError("Game Mode not set! Defaulting to Personal Training settings.");
                coneTimeout = PlayerPrefs.GetFloat("LastDifficulty", coneTimeout);
            }
        }

    private IEnumerator AutoStartFallback()
    {
        yield return new WaitForSeconds(8f);

        if (!centerEstablished)
        {
            SetFeedbackText("No plane detected — starting anyway.");
            ForceStartGame();
        }
    }

    public void ForceStartGame()
    {
        if (centerEstablished) return;

        Vector3 pos = arCamera.transform.position + arCamera.transform.forward * 2f;
        pos.y = arCamera.transform.position.y - 1.5f;

        EstablishCenter(pos, Quaternion.identity);
    }


    // --- ARPLANE MANAGER CALLBACKS ---

    private void OnPlanesChanged(ARPlanesChangedEventArgs args)
        {
            // Add new horizontal planes to the list (existing logic)
            foreach (var plane in args.added)
            {
                if (plane.alignment == PlaneAlignment.HorizontalUp)
                {
                    allPlanes.Add(plane);
                }
            }

            // Check tracking state from any tracked plane
            if (allPlanes.Any(p => p.trackingState == TrackingState.Tracking))
            {
                // We only proceed if tracking is good, the center is NOT set, and we have the camera reference.
                if (!isTracking && !centerEstablished && arCamera != null && allPlanes.Count > 0)
                {
                    isTracking = true;
                    SetFeedbackText("Surface detected. Placing arena center and starting...");

                    // Get the first tracked plane as an anchor for height
                    ARPlane firstPlane = allPlanes[0];

                    // Project camera's forward vector onto the floor plane (ignore camera pitch)
                    Vector3 cameraForward = arCamera.transform.forward;
                    cameraForward.y = 0;
                    cameraForward.Normalize();

                    // Calculate position 2m in front of the user, anchored to the floor plane's height
                    Vector3 centerPosition = arCamera.transform.position + cameraForward * 2.0f;
                    centerPosition.y = firstPlane.transform.position.y;

                    // Calculate rotation to face the player (Horizontal-only rotation)
                    Quaternion placementRotation = Quaternion.LookRotation(new Vector3(-cameraForward.x, 0, -cameraForward.z));

                    // Establish the center and start the countdown immediately!
                    // This is the call that bypasses the touch input.
                    EstablishCenter(centerPosition, placementRotation);
                }
            }

            foreach (var plane in args.removed)
            {
                allPlanes.Remove(plane);
            }
        }


        // --- INPUT HANDLER (TOUCH) ---
        void Update()
        {
            if (isGameRunning)
            {
                HandleGameTimer();
                HandleConeTimer();
                UpdateTimerUI();
                CheckProximityToCone();
            }
        }


        private void CheckProximityToCone()
        {
            // Only proceed if the game is running and a cone is currently spawned
            if (!isGameRunning || arCamera == null) return;

            // 1. Get the player's position (using the AR Camera) and the cone's position
            Vector3 playerPosition = arCamera.transform.position;
            Vector3 conePosition = currentCone.transform.position;

            // 2. Calculate the horizontal distance (ignore height)
            float distance = Vector2.Distance(
                new Vector2(playerPosition.x, playerPosition.z),
                new Vector2(conePosition.x, conePosition.z)
            );

            // 3. Check if the player is within the hit radius
            if (distance <= touchRadius)
            {
                UnityEngine.Debug.Log($"Cone hit via proximity. distance={distance:F2}, radius={touchRadius:F2}");
                OnConeHit(currentCone);
            }
        }
        private void HandlePlanePlacementInput()
        {
            // Only allow placement if tracking is good and center is not set (and not counting down)
            if (!isTracking || centerEstablished || isCountingDown) return;

            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    
                    if (arCamera != null && allPlanes.Count > 0)
                    {
                        // Use the first plane's position/rotation as the anchor
                        Vector3 planePosition = allPlanes[0].transform.position;
                        Quaternion planeRotation = allPlanes[0].transform.rotation;

                        // Project camera's forward vector onto the plane (ignore pitch/roll for direction)
                        Vector3 cameraForward = arCamera.transform.forward;
                        cameraForward.y = 0;
                        cameraForward.Normalize();

                        // Place the center 2m in front of the user's camera position on the plane's Y-level
                        Vector3 centerPosition = arCamera.transform.position + cameraForward * 2.0f;
                        centerPosition.y = planePosition.y;

                        EstablishCenter(centerPosition, planeRotation);
                    }
                }
            }
        }

        private void EstablishCenter(Vector3 position, Quaternion rotation)
        {
            if (centerEstablished) return;

            // Visual Center Marker
            if (arCenterPoint == null)
            {
                arCenterPoint = new GameObject("ARCenterPoint");
            }
            arCenterPoint.transform.position = position;
            arCenterPoint.transform.rotation = rotation;


            // Clean up AR Plane Visuals and stop detecting new planes
            if (planeManager != null)
            {
                planeManager.enabled = false;
            }

            centerEstablished = true;
            // Start the countdown coroutine!
            StartCoroutine(StartGameCountdown());
        }

        /// <summary>
        /// Handler for both End Game Panel "Return to Home" buttons.
        /// Relies on GameController to handle the scene transition.
        /// </summary>
        public void OnReturnToMenuClicked()
        {
            if (gameController != null)
            {
                // This public function in GameController handles the SceneManager.LoadScene() call
                gameController.GoToMainMenu();
            }
        }
        /// <summary>
        /// Handles the 3-2-1 countdown sequence before starting the game timer and cone logic.
        /// </summary>
        private IEnumerator StartGameCountdown()
        {
            isCountingDown = true;
            float timer = START_COUNTDOWN_TIME;

            while (timer > 0)
            {
                // Display the current countdown number (always rounded up)
                SetFeedbackText($"GET READY! Starting in: {Mathf.CeilToInt(timer)}");
                timer -= Time.deltaTime;
                yield return null; // Wait for the next frame
            }

            // COUNTDOWN FINISHED - START GAME!
            SetFeedbackText("GO!");

            isCountingDown = false;
            isGameRunning = true; 

            // Start the core game logic by placing the first cone
            RespawnConeLogic();

            // Clear the "GO!" message after a moment
            yield return new WaitForSeconds(0.5f);
            // The next call to UpdateTimerUI() will overwrite this.
            SetFeedbackText($"Time: {totalExerciseTime:F0}s / Score: {totalScore}");
        }

        /// <summary>
        /// Displays the final results panel based on the game mode and colors the UI.
        /// </summary>
        /// <param name="trainingScore">Final score for Training Mode.</param>
        /// <param name="trainingDifficulty">The final coneTimeout value (difficulty) for Training Mode.</param>
        /// <param name="challengeResult">The structured result for Challenge Mode (null in Training).</param>
        public void ShowEndGameResults(int trainingScore, float trainingDifficulty, FinalChallengeResult? challengeResult = null)
        {
            // 1. Show the root overlay and clear mode-specific panels
            endGameRootPanel.SetActive(true);
            trainingResultsPanel.SetActive(false);
            challengeResultsPanel.SetActive(false);

            // 2. Determine mode and render appropriate panel
            if (gameController.currentMode == GameController.GameMode.Challenge && challengeResult.HasValue)
            {
                // --- CHALLENGE MODE RENDERING ---
                challengeResultsPanel.SetActive(true);
                var result = challengeResult.Value;
                Color localColor, opponentColor, titleColor;

                // Determine result and colors
                if (result.IsDraw)
                {
                    titleColor = ColorDraw;
                    localColor = ColorDraw;
                    opponentColor = ColorDraw; // Both score panels are yellow
                    challengeTitleText.text = "DRAW";
                }
                else if (result.IsWinner)
                {
                    titleColor = ColorVictory;
                    localColor = ColorVictory;
                    opponentColor = ColorDefeat; // Local is Green, Opponent is Red
                    challengeTitleText.text = "VICTORY!";
                }
                else // IsLoser
                {
                    titleColor = ColorDefeat;
                    localColor = ColorDefeat;
                    opponentColor = ColorVictory; // Local is Red, Opponent is Green
                    challengeTitleText.text = "DEFEAT!";
                }

                // Apply colors and text to the Challenge Panel
                challengeTitleText.color = titleColor;

                // Local Player (YOU)
                player1NameText.text = "YOU";
                player1ScoreText.text = result.localScore.ToString();
                player1ScoreText.color = localColor; // Apply local result color

                // Opponent
                player2NameText.text = string.IsNullOrEmpty(result.opponentName) ? "OPPONENT" : result.opponentName;
                player2ScoreText.text = result.opponentScore.ToString();
                player2ScoreText.color = opponentColor; // Apply opposite result color
            }
            else // --- TRAINING MODE RENDERING ---
            {
                trainingResultsPanel.SetActive(true);

                trainingTitleText.text = "GAME OVER";
                trainingTitleText.color = ColorDefault;

                trainingScoreText.text = $"FINAL SCORE: **{trainingScore}**\nDIFFICULTY (Timeout): **{trainingDifficulty:F2}s**";
                trainingScoreText.color = ColorDefault;

                // Upload the personal results to Firebase
                if (firebaseManager != null)
                {
                    firebaseManager.UploadPersonalGameSummary(trainingScore, trainingDifficulty);
                }
            }
        }

        // --- GAME LOGIC ---

        /// <summary>
        /// Spawns a new cone in the arena based on the current mode (Random, Seeded, or Pattern).
        /// **The core cone logic is preserved with the original function name.**
        /// </summary>
        private void RespawnConeLogic()
        {
            // 1. Destroy old cone
            if (currentCone != null)
            {
                Destroy(currentCone);
            }

            // 2. Determine cone's local position relative to arCenterPoint
            Vector3 newConeLocalPosition;
            // Only use Sports Pattern if it's set AND we are in Personal Training Mode (Challenge mode uses the seed)
            bool usePattern = isSportsPatternMode && gameController.currentMode == GameController.GameMode.PersonalTraining;

            if (usePattern && SportsPattern.isModelBased)
            {
                // Use SportsPattern logic
                newConeLocalPosition = SportsPattern.GetNextPosition();
            }
            else // Random (Adaptive or Seeded Challenge)
            {
                // Get random position (X, Z) within arenaRadius, biased toward farther cones
                float angle = (float)coneRNG.NextDouble() * 360f;
                float distance = (float)coneRNG.NextDouble() * arenaRadius * 0.5f + arenaRadius * 0.5f;

                float x = distance * Mathf.Cos(angle * Mathf.Deg2Rad);
                float z = distance * Mathf.Sin(angle * Mathf.Deg2Rad);

                // Set Y to 0 as it's a local position for a plane-anchored object
                newConeLocalPosition = new Vector3(x, 0f, z);
            }

            // 3. Instantiate the new cone
            // Parenting the cone to the arCenterPoint makes it an AR-anchored object.
            currentCone = Instantiate(conePrefab, Vector3.zero, Quaternion.identity, arCenterPoint.transform);
            currentCone.transform.localPosition = newConeLocalPosition; 


            // 4. Assign score
            ConeData coneData = currentCone.GetComponent<ConeData>();
            float distanceToCenter = newConeLocalPosition.magnitude;

            if (distanceToCenter > arenaRadius * 0.75f) // Farther cones give more points
            {
                coneData.scoreValue = 2; // High-value cone
            }
            else
            {
                coneData.scoreValue = 1; // Standard cone
            }

            // 5. Reset cone timer
            coneTimer = coneTimeout;

            SetFeedbackText($"New Cone! Value: {coneData.scoreValue}");
            currentCone.GetComponent<Renderer>().material.color = Color.yellow;
        }

        private void HandleGameTimer()
        {
            gameTimer -= Time.deltaTime;

            // Start blinking if we hit the LOW_TIME_THRESHOLD and haven't started blinking yet.
            if (gameTimer <= LOW_TIME_THRESHOLD && blinkCoroutine == null)
            {
                blinkCoroutine = StartCoroutine(BlinkTimerText());
            }

            if (gameTimer <= 0)
            {
                gameTimer = 0;
                EndGame();
            }
        }

        private IEnumerator BlinkTimerText()
        {
            // Loop while the game is running and the timer is below the threshold
            while (isGameRunning && gameTimer > 0)
            {
                // Toggle the text color between red and original (or transparent)
                timerText.color = (timerText.color == Color.red) ? originalTimerColor : Color.red;
                // Wait for a short duration
                yield return new WaitForSeconds(0.25f);
            }

            // Ensure the timer text is restored to the original color when loop ends
            timerText.color = originalTimerColor;
            blinkCoroutine = null;
        }

        private void HandleConeTimer()
        {
            coneTimer -= Time.deltaTime;
            if (coneTimer <= 0)
            {
                // Cone timeout: Missed hit, respawn a new one.
                RespawnConeLogic();
            }
        }

        // --- GAME ENDING ---

        /// <summary>
        /// This is the new, primary method called when the game timer hits zero.
        /// It stops the game and triggers the appropriate results display.
        /// </summary>
        private void EndGame()
        {
            isGameRunning = false;
            if (blinkCoroutine != null)
            {
                StopCoroutine(blinkCoroutine);
                timerText.color = originalTimerColor;
                blinkCoroutine = null;
            }

            // Clean up the scene
            if (currentCone != null)
            {
                Destroy(currentCone);
            }
            if (arCenterPoint != null)
            {
                Destroy(arCenterPoint);
            }

            // We must get the persistent managers again
            firebaseManager = FindObjectOfType<FirebaseManager>();

            // Persist data based on Game Mode
            if (gameController.currentMode == GameController.GameMode.PersonalTraining)
            {
                // 1. UPLOAD TRAINING SCORE 
                if (firebaseManager != null)
                {
                    firebaseManager.UploadPersonalGameSummary(totalScore, coneTimeout);
                }

                // Save the final difficulty for next session start.
                PlayerPrefs.SetFloat("LastDifficulty", coneTimeout);
                PlayerPrefs.Save();

                // 2. SHOW TRAINING PANEL
                ShowEndGameResults(totalScore, coneTimeout);
            }
            else if (gameController.currentMode == GameController.GameMode.Challenge)
            {
                // 1. UPLOAD CHALLENGE SCORE
                if (firebaseManager != null)
                {
                    firebaseManager.UploadScoreForChallenge(
                        gameController.currentGameSeed,
                        FirebaseManager.UserId, // Use the static ID getter
                        totalScore);
                }

                // 2. LISTEN FOR OPPONENT'S SCORE
                // We must now wait for the opponent's score to arrive before we can show results.
                feedbackText.text = "Game Over! Waiting for opponent's score...";
                StartCoroutine(ListenForOpponentScore());
            }
        }

        /// <summary>
        /// Coroutine to listen for the opponent's score in a Challenge.
        /// </summary>
        private IEnumerator ListenForOpponentScore()
        {
            DatabaseReference challengeScoresRef = firebaseManager.GetChallengeScoresReference(gameController.currentGameSeed);

            bool opponentScoreFound = false;
            int opponentScore = 0;
            string opponentName = "OPPONENT"; // Default name
            float waitTimer = 30f; // Wait a max of 30 seconds

            while (waitTimer > 0 && !opponentScoreFound)
            {
                // Check Firebase for scores
                var task = challengeScoresRef.GetValueAsync();
                yield return new WaitUntil(() => task.IsCompleted);

                if (task.IsCompletedSuccessfully)
                {
                    DataSnapshot snapshot = task.Result;
                    if (snapshot.Exists && snapshot.ChildrenCount > 1) // Need more than just our own score
                    {
                        foreach (var child in snapshot.Children)
                        {
                            if (child.Key != FirebaseManager.UserId) // Find a score that is NOT ours
                            {
                                opponentScore = Convert.ToInt32(child.Child("score").Value);
                                opponentScoreFound = true;
                                break;
                            }
                        }
                    }
                }

                if (!opponentScoreFound)
                {
                    yield return new WaitForSeconds(2f); // Wait 2 seconds and check again
                    waitTimer -= 2f;
                }
            }

            // After loop, show results (even if opponent score wasn't found)
            feedbackText.text = ""; // Clear "waiting" message

            FinalChallengeResult results = new FinalChallengeResult
            {
                localScore = totalScore,
                opponentScore = opponentScore, // Will be 0 if not found
                opponentName = opponentName
            };

            ShowEndGameResults(0, 0, results);
        }


        // --- HIT DETECTION ---

        /// <summary>
        /// Public method called by the Cone when it is touched (e.g., from a separate `ConeTouchHandler.cs`).
        /// </summary>
        public void OnConeHit(GameObject cone)
        {
            if (!isGameRunning || cone != currentCone) return; // Only process the currently active cone

            // 1. Get score
            ConeData coneData = cone.GetComponent<ConeData>();
            int score = coneData != null ? coneData.scoreValue : 1;
            totalScore += score;

            // 2. Record hit time (time elapsed since cone spawned)
            float timeToHit = coneTimeout - coneTimer;
            UserData.RecordHit(timeToHit, score);

            // 3. Update feedback
            SetFeedbackText($"HIT! +{score} points. Time: {timeToHit:F2}s. Total: {totalScore}");

            // 4. Adjust difficulty (only in personal training)
            if (gameController.currentMode == GameController.GameMode.PersonalTraining)
            {
                AdjustDifficulty(timeToHit);
            }

            // 5. Respawn next cone
            RespawnConeLogic();
        }

        // --- UTILITY ---

        private void UpdateTimerUI()
        {
            timerText.text = $"Time: {gameTimer:F0}s";
            scoreText.text = $"Score: {totalScore}";
        }

        private void SetFeedbackText(string message)
        {
            feedbackText.text = message;
        }

        // --- DIFFICULTY LOGIC ---

        /// <summary>
        /// Adjusts the coneTimeout (difficulty level) based on a weighted average of recent performance.
        /// Only runs in Personal Training mode (reads mode from GameController).
        /// </summary>
        private void AdjustDifficulty(float timeToHit)
        {
            // Skip adaptive logic if not in Personal Training Mode
            if (gameController.currentMode != GameController.GameMode.PersonalTraining) return;

            const int WINDOW_SIZE = 50;
            List<(float time, int score)> allHits = UserData.GetAllHits();
            // Get the last WINDOW_SIZE hits
            List<(float time, int score)> recentHits = allHits.Skip(Mathf.Max(0, allHits.Count - WINDOW_SIZE)).ToList();

            if (recentHits.Count < 10) return;

            // --- Data Cleaning (Filtering Outliers) ---
            recentHits = recentHits.Where(h => h.time >= 0.2f && h.time <= 15.0f).ToList();

            // --- Weighted Average Calculation (Faster Time + Higher Score = Higher Weight) ---
            float weightedSum = 0f;
            float totalWeight = 0f;
            const float BASELINE_TIME = 5.0f;

            foreach (var hit in recentHits)
            {
                // timeWeight is higher for faster times (lower hit.time)
                float timeWeight = Mathf.Clamp(BASELINE_TIME / hit.time, 0.1f, 5.0f);
                float finalWeight = timeWeight * hit.score;

                // This calculates the weighted average *time*
                weightedSum += hit.time * finalWeight;
                totalWeight += finalWeight;
            }

            if (totalWeight == 0) return;

            float recentWeightedAverageTime = weightedSum / totalWeight;

            // --- DIFFICULTY ADJUSTMENT ---
            // Target: 2.5s +/- 0.5s
            const float TARGET_AVG_TIME = 2.5f;

            // Compare recent performance to the target
            if (recentWeightedAverageTime < TARGET_AVG_TIME - 0.5f) // User is very fast (faster than 2.0s average)
            {
                // Increase difficulty (Decrease coneTimeout)
                coneTimeout = Mathf.Max(minTimeout, coneTimeout - difficultyAdjustmentFactor * 2);
                UnityEngine.Debug.Log($"Difficulty UP. Avg Time: {recentWeightedAverageTime:F2}s. New Timeout: {coneTimeout:F2}s.");
            }
            else if (recentWeightedAverageTime < TARGET_AVG_TIME) // User is fast (average 2.0s - 2.5s)
            {
                // Increase difficulty slightly
                coneTimeout = Mathf.Max(minTimeout, coneTimeout - difficultyAdjustmentFactor);
                UnityEngine.Debug.Log($"Difficulty UP (slight). Avg Time: {recentWeightedAverageTime:F2}s. New Timeout: {coneTimeout:F2}s.");
            }
            else if (recentWeightedAverageTime > TARGET_AVG_TIME + 0.5f) // User is very slow (slower than 3.0s average)
            {
                // Decrease difficulty (Increase coneTimeout)
                coneTimeout = Mathf.Min(maxTimeout, coneTimeout + difficultyAdjustmentFactor * 2);
                UnityEngine.Debug.Log($"Difficulty DOWN. Avg Time: {recentWeightedAverageTime:F2}s. New Timeout: {coneTimeout:F2}s.");
            }
            else // User is in the target range (2.5s +/- 0.5s)
            {
                // Maintain difficulty
                UnityEngine.Debug.Log($"Difficulty maintained. Avg Time: {recentWeightedAverageTime:F2}s.");
            }
        }
    }