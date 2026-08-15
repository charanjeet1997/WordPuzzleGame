#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using UnityEngine.Audio;
using Image = UnityEngine.UI.Image;
using TMPro;
using Games.WorldSystem;
using CommanTickManager;
using Game.Factories;
using WordPuzzle.Data;
using WordPuzzle.Factory;
using WordPuzzle.Particles;
using WordPuzzle.Gameplay;
using WordPuzzle.Managers;
using WordPuzzle.Services;
using WordPuzzle.UI;
using WordPuzzle.Audio;

namespace WordPuzzle.Editor
{
    public static class WordPuzzleSceneSetup
    {
        // Breathing room between the outermost letter node and the wheel backdrop edge, in world units.
        private const float BackdropPadding = 0.1f;

        [MenuItem("WordPuzzle/Reset All Saved Progress (PlayerPrefs)")]
        public static void ResetAllSavedProgress()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("<color=green>[WordPuzzle]</color> All saved level states, coins, and progress have been completely reset!");
        }

        [MenuItem("WordPuzzle/Setup Wonders of Word Scene")]
        public static void BuildSceneSetup()
        {
            // 1. Ensure Directory Structure (NO Resources folder used!)
            EnsureDirectories();

            // 2. Cleanup Legacy Standalone Objects outside of Managers
            CleanupLegacyStandaloneManagerObjects();

            // 3. Build & Save Factory Prefabs in Assets/WordPuzzle/Prefabs
            GameObject gridTilePrefab = CreateOrUpdateGridTilePrefab();
            GameObject letterNodePrefab = CreateOrUpdateLetterNodePrefab();
            // One prefab per FXType, in enum order - the factory is indexed by that enum.
            ParticleFX[] particleFXPrefabs = CreateOrUpdateParticleFXPrefabs();

            // 4. Build & Save World Prefab (WordPuzzleWorld.prefab)
            GameObject worldPrefab = CreateOrUpdateWorldPrefab(gridTilePrefab, letterNodePrefab);

            // 5. Create & Register WorldDatabase Asset in Data/SO
            WorldDatabase worldDB = CreateOrGetWorldDatabase(worldPrefab);

            // 6. Create PanelSettings Asset in Data/SO
            PanelSettings panelSettings = GetOrCreatePanelSettings();

            // 7. Create Level Data Assets & UI Toolkit View Config Assets in Data/SO
            List<LevelData> levelAssets = CreateSampleLevelAssets();
            LevelDatabase levelDatabase = GetOrCreateLevelDatabase(levelAssets);
            ViewConfig configSplashScreen = CreateOrGetViewConfig("ViewConfig_SplashScreen", "SplashScreen", "WordPuzzle.UI.SplashScreenView", panelSettings, true, "Assets/WordPuzzle/UI/Layouts/SplashScreen.uxml");
            ViewConfig configMainMenu = CreateOrGetViewConfig("ViewConfig_MainMenu", "MainMenu", "WordPuzzle.UI.MainMenuView", panelSettings, true, "Assets/WordPuzzle/UI/Layouts/MainMenu.uxml");
            ViewConfig configHUD = CreateOrGetViewConfig("ViewConfig_HUD", "HUD", "WordPuzzle.UI.HUDView", panelSettings, true, "Assets/WordPuzzle/UI/Layouts/HUD.uxml");
            ViewConfig configPause = CreateOrGetViewConfig("ViewConfig_PauseOverlay", "PauseOverlay", "WordPuzzle.UI.PauseOverlayView", panelSettings, false, "Assets/WordPuzzle/UI/Layouts/PauseOverlay.uxml");
            ViewConfig configLevelComplete = CreateOrGetViewConfig("ViewConfig_LevelComplete", "LevelComplete", "WordPuzzle.UI.LevelCompleteView", panelSettings, false, "Assets/WordPuzzle/UI/Layouts/LevelComplete.uxml");
            ViewConfig configSettings = CreateOrGetViewConfig("ViewConfig_Settings", "Settings", "WordPuzzle.UI.SettingsOverlayView", panelSettings, false, "Assets/WordPuzzle/UI/Layouts/SettingsOverlay.uxml");
            ViewConfig configModeSelect = CreateOrGetViewConfig("ViewConfig_ModeSelect", "ModeSelect", "WordPuzzle.UI.ModeSelectView", panelSettings, false, "Assets/WordPuzzle/UI/Layouts/ModeSelect.uxml");

            // 8. Main Camera setup (Exact Air Hockey Specification)
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                mainCam = camObj.AddComponent<Camera>();
                camObj.tag = "MainCamera";
            }
            mainCam.transform.position = new Vector3(0f, 0f, -0.2f);
            mainCam.orthographic = true;
            mainCam.orthographicSize = 4.5f;
            mainCam.nearClipPlane = 0.02f;
            mainCam.backgroundColor = new Color(0.04f, 0.06f, 0.12f);
            mainCam.clearFlags = CameraClearFlags.SolidColor;

            if (mainCam.GetComponent<AudioListener>() == null)
            {
                mainCam.gameObject.AddComponent<AudioListener>();
            }

            // 8b. High-Res 2D Background Sprite
            GameObject bgObj = GameObject.Find("SceneBackground");
            if (bgObj == null)
            {
                bgObj = new GameObject("SceneBackground");
            }
            Sprite bgSpr = Resources.Load<Sprite>("Sprites/game_background");
            if (bgSpr == null)
            {
                bgSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/WordPuzzle/Sprites/game_background.png");
            }
            if (bgSpr != null)
            {
                SpriteRenderer bgSR = GetOrAddComponent<SpriteRenderer>(bgObj);
                bgSR.sprite = bgSpr;
                bgSR.sortingOrder = -10;
                bgObj.transform.position = new Vector3(0f, 0f, 10f);
                bgObj.transform.localScale = Vector3.one;
            }

            // 9. ProcessingUpdate (CommanTickManager)
            GameObject processingUpdateObj = GameObject.Find("ProcessingUpdate");
            if (processingUpdateObj == null)
            {
                processingUpdateObj = new GameObject("ProcessingUpdate");
                processingUpdateObj.AddComponent<ProcessingUpdate>();
            }

            // 10. WorldManager in scene
            GameObject worldMgrObj = GameObject.Find("WorldManager");
            if (worldMgrObj == null)
            {
                worldMgrObj = new GameObject("WorldManager");
            }
            WorldManager worldManager = GetOrAddComponent<WorldManager>(worldMgrObj);
            SerializedObject wmSO = new SerializedObject(worldManager);
            wmSO.FindProperty("_worldDatabase").objectReferenceValue = worldDB;
            wmSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(worldManager);

            // 11. Single Centralized Managers GameObject (Matching Air Hockey Architecture)
            GameObject managersObj = GameObject.Find("Managers");
            if (managersObj == null)
            {
                managersObj = new GameObject("Managers");
            }

            ProgressionService progressionService = GetOrAddComponent<ProgressionService>(managersObj);
            ParticleService particleService = GetOrAddComponent<ParticleService>(managersObj);

            // Dictionary meanings shown on the level-complete screen. Loads its JSON off the
            // first frame, so having it in the scene costs nothing at boot.
            GetOrAddComponent<WordDefinitionService>(managersObj);
            GameManager gameManager = GetOrAddComponent<GameManager>(managersObj);
            WordPuzzleInitializer initializer = GetOrAddComponent<WordPuzzleInitializer>(managersObj);

            // Child 1: Data GameObject -> contains Data references
            Transform dataChild = managersObj.transform.Find("Data");
            GameObject dataObj = dataChild != null ? dataChild.gameObject : new GameObject("Data");
            dataObj.transform.SetParent(managersObj.transform);

            // Child 2: Audio GameObject -> contains AudioManager and channel AudioSources
            Transform audioChild = managersObj.transform.Find("Audio");
            GameObject audioObj = audioChild != null ? audioChild.gameObject : new GameObject("Audio");
            audioObj.transform.SetParent(managersObj.transform);

            AudioManager audioManager = GetOrAddComponent<AudioManager>(audioObj);
            SetupAudioManager(audioManager);

            // Wire Audio Clips directly to AudioManager from Assets/WordPuzzle/Sounds/
            string soundPath = "Assets/WordPuzzle/Sounds";
            audioManager.clipSwipeChar = AssetDatabase.LoadAssetAtPath<AudioClip>($"{soundPath}/swipe_char.wav");
            audioManager.clipWordMatched = AssetDatabase.LoadAssetAtPath<AudioClip>($"{soundPath}/word_matched.wav");
            audioManager.clipWrongWord = AssetDatabase.LoadAssetAtPath<AudioClip>($"{soundPath}/wrong_word.wav");
            audioManager.clipBonusWord = AssetDatabase.LoadAssetAtPath<AudioClip>($"{soundPath}/bonus_word.wav");
            audioManager.clipHint = AssetDatabase.LoadAssetAtPath<AudioClip>($"{soundPath}/hint.wav");
            audioManager.clipShuffle = AssetDatabase.LoadAssetAtPath<AudioClip>($"{soundPath}/shuffle.wav");
            audioManager.clipFanfare = AssetDatabase.LoadAssetAtPath<AudioClip>($"{soundPath}/level_complete.wav");
            audioManager.clipButtonClick = AssetDatabase.LoadAssetAtPath<AudioClip>($"{soundPath}/button_click.wav");
            EditorUtility.SetDirty(audioManager);

            SetupMusicPlayer(audioObj);

            // Child 3: Factory GameObject -> factory manager singletons + the registration controller.
            // The managers are framework singletons; without them in the scene RegisterFactoryByType
            // hits its "FactoryManager instance not found" guard and every factory silently fails.
            Transform factoryChild = managersObj.transform.Find("Factory");
            GameObject factoryObj = factoryChild != null ? factoryChild.gameObject : new GameObject("Factory");
            factoryObj.transform.SetParent(managersObj.transform);

            GetOrAddComponent<FactoryManagerByType>(factoryObj);
            GetOrAddComponent<FactoryManagerByName>(factoryObj);

            WordPuzzleFactoryController factoryController = GetOrAddComponent<WordPuzzleFactoryController>(factoryObj);
            SerializedObject factorySO = new SerializedObject(factoryController);
            ConfigureFactory(factorySO, "gridTileConfig", "GridTile", gridTilePrefab.GetComponent<GridTile>());
            ConfigureFactory(factorySO, "letterNodeConfig", "LetterNode", letterNodePrefab.GetComponent<LetterNode>());
            ConfigureFactory(factorySO, "particleFXConfig", "ParticleFX", particleFXPrefabs);
            factorySO.ApplyModifiedProperties();
            EditorUtility.SetDirty(factoryController);

            // Child 4: UI GameObject -> contains UIDocument & UIManager
            Transform uiChild = managersObj.transform.Find("UI");
            GameObject uiObj = uiChild != null ? uiChild.gameObject : new GameObject("UI");
            uiObj.transform.SetParent(managersObj.transform);

            UIDocument uiDoc = GetOrAddComponent<UIDocument>(uiObj);
            uiDoc.panelSettings = panelSettings;
            EditorUtility.SetDirty(uiDoc);

            UIManager uiManager = GetOrAddComponent<UIManager>(uiObj);
            uiManager.defaultPanelSettings = panelSettings;
            EditorUtility.SetDirty(uiManager);

            // 12. Instantiate World from WorldPrefab in scene if not present
            GameObject worldInstance = GameObject.Find("WordPuzzleWorld");
            if (worldInstance == null && worldPrefab != null)
            {
                worldInstance = PrefabUtility.InstantiatePrefab(worldPrefab) as GameObject;
            }

            GameplayHandler gameplayHandler = worldInstance != null ? worldInstance.GetComponentInChildren<GameplayHandler>() : null;
            if (gameplayHandler != null)
            {
                if (gameplayHandler.gridController != null)
                {
                    EditorUtility.SetDirty(gameplayHandler.gridController);
                }
                if (gameplayHandler.wheelController != null)
                {
                    if (gameplayHandler.wheelController.lineRenderer == null)
                    {
                        gameplayHandler.wheelController.lineRenderer = gameplayHandler.wheelController.GetComponent<LineRenderer>() ?? gameplayHandler.wheelController.gameObject.AddComponent<LineRenderer>();
                    }
                    EditorUtility.SetDirty(gameplayHandler.wheelController);
                }
                EditorUtility.SetDirty(gameplayHandler);
            }

            // Wire Direct Asset References to GameManager & Initializer
            gameManager.worldName = WorldName.WordPuzzleWorld;
            gameManager.levelDatabase = levelDatabase;
            gameManager.levels = levelAssets;
            gameManager.configMainMenu = configMainMenu;
            gameManager.configHUD = configHUD;
            gameManager.configPause = configPause;
            gameManager.configLevelComplete = configLevelComplete;
            gameManager.configSettings = configSettings;
            gameManager.configModeSelect = configModeSelect;
            EditorUtility.SetDirty(gameManager);

            initializer.configSplashScreen = configSplashScreen;
            initializer.configMainMenu = configMainMenu;
            EditorUtility.SetDirty(initializer);

            // Configure App Icon in Unity PlayerSettings if asset is present
            Texture2D appIconTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/WordPuzzle/Sprites/AppIcon.png");
            if (appIconTex != null)
            {
                PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new Texture2D[] { appIconTex });
            }

            Undo.RegisterCreatedObjectUndo(managersObj, "Setup Wonders of Word Scene");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var currentScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(currentScene);
            if (!string.IsNullOrEmpty(currentScene.path))
            {
                EditorSceneManager.SaveScene(currentScene);
            }

            Debug.Log("<color=green>[Wonders of Word Setup]</color> Complete end-to-end setup finished! 100% of Inspector fields assigned to AudioManager and saved to scene.");
        }

        private static void CleanupLegacyStandaloneManagerObjects()
        {
            string[] legacyNames = new string[] { "WondersOfWord_Root", "GameManager", "UIManager", "AudioManager", "WordPuzzleFactory", "AudioService" };
            foreach (string name in legacyNames)
            {
                GameObject obj = GameObject.Find(name);
                if (obj != null && (obj.transform.parent == null || obj.transform.parent.name != "Managers"))
                {
                    Undo.DestroyObjectImmediate(obj);
                }
            }

            // Clean up old AudioService component on Managers if present
            GameObject managersObj = GameObject.Find("Managers");
            if (managersObj != null)
            {
                Component legacyAudioService = managersObj.GetComponent("AudioService");
                if (legacyAudioService != null)
                {
                    Undo.DestroyObjectImmediate(legacyAudioService);
                }
            }
        }

        private static GameObject CreateOrUpdateGridTilePrefab()
        {
            string path = "Assets/WordPuzzle/Prefabs/GridTilePrefab.prefab";
            GameObject temp = new GameObject("GridTilePrefab");

            Sprite hiddenSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/WordPuzzle/Sprites/grid_tile_hidden.png");
            Sprite revealedSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/WordPuzzle/Sprites/grid_tile_revealed.png");

            SpriteRenderer sr = temp.AddComponent<SpriteRenderer>();
            sr.sprite = hiddenSpr;
            sr.sortingOrder = 5;

            GameObject textObj = new GameObject("LetterText");
            textObj.transform.SetParent(temp.transform, false);
            TextMeshPro tmpText = textObj.AddComponent<TextMeshPro>();
            tmpText.alignment = TextAlignmentOptions.Center;
            tmpText.fontSize = 5.5f;
            tmpText.fontWeight = FontWeight.Bold;
            tmpText.color = new Color(0.08f, 0.12f, 0.22f, 1f);
            tmpText.sortingOrder = 6;

            GridTile tileComp = temp.AddComponent<GridTile>();
            tileComp.tileSprite = sr;
            tileComp.letterTextMesh = tmpText;
            tileComp.hiddenTileSprite = hiddenSpr;
            tileComp.revealedTileSprite = revealedSpr;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        private static GameObject CreateOrUpdateLetterNodePrefab()
        {
            string path = "Assets/WordPuzzle/Prefabs/LetterNodePrefab.prefab";
            GameObject temp = new GameObject("LetterNodePrefab");

            Sprite normalSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/WordPuzzle/Sprites/letter_node_normal.png");
            Sprite selectedSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/WordPuzzle/Sprites/letter_node_selected.png");

            SpriteRenderer sr = temp.AddComponent<SpriteRenderer>();
            sr.sprite = normalSpr;
            sr.sortingOrder = 5;

            GameObject textObj = new GameObject("LetterText");
            textObj.transform.SetParent(temp.transform, false);
            TextMeshPro tmpText = textObj.AddComponent<TextMeshPro>();
            tmpText.alignment = TextAlignmentOptions.Center;
            tmpText.fontSize = 6f;
            tmpText.fontWeight = FontWeight.Bold;
            tmpText.color = Color.white;
            tmpText.sortingOrder = 6;

            LetterNode nodeComp = temp.AddComponent<LetterNode>();
            nodeComp.bgSprite = sr;
            nodeComp.letterTextMesh = tmpText;
            nodeComp.normalSprite = normalSpr;
            nodeComp.selectedSprite = selectedSpr;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        /// <summary>
        /// One prefab per <see cref="FXType"/>, returned in enum order because the factory is
        /// indexed by that enum. Only a single shared prefab existed before, so three of the
        /// four effects could never be created.
        /// </summary>
        private static ParticleFX[] CreateOrUpdateParticleFXPrefabs()
        {
            return new[]
            {
                // TileReveal - cheapest emitter in the game: it fires once per revealed tile.
                BuildParticleFXPrefab("FX_TileReveal", new Color(1f, 1f, 1f, 1f),
                    burst: 8, lifetime: 0.35f, speed: 1.2f, size: 0.12f, radius: 0.1f, gravity: -0.2f),

                // WordMatchBurst - the main reward pop, at the centre of the matched word.
                BuildParticleFXPrefab("FX_WordMatchBurst", new Color(0.45f, 0.9f, 1f, 1f),
                    burst: 24, lifetime: 0.5f, speed: 2.6f, size: 0.18f, radius: 0.15f, gravity: 0.15f),

                // BonusWordSparkle - gold, deliberately a different colour language to targets.
                BuildParticleFXPrefab("FX_BonusWordSparkle", new Color(1f, 0.82f, 0.25f, 1f),
                    burst: 16, lifetime: 0.7f, speed: 1.8f, size: 0.15f, radius: 0.25f, gravity: -0.1f),

                // LevelCompleteFireworks - celebratory radial fireworks.
                BuildParticleFXPrefab("FX_LevelCompleteFireworks", new Color(1f, 0.6f, 0.85f, 1f),
                    burst: 40, lifetime: 1.4f, speed: 4.5f, size: 0.2f, radius: 0.1f, gravity: 0.5f),

                // Confetti - colorful cascading celebration confetti with 3D tumbling paper pieces.
                BuildConfettiParticleFXPrefab("FX_Confetti")
            };
        }

        private static ParticleFX BuildConfettiParticleFXPrefab(string name)
        {
            string path = $"Assets/WordPuzzle/Prefabs/{name}.prefab";
            GameObject temp = new GameObject(name);

            // 1. Primary Confetti Emitter (Ribbon & Paper pieces)
            ParticleSystem ps = temp.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 2.4f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 2.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4.5f, 8.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.28f);
            main.gravityModifier = 0.42f;
            main.maxParticles = 80;
            main.loop = false;
            main.stopAction = ParticleSystemStopAction.None;
            main.playOnAwake = false;

            // Enable 3D tumbling rotation on all axes
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2);

            // Rich 8-Color Festive Celebration Palette (Bright saturated colors)
            var gradient = new Gradient();
            gradient.mode = GradientMode.Fixed; // Pure discrete confetti colors for each paper flake
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1f, 0.88f, 0.05f), 0.0f),    // Sunshine Gold
                    new GradientColorKey(new Color(1f, 0.15f, 0.25f), 0.14f),   // Vivid Ruby Red
                    new GradientColorKey(new Color(1f, 0.08f, 0.65f), 0.28f),   // Neon Magenta
                    new GradientColorKey(new Color(0f, 0.92f, 1f), 0.42f),      // Electric Sky Cyan
                    new GradientColorKey(new Color(0.05f, 1f, 0.45f), 0.57f),   // Neon Emerald
                    new GradientColorKey(new Color(1f, 0.52f, 0.05f), 0.71f),   // Sunset Tangerine
                    new GradientColorKey(new Color(0.65f, 0.2f, 1f), 0.85f),    // Royal Violet
                    new GradientColorKey(new Color(1f, 0.98f, 0.7f), 1.0f)      // Shimmer Gold
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.85f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            main.startColor = new ParticleSystem.MinMaxGradient(gradient) { mode = ParticleSystemGradientMode.RandomColor };

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 65) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 45f;
            shape.radius = 0.35f;
            temp.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

            // Fast continuous 3D tumbling rotation
            var rotOverLifetime = ps.rotationOverLifetime;
            rotOverLifetime.enabled = true;
            rotOverLifetime.separateAxes = true;
            rotOverLifetime.x = new ParticleSystem.MinMaxCurve(-9f, 9f);
            rotOverLifetime.y = new ParticleSystem.MinMaxCurve(-10f, 10f);
            rotOverLifetime.z = new ParticleSystem.MinMaxCurve(-8f, 8f);

            // Air drag / velocity damping: explosive blast that softly floats
            var limitVel = ps.limitVelocityOverLifetime;
            limitVel.enabled = true;
            limitVel.dampen = 0.3f;
            limitVel.limit = new ParticleSystem.MinMaxCurve(2.0f);

            // Organic flutter turbulence (simulates realistic fluttering paper on air)
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.65f;
            noise.frequency = 0.45f;
            noise.scrollSpeed = 0.7f;
            noise.damping = true;

            // Size curve: quick pop in, subtle pulse, smooth fade
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 0.4f);
            sizeCurve.AddKey(0.1f, 1.1f);
            sizeCurve.AddKey(0.25f, 1.0f);
            sizeCurve.AddKey(0.85f, 0.95f);
            sizeCurve.AddKey(1f, 0.05f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            ParticleSystemRenderer psRenderer = temp.GetComponent<ParticleSystemRenderer>();
            Material confettiMat = ParticleMaterialUtility.GetConfettiMaterial();
            if (confettiMat != null)
            {
                psRenderer.sharedMaterial = confettiMat;
            }
            else
            {
                ParticleMaterialUtility.EnsureValidMaterial(temp);
            }
            psRenderer.sortingOrder = 20;

            // 2. Secondary Magical Sparkle Child Emitter
            GameObject sparklesObj = new GameObject("ConfettiSparkles");
            sparklesObj.transform.SetParent(temp.transform, false);
            ParticleSystem sparklePS = sparklesObj.AddComponent<ParticleSystem>();
            var sMain = sparklePS.main;
            sMain.duration = 1.6f;
            sMain.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
            sMain.startSpeed = new ParticleSystem.MinMaxCurve(2.0f, 6.0f);
            sMain.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
            sMain.gravityModifier = 0.15f;
            sMain.maxParticles = 30;
            sMain.loop = false;
            sMain.stopAction = ParticleSystemStopAction.None;
            sMain.playOnAwake = false;

            var sGradient = new Gradient();
            sGradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1f, 0.95f, 0.4f), 0.0f),  // Bright Gold Sparkle
                    new GradientColorKey(new Color(0.4f, 0.95f, 1f), 0.5f),  // Cyan Sparkle
                    new GradientColorKey(new Color(1f, 0.6f, 0.9f), 1.0f)   // Pink Sparkle
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            sMain.startColor = new ParticleSystem.MinMaxGradient(sGradient) { mode = ParticleSystemGradientMode.RandomColor };

            var sEmission = sparklePS.emission;
            sEmission.rateOverTime = 0;
            sEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 25) });

            var sShape = sparklePS.shape;
            sShape.shapeType = ParticleSystemShapeType.Sphere;
            sShape.radius = 0.25f;

            ParticleSystemRenderer sRenderer = sparklesObj.GetComponent<ParticleSystemRenderer>();
            Material defaultMat = ParticleMaterialUtility.GetMaterial();
            if (defaultMat != null) sRenderer.sharedMaterial = defaultMat;
            sRenderer.sortingOrder = 21;

            temp.AddComponent<ParticleFX>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            return prefab.GetComponent<ParticleFX>();
        }

        private static ParticleFX BuildParticleFXPrefab(string name, Color color, int burst,
            float lifetime, float speed, float size, float radius, float gravity)
        {
            string path = $"Assets/WordPuzzle/Prefabs/{name}.prefab";
            GameObject temp = new GameObject(name);

            ParticleSystem ps = temp.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = lifetime;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = color;
            main.gravityModifier = gravity;
            // Hard ceiling per prefab so no effect can blow the mobile particle budget.
            main.maxParticles = burst;
            // Must be non-looping: stopAction never fires on a system that never stops.
            main.loop = false;
            // Not Destroy: these instances are pooled and handed back to the factory. The old
            // prefab set Destroy, which only survived because ParticleFX.Init overrode it.
            main.stopAction = ParticleSystemStopAction.None;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)burst) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;

            // Off by default - each of these costs a separate pass on mobile.
            var collision = ps.collision; collision.enabled = false;
            var trails = ps.trails; trails.enabled = false;
            var lights = ps.lights; lights.enabled = false;
            var noise = ps.noise; noise.enabled = false;

            // AddComponent leaves the built-in Default-ParticleSystem material, whose shader
            // does not exist under URP and renders magenta.
            ParticleMaterialUtility.EnsureValidMaterial(temp);

            // Required so the effect can be pooled through FactoryManagerByType,
            // which constrains its generic to MonoBehaviour (ParticleSystem is not one).
            temp.AddComponent<ParticleFX>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            return prefab.GetComponent<ParticleFX>();
        }

        private static GameObject CreateOrUpdateWorldPrefab(GameObject gridTilePrefab, GameObject letterNodePrefab)
        {
            string path = "Assets/WordPuzzle/Prefabs/WordPuzzleWorld.prefab";
            GameObject temp = new GameObject("WordPuzzleWorld");

            World worldComp = temp.AddComponent<World>();

            GameObject gameplayObj = new GameObject("GameplayHandler");
            gameplayObj.transform.SetParent(temp.transform, false);

            GameObject gridObj = new GameObject("CrosswordGrid");
            gridObj.transform.SetParent(gameplayObj.transform, false);
            gridObj.transform.localPosition = new Vector3(0f, 1.85f, 0f);
            CrosswordGridController gridController = gridObj.AddComponent<CrosswordGridController>();

            GameObject wheelObj = new GameObject("LetterWheel");
            wheelObj.transform.SetParent(gameplayObj.transform, false);
            wheelObj.transform.localPosition = new Vector3(0f, -1.85f, 0f);

            LetterWheelController wheelController = wheelObj.AddComponent<LetterWheelController>();
            wheelController.lineRenderer = wheelObj.GetComponent<LineRenderer>() ?? wheelObj.AddComponent<LineRenderer>();

            Sprite backdropSpr = Resources.Load<Sprite>("Sprites/wheel_backdrop");
            if (backdropSpr == null)
            {
                backdropSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/WordPuzzle/Sprites/wheel_backdrop.png");
            }
            if (backdropSpr != null)
            {
                GameObject backdropObj = new GameObject("WheelBackdrop");
                backdropObj.transform.SetParent(wheelObj.transform, false);
                SpriteRenderer backdropSR = backdropObj.AddComponent<SpriteRenderer>();
                backdropSR.sprite = backdropSpr;
                backdropSR.sortingOrder = 2;

                // Size the backdrop from the node ring it has to contain, not a magic number:
                // outermost node edge + padding, converted out of the sprite's native world size.
                float ringOuterRadius = wheelController.wheelRadius + wheelController.nodeSize * 0.5f;
                float backdropRadius = ringOuterRadius + BackdropPadding;
                float nativeSize = backdropSpr.bounds.size.x;
                float backdropScale = nativeSize > 0f ? (backdropRadius * 2f) / nativeSize : 1f;
                backdropObj.transform.localScale = new Vector3(backdropScale, backdropScale, 1f);
            }

            GameplayHandler handlerComp = gameplayObj.AddComponent<GameplayHandler>();
            handlerComp.wheelController = wheelController;
            handlerComp.gridController = gridController;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        private static WorldDatabase CreateOrGetWorldDatabase(GameObject worldPrefab)
        {
            string path = "Assets/WordPuzzle/Data/SO/WorldDatabase.asset";
            WorldDatabase db = AssetDatabase.LoadAssetAtPath<WorldDatabase>(path);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<WorldDatabase>();
                AssetDatabase.CreateAsset(db, path);
            }

            if (worldPrefab != null)
            {
                World w = worldPrefab.GetComponent<World>();
                if (w != null)
                {
                    SerializedObject so = new SerializedObject(db);
                    SerializedProperty worldsProp = so.FindProperty("worlds");
                    if (worldsProp != null)
                    {
                        bool found = false;
                        for (int i = 0; i < worldsProp.arraySize; i++)
                        {
                            SerializedProperty item = worldsProp.GetArrayElementAtIndex(i);
                            SerializedProperty nameProp = item.FindPropertyRelative("worldName");
                            if (nameProp != null && nameProp.enumValueIndex == (int)WorldName.WordPuzzleWorld)
                            {
                                item.FindPropertyRelative("world").objectReferenceValue = w;
                                found = true;
                                break;
                            }
                        }
                        if (!found)
                        {
                            worldsProp.arraySize++;
                            SerializedProperty item = worldsProp.GetArrayElementAtIndex(worldsProp.arraySize - 1);
                            item.FindPropertyRelative("worldName").enumValueIndex = (int)WorldName.WordPuzzleWorld;
                            item.FindPropertyRelative("world").objectReferenceValue = w;
                        }
                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(db);
                    }
                }
            }

            return db;
        }

        private static PanelSettings GetOrCreatePanelSettings()
        {
            string path = "Assets/WordPuzzle/Data/SO/DefaultPanelSettings.asset";
            PanelSettings panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (panelSettings == null)
            {
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panelSettings.referenceResolution = new Vector2Int(1080, 1920);
                panelSettings.match = 0.5f;
                AssetDatabase.CreateAsset(panelSettings, path);
            }
            return panelSettings;
        }

        /// <summary>
        /// Adds the playlist alongside AudioManager - it plays through the existing
        /// AudioSource_Background channel, so there is no second source and no second mixer
        /// routing to keep in sync. Every clip in Assets/WordPuzzle/Audio/Music is picked up,
        /// so dropping a new loop into that folder and re-running setup adds it to the rotation.
        /// </summary>
        private static void SetupMusicPlayer(GameObject audioObj)
        {
            const string musicFolder = "Assets/WordPuzzle/Audio/Music";

            // A leftover child from the earlier separate-source layout would keep a stale
            // MusicPlayer in the scene, and two playlists would fight over one channel.
            Transform legacyChild = audioObj.transform.Find("MusicPlayer");
            if (legacyChild != null) Undo.DestroyObjectImmediate(legacyChild.gameObject);

            MusicPlayer player = GetOrAddComponent<MusicPlayer>(audioObj);

            var tracks = new List<AudioClip>();
            if (AssetDatabase.IsValidFolder(musicFolder))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { musicFolder }))
                {
                    AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
                    if (clip != null) tracks.Add(clip);
                }
            }

            player.EditorSetTracks(tracks);
            EditorUtility.SetDirty(player);

            if (tracks.Count == 0)
            {
                Debug.LogWarning($"[Wonders of Word Setup] No music clips found in {musicFolder} - the playlist will be silent.");
            }
        }

        private static void SetupAudioManager(AudioManager audioManager)
        {
            if (audioManager == null) return;

            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/WordPuzzle/Audio Mixers/WordPuzzleAudioMixer.mixer");

            SerializedObject so = new SerializedObject(audioManager);
            SerializedProperty listProp = so.FindProperty("audioSourceList");
            if (listProp != null)
            {
                listProp.ClearArray();

                (WordPuzzle.Audio.AudioType type, string name)[] channelTypes = new[]
                {
                    (WordPuzzle.Audio.AudioType.SFX, "AudioSource_SFX"),
                    (WordPuzzle.Audio.AudioType.UI, "AudioSource_UI"),
                    (WordPuzzle.Audio.AudioType.Background, "AudioSource_Background")
                };

                foreach (var ch in channelTypes)
                {
                    Transform child = audioManager.transform.Find(ch.name);
                    GameObject childObj = child != null ? child.gameObject : new GameObject(ch.name);
                    childObj.transform.SetParent(audioManager.transform);

                    AudioSource src = childObj.GetComponent<AudioSource>();
                    if (src == null)
                    {
                        src = childObj.AddComponent<AudioSource>();
                    }

                    if (src != null)
                    {
                        src.spatialBlend = 0f;
                        src.playOnAwake = false;

                        if (mixer != null)
                        {
                            string groupName = ch.type == WordPuzzle.Audio.AudioType.SFX ? "SFX" : (ch.type == WordPuzzle.Audio.AudioType.UI ? "UI" : "Master");
                            AudioMixerGroup[] groups = mixer.FindMatchingGroups(groupName);
                            if (groups != null && groups.Length > 0)
                            {
                                src.outputAudioMixerGroup = groups[0];
                            }
                        }

                        listProp.arraySize++;
                        SerializedProperty entry = listProp.GetArrayElementAtIndex(listProp.arraySize - 1);
                        entry.FindPropertyRelative("type").enumValueIndex = (int)ch.type;
                        entry.FindPropertyRelative("source").objectReferenceValue = src;
                    }
                }
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(audioManager);
            }
        }

        private static void EnsureDirectories()
        {
            string baseFolder = "Assets/WordPuzzle";
            EnsureFolder($"{baseFolder}/Prefabs");
            EnsureFolder($"{baseFolder}/Data/SO");
            EnsureFolder($"{baseFolder}/Data/SO/Levels");
            EnsureFolder($"{baseFolder}/Resources");
            EnsureFolder($"{baseFolder}/Sounds");
            EnsureFolder($"{baseFolder}/Sprites");
            EnsureFolder($"{baseFolder}/Audio Mixers");
            EnsureFolder($"{baseFolder}/UI/Layouts");
        }

        private static void EnsureFolder(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
            }
        }

        private static ViewConfig CreateOrGetViewConfig(string assetName, string viewId, string scriptType, PanelSettings panelSettings, bool hidePrevious, string uxmlPath = null)
        {
            string path = $"Assets/WordPuzzle/Data/SO/{assetName}.asset";
            ViewConfig config = AssetDatabase.LoadAssetAtPath<ViewConfig>(path);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<ViewConfig>();
                AssetDatabase.CreateAsset(config, path);
            }

            config.viewId = viewId;
            config.screenScriptTypeName = scriptType;
            config.panelSettings = panelSettings;
            config.shouldHidePreviousUI = hidePrevious;

            if (!string.IsNullOrEmpty(uxmlPath))
            {
                VisualTreeAsset vta = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
                if (vta != null)
                {
                    config.visualTreeAsset = vta;
                }
            }

            EditorUtility.SetDirty(config);
            return config;
        }

        /// <summary>
        /// Returns the level database, seeding it with the sample levels only when it is new or
        /// empty. A database already filled by the Level Generator is left untouched, so running
        /// scene setup never wipes a generated campaign.
        /// </summary>
        private static LevelDatabase GetOrCreateLevelDatabase(List<LevelData> sampleLevels)
        {
            const string path = "Assets/WordPuzzle/Data/SO/LevelDatabase.asset";

            LevelDatabase db = AssetDatabase.LoadAssetAtPath<LevelDatabase>(path);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<LevelDatabase>();
                AssetDatabase.CreateAsset(db, path);
            }

            if (db.Count == 0)
            {
                db.EditorSetLevels(new List<LevelData>(sampleLevels));
                EditorUtility.SetDirty(db);
            }

            return db;
        }

        private static List<LevelData> CreateSampleLevelAssets()
        {
            List<LevelData> result = new List<LevelData>();

            // Crossword rule: a word must either share its intersecting cell with a crossing word
            // or be separated by a blank. A word starting directly under an existing letter merges
            // with it into one run - e.g. CAT at (1,0) beneath CATS' C read as "CCAT".
            result.Add(CreateLevel("Level_0001", 1, "Chapter 1 - Green Meadow", "CATS", new[]
            {
                ("CATS", 0, 0, WordOrientation.Horizontal),
                ("CAT", 0, 0, WordOrientation.Vertical),
                ("SAT", 0, 3, WordOrientation.Vertical)
            }));

            result.Add(CreateLevel("Level_0002", 2, "Chapter 1 - Starlight Peak", "STAR", new[]
            {
                ("STAR", 0, 0, WordOrientation.Horizontal),
                ("SAT", 0, 0, WordOrientation.Vertical),
                ("ART", 0, 2, WordOrientation.Vertical)
            }));

            result.Add(CreateLevel("Level_0003", 3, "Chapter 1 - Whispering Woods", "BIRD", new[]
            {
                ("BIRD", 0, 0, WordOrientation.Horizontal),
                ("RIB", 0, 2, WordOrientation.Vertical),
                ("BID", 0, 0, WordOrientation.Vertical)
            }));

            return result;
        }

        private static LevelData CreateLevel(string levelName, int levelNum, string chapter, string letters, (string word, int r, int c, WordOrientation ori)[] targets)
        {
            LevelData data = new LevelData();
            data.levelName = levelName;
            data.levelNumber = levelNum;
            data.chapterTitle = chapter;
            data.wheelLetters = letters;
            data.targetWords = new List<TargetWordEntry>();

            foreach (var t in targets)
            {
                data.targetWords.Add(new TargetWordEntry
                {
                    word = t.word,
                    startRow = t.r,
                    startCol = t.c,
                    orientation = t.ori
                });
            }
            return data;
        }

        /// <summary>
        /// Fills a serialized FactoryConfig&lt;T&gt; on the controller.
        /// startImmediately is left off deliberately: the framework's prewarm path calls
        /// AddObject() which fills the single availableObjects queue, while Create(int) reads
        /// availableObjectsDictionary - so prewarmed instances would never be handed out.
        /// The pool grows on demand instead.
        /// </summary>
        private static void ConfigureFactory(SerializedObject controllerSO, string fieldName, string factoryName, params Component[] prefabs)
        {
            SerializedProperty config = controllerSO.FindProperty(fieldName);
            if (config == null || prefabs == null || prefabs.Length == 0)
            {
                Debug.LogWarning($"[WordPuzzleSetup] Could not configure factory '{fieldName}' (prefab missing or field renamed).");
                return;
            }

            // The list must hold every index the game asks for. It was hardcoded to 1, so any
            // FXType above 0 fell into the out-of-bounds path in FactoryFuncMapping and
            // silently played nothing.
            SerializedProperty prefabList = config.FindPropertyRelative("prefab");
            prefabList.arraySize = prefabs.Length;
            for (int i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] == null)
                {
                    Debug.LogWarning($"[WordPuzzleSetup] Factory '{fieldName}' has no prefab for index {i}.");
                }
                prefabList.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
            }

            config.FindPropertyRelative("name").stringValue = factoryName;
            config.FindPropertyRelative("startImmediately").boolValue = false;
            config.FindPropertyRelative("numberOfObjectsToCreate").intValue = 0;
            config.FindPropertyRelative("delayBetweenInstances").floatValue = 0f;
        }

        private static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            if (target == null) return null;
            T comp = target.GetComponent<T>();
            if (comp == null)
            {
                comp = target.AddComponent<T>();
            }
            return comp;
        }
    }
}
#endif
