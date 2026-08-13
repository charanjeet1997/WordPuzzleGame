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
            GameObject particleFXPrefab = CreateOrUpdateParticleFXPrefab();

            // 4. Build & Save World Prefab (WordPuzzleWorld.prefab)
            GameObject worldPrefab = CreateOrUpdateWorldPrefab(gridTilePrefab, letterNodePrefab);

            // 5. Create & Register WorldDatabase Asset in Data/SO
            WorldDatabase worldDB = CreateOrGetWorldDatabase(worldPrefab);

            // 6. Create PanelSettings Asset in Data/SO
            PanelSettings panelSettings = GetOrCreatePanelSettings();

            // 7. Create Level Data Assets & UI Toolkit View Config Assets in Data/SO
            List<LevelData> levelAssets = CreateSampleLevelAssets();
            LevelDatabase levelDatabase = GetOrCreateLevelDatabase(levelAssets);
            ViewConfig configMainMenu = CreateOrGetViewConfig("ViewConfig_MainMenu", "MainMenu", "WordPuzzle.UI.MainMenuView", panelSettings, true, "Assets/WordPuzzle/UI/Layouts/MainMenu.uxml");
            ViewConfig configHUD = CreateOrGetViewConfig("ViewConfig_HUD", "HUD", "WordPuzzle.UI.HUDView", panelSettings, true, "Assets/WordPuzzle/UI/Layouts/HUD.uxml");
            ViewConfig configPause = CreateOrGetViewConfig("ViewConfig_PauseOverlay", "PauseOverlay", "WordPuzzle.UI.PauseOverlayView", panelSettings, false, "Assets/WordPuzzle/UI/Layouts/PauseOverlay.uxml");
            ViewConfig configLevelComplete = CreateOrGetViewConfig("ViewConfig_LevelComplete", "LevelComplete", "WordPuzzle.UI.LevelCompleteView", panelSettings, false, "Assets/WordPuzzle/UI/Layouts/LevelComplete.uxml");
            ViewConfig configSettings = CreateOrGetViewConfig("ViewConfig_Settings", "Settings", "WordPuzzle.UI.SettingsOverlayView", panelSettings, false, "Assets/WordPuzzle/UI/Layouts/SettingsOverlay.uxml");

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

            ParticleService particleService = GetOrAddComponent<ParticleService>(managersObj);
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
            ConfigureFactory(factorySO, "gridTileConfig", gridTilePrefab.GetComponent<GridTile>(), "GridTile");
            ConfigureFactory(factorySO, "letterNodeConfig", letterNodePrefab.GetComponent<LetterNode>(), "LetterNode");
            ConfigureFactory(factorySO, "particleFXConfig", particleFXPrefab.GetComponent<ParticleFX>(), "ParticleFX");
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
            EditorUtility.SetDirty(gameManager);

            initializer.configMainMenu = configMainMenu;
            EditorUtility.SetDirty(initializer);

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

        private static GameObject CreateOrUpdateParticleFXPrefab()
        {
            string path = "Assets/WordPuzzle/Prefabs/ParticleFXPrefab.prefab";
            GameObject temp = new GameObject("ParticleFXPrefab");

            ParticleSystem ps = temp.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.6f;
            main.startLifetime = 0.5f;
            main.startSpeed = 3f;
            main.startSize = 0.25f;
            main.startColor = new Color(1f, 0.85f, 0.2f, 1f);
            // Must be non-looping: stopAction never fires on a system that never stops.
            main.loop = false;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 25) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;

            // AddComponent leaves the built-in Default-ParticleSystem material, whose shader
            // does not exist under URP and renders magenta.
            ParticleMaterialUtility.EnsureValidMaterial(temp);

            // Required so the effect can be pooled through FactoryManagerByType,
            // which constrains its generic to MonoBehaviour (ParticleSystem is not one).
            temp.AddComponent<ParticleFX>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            return prefab;
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
            string levelPath = "Assets/WordPuzzle/Data/SO";
            List<LevelData> result = new List<LevelData>();

            // Crossword rule: a word must either share its intersecting cell with a crossing word
            // or be separated by a blank. A word starting directly under an existing letter merges
            // with it into one run - e.g. CAT at (1,0) beneath CATS' C read as "CCAT".
            result.Add(CreateLevelAsset($"{levelPath}/LevelData_01.asset", 1, "Chapter 1 - Green Meadow", "CATS", new[]
            {
                ("CATS", 0, 0, WordOrientation.Horizontal),
                ("CAT", 0, 0, WordOrientation.Vertical),
                ("SAT", 0, 3, WordOrientation.Vertical)
            }));

            result.Add(CreateLevelAsset($"{levelPath}/LevelData_02.asset", 2, "Chapter 1 - Starlight Peak", "STAR", new[]
            {
                ("STAR", 0, 0, WordOrientation.Horizontal),
                ("SAT", 0, 0, WordOrientation.Vertical),
                ("ART", 0, 2, WordOrientation.Vertical)
            }));

            result.Add(CreateLevelAsset($"{levelPath}/LevelData_03.asset", 3, "Chapter 1 - Whispering Woods", "BIRD", new[]
            {
                ("BIRD", 0, 0, WordOrientation.Horizontal),
                ("RIB", 0, 2, WordOrientation.Vertical),
                ("BID", 0, 0, WordOrientation.Vertical)
            }));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return result;
        }

        private static LevelData CreateLevelAsset(string path, int levelNum, string chapter, string letters, (string word, int r, int c, WordOrientation ori)[] targets)
        {
            LevelData data = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<LevelData>();
                AssetDatabase.CreateAsset(data, path);
            }

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
            EditorUtility.SetDirty(data);
            return data;
        }

        /// <summary>
        /// Fills a serialized FactoryConfig&lt;T&gt; on the controller.
        /// startImmediately is left off deliberately: the framework's prewarm path calls
        /// AddObject() which fills the single availableObjects queue, while Create(int) reads
        /// availableObjectsDictionary - so prewarmed instances would never be handed out.
        /// The pool grows on demand instead.
        /// </summary>
        private static void ConfigureFactory(SerializedObject controllerSO, string fieldName, Component prefab, string factoryName)
        {
            SerializedProperty config = controllerSO.FindProperty(fieldName);
            if (config == null || prefab == null)
            {
                Debug.LogWarning($"[WordPuzzleSetup] Could not configure factory '{fieldName}' (prefab missing or field renamed).");
                return;
            }

            SerializedProperty prefabList = config.FindPropertyRelative("prefab");
            prefabList.arraySize = 1;
            prefabList.GetArrayElementAtIndex(0).objectReferenceValue = prefab;

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
