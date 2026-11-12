using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Blackout.Experience;
using Blackout.Experience.Editor;

namespace Blackout.World.Editor
{
    /// <summary>
    /// Builds the BLACKOUT night-market city from the Kenney CC0 kits: roads, districts, a lamp-lit market,
    /// emitter points for audio, the delivery tower, and the power-cut lighting. Menu: Blackout > World > Build City Scene.
    /// Everything is static-batched and uses a handful of shared materials so it runs on Quest 2.
    /// </summary>
    public static class CityBuilder
    {
        public const string ScenePath = "Assets/_World/Scenes/Blackout_City.unity";

        const string Kenney = "Assets/_World/ThirdParty/Kenney/";
        const string Roads = Kenney + "city-kit-roads/Models/FBX format/";
        const string Comm = Kenney + "city-kit-commercial_2.1/Models/FBX format/";
        const string Ind = Kenney + "city-kit-industrial/Models/FBX format/";
        const string Furn = Kenney + "furniture-kit/Models/FBX format/";
        const string MatDir = "Assets/_World/Materials/";

        // Layout
        internal const float Tile = 12f;          // metres per tile
        internal const int Blocks = 7;            // blocks per side
        internal const int Period = 4;            // tiles per block incl. one road line
        internal const int Size = Blocks * Period + 1;   // 29 x 29 tiles, about 348 m square
        internal const int Half = Size / 2;

        // Model orientation assumptions (verified visually and adjustable here)
        const bool RoadStraightRunsAlongX = true;
        const float BuildingFrontYawOffset = 0f;

        static readonly Dictionary<string, Material> MatCache = new Dictionary<string, Material>();
        static readonly Dictionary<string, Bounds> BoundsCache = new Dictionary<string, Bounds>();
        static int _matCounter;
        static System.Random _rng;

        static Material _lampMat, _ringMat, _echoMat, _relayMat, _postMat;
        static AudioSource _towerAudio;
        static Material[] _signMats;
        static readonly Color[] SignColors =
        {
            new Color(1f, 0.25f, 0.2f), new Color(0.2f, 0.9f, 0.5f), new Color(0.25f, 0.6f, 1f),
            new Color(1f, 0.8f, 0.2f), new Color(0.9f, 0.3f, 0.9f)
        };

        static readonly List<Transform> LampTransforms = new List<Transform>();
        static Transform _roadsRoot, _buildingsRoot, _propsRoot, _marketRoot, _lampsRoot, _audioRoot, _landmarkRoot;

        [MenuItem("Blackout/World/Build City Scene")]
        public static void Build()
        {
            var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ExperienceSceneBuilder.RigPath);
            if (rigPrefab == null) { Debug.LogError("[Blackout] XR rig prefab missing."); return; }

            _rng = new System.Random(42);
            MatCache.Clear(); BoundsCache.Clear(); LampTransforms.Clear(); _matCounter = 0;
            Directory.CreateDirectory(MatDir);
            Directory.CreateDirectory("Assets/_World/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Rig and UI
            new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<XRUIInputModule>();
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
            rig.name = "XR Origin (XR Rig)";
            var cam = rig.GetComponentInChildren<Camera>(true);
            var fogColor = new Color(0.012f, 0.016f, 0.032f);
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = fogColor;
                cam.farClipPlane = 260f;
                cam.nearClipPlane = 0.05f;
            }
            new GameObject("Experience").AddComponent<ExperienceBootstrap>();

            // Roots
            var root = new GameObject("City").transform;
            _roadsRoot = NewRoot("Roads", root);
            _buildingsRoot = NewRoot("Buildings", root);
            _propsRoot = NewRoot("StreetProps", root);
            _marketRoot = NewRoot("NightMarket", root);
            _lampsRoot = NewRoot("Lamps", root);
            _landmarkRoot = NewRoot("Landmarks", root);
            _audioRoot = NewRoot("AudioEmitters", root);

            MakeSharedMaterials();
            BuildGround(root);

            float roadScale = FitScale(Roads + "road-straight.fbx", Tile);
            float comScale = FitScale(Comm + "building-a.fbx", Tile * 0.92f);

            BuildRoadsAndBlocks(roadScale, comScale);
            BuildLamps(roadScale);
            BuildMarket(comScale);
            BuildLandmarks();
            BuildAudioEmitters();
            BuildBoundary(root);
            BuildLighting(root);

            // Spawn
            var spawn = new GameObject("PlayerSpawn").transform;
            spawn.position = CellCenter(Period * 3, Half + 12);   // on the long north-south avenue
            spawn.rotation = Quaternion.Euler(0f, 180f, 0f);   // face north, toward the tower
            rig.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            BuildGameplay(spawn);
            MonsterBuilder.Build();
            QuestBuilder.Build();

            foreach (var t in root.GetComponentsInChildren<Transform>())
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            ExperienceSceneBuilder.AddToBuildSettings(ScenePath);
            Debug.Log($"[Blackout] City built: {root.GetComponentsInChildren<MeshRenderer>().Length} renderers, {LampTransforms.Count} lamps. Saved {ScenePath}");
        }

        // ---------------------------------------------------------------- layout

        internal static Vector3 CellCenter(int i, int j) => new Vector3((i - Half) * Tile, 0f, (j - Half) * Tile);
        static bool IsRoadLineX(int j) => j % Period == 0;   // road running along X at this row
        static bool IsRoadLineZ(int i) => i % Period == 0;   // road running along Z at this column

        static void BuildRoadsAndBlocks(float roadScale, float comScale)
        {
            var cross = Load(Roads + "road-crossroad.fbx");
            var straight = Load(Roads + "road-straight.fbx");
            var skyscrapers = new[] { "building-skyscraper-a", "building-skyscraper-b", "building-skyscraper-c", "building-skyscraper-d", "building-skyscraper-e" }.Select(n => Load(Comm + n + ".fbx")).ToArray();
            var commercial = "abcdefghijklmn".Select(c => Load(Comm + "building-" + c + ".fbx")).ToArray();
            var industrial = "abcdefghijklmnopqrst".Select(c => Load(Ind + "building-" + c + ".fbx")).ToArray();

            for (int i = 0; i < Size; i++)
            for (int j = 0; j < Size; j++)
            {
                bool rx = IsRoadLineX(j), rz = IsRoadLineZ(i);
                var c = CellCenter(i, j);
                if (rx && rz)
                {
                    PlaceRoad(cross, c, 0f, roadScale);
                }
                else if (rx)
                {
                    PlaceRoad(straight, c, RoadStraightRunsAlongX ? 0f : 90f, roadScale);
                }
                else if (rz)
                {
                    PlaceRoad(straight, c, RoadStraightRunsAlongX ? 90f : 0f, roadScale);
                }
                else
                {
                    int bx = i / Period, bz = j / Period;
                    int a = i % Period - 1, b = j % Period - 1;   // 0..2 inside the block
                    PlaceBlockCell(bx, bz, a, b, c, skyscrapers, commercial, industrial, comScale);
                }
            }
        }

        static void PlaceRoad(GameObject prefab, Vector3 c, float yaw, float scale)
        {
            var go = Place(prefab, _roadsRoot, c, yaw, scale, false);
            if (go.GetComponentInChildren<MeshFilter>() != null)
            {
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                    mf.gameObject.AddComponent<MeshCollider>();
            }
        }

        internal static bool IsMarketBlock(int bx, int bz)
        {
            int dx = Mathf.Abs(bx - Blocks / 2), dz = Mathf.Abs(bz - Blocks / 2);
            return (dx == 0 && dz == 0) || (dx + dz == 1);
        }

        static void PlaceBlockCell(int bx, int bz, int a, int b, Vector3 c, GameObject[] sky, GameObject[] com, GameObject[] ind, float comScale)
        {
            if (IsMarketBlock(bx, bz)) return;                          // open ground, filled by BuildMarket
            if (bx == Blocks / 2 && bz == 0 && a == 1 && b == 1) return; // tower plaza

            int dist = Mathf.Max(Mathf.Abs(bx - Blocks / 2), Mathf.Abs(bz - Blocks / 2));
            bool industrialZone = bx >= Blocks - 2 && bz >= 1;
            GameObject prefab;
            if (industrialZone) prefab = ind[_rng.Next(ind.Length)];
            else if (dist <= 1 && _rng.NextDouble() < 0.65) prefab = sky[_rng.Next(sky.Length)];
            else prefab = com[_rng.Next(com.Length)];

            // face the nearest road
            Vector3 face;
            if (a == 0 && (b != 2 || _rng.Next(2) == 0)) face = Vector3.left;
            else if (a == 2) face = Vector3.right;
            else if (b == 0) face = Vector3.back;
            else if (b == 2) face = Vector3.forward;
            else face = new Vector3[] { Vector3.left, Vector3.right, Vector3.forward, Vector3.back }[_rng.Next(4)];
            float yaw = Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg + BuildingFrontYawOffset;

            float fit = FitScale(AssetDatabase.GetAssetPath(prefab), Tile * 0.92f);
            var go = Place(prefab, _buildingsRoot, c, yaw, fit, true);

            // glowing sign on the street face of some shops
            if (!industrialZone && _rng.NextDouble() < 0.3 && (a != 1 || b != 1))
            {
                int k = _rng.Next(_signMats.Length);
                var sign = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(sign.GetComponent<Collider>());
                sign.name = "NeonSign";
                sign.transform.SetParent(_buildingsRoot, false);
                sign.transform.localScale = new Vector3(4.5f, 1.1f, 0.2f);
                sign.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                sign.transform.position = c + face * (Tile * 0.46f + 0.15f) + Vector3.up * 3.6f;
                sign.GetComponent<MeshRenderer>().sharedMaterial = _signMats[k];
                NoShadow(sign.GetComponent<MeshRenderer>());
            }
        }

        // ---------------------------------------------------------------- lamps and props

        static void BuildLamps(float roadScale)
        {
            var lamp = Load(Roads + "light-square.fbx");
            var cone = Load(Roads + "construction-cone.fbx");
            var dumpster = Load(Roads + "dumpster.fbx");
            var barrier = Load(Roads + "construction-barrier.fbx");
            var pole = Load(Roads + "electricity-pole.fbx");
            int n = 0;
            for (int i = 0; i < Size; i++)
            for (int j = 0; j < Size; j++)
            {
                bool rx = IsRoadLineX(j), rz = IsRoadLineZ(i);
                if (rx == rz) continue;               // intersections and blocks skipped
                n++;
                var c = CellCenter(i, j);
                Vector3 side = rx ? Vector3.forward : Vector3.right;
                if (n % 2 == 0) side = -side;
                Vector3 toRoad = -side;

                if (n % 3 == 0)
                {
                    var l = Place(lamp, _lampsRoot, c + side * (Tile * 0.42f), Mathf.Atan2(toRoad.x, toRoad.z) * Mathf.Rad2Deg, roadScale, false);
                    var b = WorldBounds(l);
                    AddBulb(_lampsRoot, new Vector3(b.center.x, b.max.y - 0.35f, b.center.z), 0.7f, true);
                }
                else if (n % 7 == 0)
                {
                    Place(n % 2 == 0 ? dumpster : barrier, _propsRoot, c + side * (Tile * 0.36f), (float)_rng.Next(4) * 90f, roadScale, true);
                }
                else if (n % 11 == 0)
                {
                    Place(cone, _propsRoot, c + side * (Tile * 0.2f), (float)_rng.Next(360), roadScale, false);
                }
                else if (n % 13 == 0)
                {
                    Place(pole, _propsRoot, c + side * (Tile * 0.44f), Mathf.Atan2(toRoad.x, toRoad.z) * Mathf.Rad2Deg, roadScale, true);
                }
            }
        }

        static void AddBulb(Transform parent, Vector3 pos, float size, bool asLamp)
        {
            var bulb = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(bulb.GetComponent<Collider>());
            bulb.name = "Bulb";
            bulb.transform.SetParent(parent, false);
            bulb.transform.position = pos;
            bulb.transform.localScale = Vector3.one * size;
            var r = bulb.GetComponent<MeshRenderer>();
            r.sharedMaterial = _lampMat;
            NoShadow(r);
            if (asLamp) LampTransforms.Add(bulb.transform);
        }

        // ---------------------------------------------------------------- night market

        static void BuildMarket(float comScale)
        {
            var table = Load(Furn + "tableCloth.fbx");
            var crateA = Load(Furn + "cardboardBoxClosed.fbx");
            var crateB = Load(Furn + "cardboardBoxOpen.fbx");
            var plant = Load(Furn + "pottedPlant.fbx");
            var stool = Load(Furn + "stoolBar.fbx");
            var parasolA = Load(Comm + "detail-parasol-a.fbx");
            var parasolB = Load(Comm + "detail-parasol-b.fbx");
            var awning = Load(Comm + "detail-awning-wide.fbx");

            float f = FitScale(Furn + "tableCloth.fbx", 2.6f);   // one furniture scale for everything
            int centre = Blocks / 2;

            for (int bx = 0; bx < Blocks; bx++)
            for (int bz = 0; bz < Blocks; bz++)
            {
                if (!IsMarketBlock(bx, bz)) continue;
                var origin = CellCenter(bx * Period + 2, bz * Period + 2);   // block centre
                // three rows of stalls with aisles between
                for (int row = -1; row <= 1; row++)
                for (int col = -2; col <= 2; col++)
                {
                    var p = origin + new Vector3(col * 7f, 0f, row * 10f);
                    float yaw = row == 0 ? (col % 2 == 0 ? 0f : 180f) : (row < 0 ? 0f : 180f);
                    PlaceStall(p, yaw, f, comScale, table, crateA, crateB, plant, stool, parasolA, parasolB, awning);
                }
                // string lights across the aisles
                for (int row = -1; row <= 1; row += 2)
                for (int col = -15; col <= 15; col += 3)
                    AddBulb(_marketRoot, origin + new Vector3(col, 4.2f, row * 5f), 0.35f, col % 6 == 0);
            }
        }

        static void PlaceStall(Vector3 p, float yaw, float f, float comScale, GameObject table, GameObject crateA, GameObject crateB,
            GameObject plant, GameObject stool, GameObject parasolA, GameObject parasolB, GameObject awning)
        {
            Place(table, _marketRoot, p, yaw, f, true);
            Place(_rng.Next(2) == 0 ? crateA : crateB, _marketRoot, p + new Vector3(1.9f, 0f, 0.2f), (float)_rng.Next(360), f * 0.9f, false);
            if (_rng.NextDouble() < 0.5) Place(crateA, _marketRoot, p + new Vector3(-1.9f, 0f, -0.2f), (float)_rng.Next(360), f * 0.8f, false);
            if (_rng.NextDouble() < 0.35) Place(plant, _marketRoot, p + new Vector3(0.0f, 0f, 1.6f), 0f, f, false);
            if (_rng.NextDouble() < 0.4) Place(stool, _marketRoot, p + new Vector3(0.8f, 0f, 1.7f), 0f, f, false);
            var cover = _rng.NextDouble() < 0.6 ? (_rng.Next(2) == 0 ? parasolA : parasolB) : awning;
            var covered = Place(cover, _marketRoot, p + Vector3.up * 0f, yaw, comScale * 0.35f, false);
            var cb = WorldBounds(covered);
            covered.transform.position += Vector3.up * (3.0f - cb.min.y);
            AddBulb(_marketRoot, p + new Vector3(0f, 1.45f, 0f), 0.3f, false);
        }

        // ---------------------------------------------------------------- landmarks and audio

        static void BuildLandmarks()
        {
            var chimney = Load(Ind + "chimney-large.fbx");
            var centre = CellCenter(Half, 2);
            float s = FitHeight(Ind + "chimney-large.fbx", 70f);
            var tower = Place(chimney, _landmarkRoot, centre, 0f, s, true);
            tower.name = "DeliveryTower";
            var b = WorldBounds(tower);

            var beacon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(beacon.GetComponent<Collider>());
            beacon.name = "TowerBeacon";
            beacon.transform.SetParent(_landmarkRoot, false);
            beacon.transform.position = new Vector3(centre.x, b.max.y + 1.5f, centre.z);
            beacon.transform.localScale = Vector3.one * 2.4f;
            beacon.GetComponent<MeshRenderer>().sharedMaterial = _signMats[0];
            NoShadow(beacon.GetComponent<MeshRenderer>());

            var target = new GameObject("DeliveryTarget");
            target.transform.SetParent(_landmarkRoot, false);
            target.transform.position = centre + new Vector3(0f, 0f, Tile * 0.6f);
            var trigger = target.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 6f;
        }

        static void BuildAudioEmitters()
        {
            AddEmitter("Generator", 14, 3f, 40f);
            AddEmitter("MarketChatter", 5, 4f, 55f);
            AddEmitter("DistantTraffic", 4, 6f, 90f);
            var tgt = new GameObject("DeliveryBeaconAudio");
            tgt.transform.SetParent(_audioRoot, false);
            tgt.transform.position = CellCenter(Half, 2) + new Vector3(0f, 2f, Tile * 0.6f);
            _towerAudio = tgt.AddComponent<AudioSource>();
            ConfigEmitter(_towerAudio, 4f, 120f);
            _towerAudio.clip = Clip("tower_signal");
            _towerAudio.playOnAwake = false;   // SonarGameplay starts it once all relays are on
        }

        static void AddEmitter(string name, int count, float minD, float maxD)
        {
            for (int k = 0; k < count; k++)
            {
                var go = new GameObject($"{name}_{k + 1:00}");
                go.transform.SetParent(_audioRoot, false);
                Vector3 pos;
                if (name == "MarketChatter")
                {
                    int[,] blocks = { { 3, 3 }, { 2, 3 }, { 4, 3 }, { 3, 2 }, { 3, 4 } };
                    pos = CellCenter(blocks[k % 5, 0] * Period + 2, blocks[k % 5, 1] * Period + 2);
                }
                else
                {
                    int i = _rng.Next(1, Blocks) * Period, j = _rng.Next(1, Blocks) * Period;
                    pos = CellCenter(i, j) + new Vector3(_rng.Next(-4, 5), 0f, _rng.Next(-4, 5));
                }
                go.transform.position = pos + Vector3.up * 1.2f;
                var src = go.AddComponent<AudioSource>();
                ConfigEmitter(src, minD, maxD);
                AssignAmbient(src, name, k);
            }
        }

        static void ConfigEmitter(AudioSource src, float minD, float maxD)
        {
            src.playOnAwake = false;     // Track 3 assigns clips and enables Play On Awake / loop
            src.loop = true;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = minD;
            src.maxDistance = maxD;
        }

        static AudioClip Clip(string name)
        {
            var c = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_World/Audio/" + name + ".wav");
            if (c == null) Debug.LogWarning("[Blackout] Missing audio clip: " + name);
            return c;
        }

        static void AssignAmbient(AudioSource src, string kind, int index)
        {
            string clip = kind == "Generator" ? "generator_loop" : kind == "MarketChatter" ? "market_murmur" : "distant_traffic";
            src.clip = Clip(clip);
            src.loop = true;
            src.playOnAwake = true;
            src.dopplerLevel = 0f;
            src.volume = kind == "Generator" ? 0.7f : kind == "MarketChatter" ? 0.9f : 0.5f;
            src.pitch = 0.94f + (index % 5) * 0.03f;   // avoids identical emitters phasing together
        }

        static Material NewSprite(string name, Color c)
        {
            Directory.CreateDirectory("Assets/_Sonar/Materials");
            var m = new Material(Shader.Find("Sprites/Default")) { name = name, color = c };
            AssetDatabase.CreateAsset(m, AssetDatabase.GenerateUniqueAssetPath("Assets/_Sonar/Materials/" + name + ".mat"));
            return m;
        }

        // ---------------------------------------------------------------- gameplay

        static void BuildGameplay(Transform spawn)
        {
            _ringMat = NewSprite("SonarRing", new Color(0.35f, 0.95f, 1f, 1f));
            _echoMat = NewSprite("SonarEcho", Color.white);
            var dotShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Sonar/Shaders/EchoDot.shader");
            if (dotShader != null)
            {
                _echoMat.shader = dotShader;
                _echoMat.SetColor("_Tint", Color.white);
                _echoMat.renderQueue = 3000;
                EditorUtility.SetDirty(_echoMat);
            }
            else Debug.LogWarning("[Blackout] EchoDot shader not found.");
            _relayMat = NewLit("RelayGlow", new Color(0.1f, 0.1f, 0.12f));
            _relayMat.EnableKeyword("_EMISSION");
            _relayMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            _relayMat.SetColor("_EmissionColor", Color.black);
            _postMat = NewLit("RelayPost", new Color(0.18f, 0.18f, 0.2f));

            var root = new GameObject("Gameplay").transform;   // not static: relays animate their glow
            Vector3[] pos =
            {
                CellCenter(Period * 3, 20),                                   // first relay on the avenue, south of the market
                CellCenter(Half, Half) + new Vector3(3.5f, 0f, 5f),           // second relay in the middle of the night market
                CellCenter(Half, 4)                                           // third relay on the street south of the tower
            };
            Color[] cols = { new Color(0.35f, 0.95f, 1f), new Color(0.45f, 1f, 0.55f), new Color(1f, 0.75f, 0.3f) };
            var relays = new RelayBeacon[3];
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject("Relay_" + (i + 1));
                go.transform.SetParent(root, false);
                go.transform.position = pos[i];

                var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                post.name = "Post";
                post.transform.SetParent(go.transform, false);
                post.transform.localPosition = new Vector3(0f, 1.75f, 0f);
                post.transform.localScale = new Vector3(0.5f, 3.5f, 0.5f);
                post.GetComponent<MeshRenderer>().sharedMaterial = _postMat;
                NoShadow(post.GetComponent<MeshRenderer>());

                var lamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lamp.name = "Lamp";
                lamp.transform.SetParent(go.transform, false);
                lamp.transform.localPosition = new Vector3(0f, 4.0f, 0f);
                lamp.transform.localScale = new Vector3(1.1f, 0.8f, 1.1f);
                lamp.GetComponent<MeshRenderer>().sharedMaterial = _relayMat;
                NoShadow(lamp.GetComponent<MeshRenderer>());

                var src = go.AddComponent<AudioSource>();
                ConfigEmitter(src, 2f, 75f);
                src.clip = Clip("relay_tone_" + (i + 1));
                src.playOnAwake = false;
                src.dopplerLevel = 0f;

                var rb = go.AddComponent<RelayBeacon>();
                rb.tone = src;
                rb.glow = lamp.GetComponent<MeshRenderer>();
                rb.color = cols[i];
                relays[i] = rb;
            }

            var sonarGo = new GameObject("Sonar");
            sonarGo.transform.SetParent(root, false);
            var ping = sonarGo.AddComponent<SonarPing>();
            ping.ringMaterial = _ringMat;
            ping.echoMaterial = _echoMat;
            ping.pingClip = Clip("sonar_ping");

            var gp = sonarGo.AddComponent<SonarGameplay>();
            gp.spawn = spawn;
            gp.relays = relays;
            var target = GameObject.Find("DeliveryTarget");
            gp.tower = target != null ? target.transform : null;
            gp.towerAudio = _towerAudio;
            gp.sonar = ping;
            gp.relayOnClip = Clip("relay_on");
        }

        // ---------------------------------------------------------------- ground, boundary, lighting

        static void BuildGround(Transform root)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.SetParent(root, false);
            float side = Size * Tile + 80f;
            ground.transform.localScale = new Vector3(side, 1f, side);
            ground.transform.position = new Vector3(0f, -0.5f - 0.03f, 0f);
            var m = NewLit("Ground", new Color(0.09f, 0.09f, 0.1f));
            ground.GetComponent<MeshRenderer>().sharedMaterial = m;
            NoShadow(ground.GetComponent<MeshRenderer>());
        }

        static void BuildBoundary(Transform root)
        {
            float edge = (Half + 1.5f) * Tile;
            var wall = new GameObject("Boundary").transform;
            wall.SetParent(root, false);
            Vector3[] pos = { new Vector3(edge, 5f, 0f), new Vector3(-edge, 5f, 0f), new Vector3(0f, 5f, edge), new Vector3(0f, 5f, -edge) };
            Vector3[] scl = { new Vector3(1f, 12f, edge * 2f), new Vector3(1f, 12f, edge * 2f), new Vector3(edge * 2f, 12f, 1f), new Vector3(edge * 2f, 12f, 1f) };
            for (int k = 0; k < 4; k++)
            {
                var w = new GameObject("Wall" + k);
                w.transform.SetParent(wall, false);
                w.transform.position = pos[k];
                var bc = w.AddComponent<BoxCollider>();
                bc.size = scl[k];
            }
        }

        static void BuildLighting(Transform root)
        {
            var moonGo = new GameObject("Moon");
            moonGo.transform.SetParent(root, false);
            moonGo.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
            var moon = moonGo.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.55f, 0.65f, 1f);
            moon.intensity = 0.02f;
            moon.shadows = LightShadows.None;

            var cityLight = new GameObject("CityLighting");
            cityLight.transform.SetParent(root, false);
            var pool = cityLight.AddComponent<LampLightPool>();
            pool.lamps = LampTransforms.ToArray();
            var power = cityLight.AddComponent<CityPower>();
            power.moon = moon;
            power.lampPool = pool;
            power.lampMaterial = _lampMat;
            power.signMaterials = _signMats;
            power.signColors = SignColors;

            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.003f, 0.004f, 0.008f);
            RenderSettings.fogDensity = 0.03f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.004f, 0.005f, 0.012f);
            RenderSettings.sun = moon;
        }

        // ---------------------------------------------------------------- helpers

        static Transform NewRoot(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        internal static GameObject Load(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) throw new FileNotFoundException("Missing model: " + path);
            return go;
        }

        static Bounds WorldBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        static Bounds NaturalBounds(string path)
        {
            if (BoundsCache.TryGetValue(path, out var b)) return b;
            var go = Object.Instantiate(Load(path));
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            b = WorldBounds(go);
            Object.DestroyImmediate(go);
            BoundsCache[path] = b;
            return b;
        }

        static float FitScale(string path, float targetFootprint)
        {
            var s = NaturalBounds(path).size;
            return targetFootprint / Mathf.Max(0.0001f, Mathf.Max(s.x, s.z));
        }

        static float FitHeight(string path, float targetHeight)
        {
            return targetHeight / Mathf.Max(0.0001f, NaturalBounds(path).size.y);
        }

        static GameObject Place(GameObject prefab, Transform parent, Vector3 center, float yaw, float scale, bool collider)
        {
            var go = Object.Instantiate(prefab, parent);
            go.name = prefab.name;
            go.transform.localScale = Vector3.one * scale;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.position = Vector3.zero;
            var b = WorldBounds(go);
            go.transform.position += new Vector3(center.x - b.center.x, center.y - b.min.y, center.z - b.center.z);
            Reskin(go);
            if (collider) AddBox(go);
            return go;
        }

        static void AddBox(GameObject go)
        {
            var b = WorldBounds(go);
            var bc = go.AddComponent<BoxCollider>();
            bc.center = go.transform.InverseTransformPoint(b.center);
            var s = go.transform.InverseTransformVector(b.size);
            bc.size = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        }

        static void NoShadow(Renderer r)
        {
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        static void Reskin(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = MapMaterial(mats[i]);
                r.sharedMaterials = mats;
                NoShadow(r);
            }
        }

        static Material MapMaterial(Material src)
        {
            string key = src == null ? "null" : AssetDatabase.GetAssetPath(src) + "|" + src.name;
            if (MatCache.TryGetValue(key, out var cached)) return cached;

            Color color = Color.white;
            Texture tex = null;
            if (src != null)
            {
                if (src.HasProperty("_BaseColor")) color = src.GetColor("_BaseColor");
                else if (src.HasProperty("_Color")) color = src.GetColor("_Color");
                if (src.HasProperty("_BaseMap")) tex = src.GetTexture("_BaseMap");
                if (tex == null && src.HasProperty("_MainTex")) tex = src.GetTexture("_MainTex");
            }
            var m = NewLit("auto_" + (++_matCounter) + "_" + (src != null ? Sanitize(src.name) : "none"), color);
            if (tex != null) m.SetTexture("_BaseMap", tex);
            MatCache[key] = m;
            return m;
        }

        static string Sanitize(string s) => new string(s.Where(char.IsLetterOrDigit).ToArray());

        static Material NewLit(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(shader) { name = name };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", 0f);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_SpecularHighlights", 0f);
            m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            var path = MatDir + name + ".mat";
            AssetDatabase.CreateAsset(m, AssetDatabase.GenerateUniqueAssetPath(path));
            return m;
        }

        static void MakeSharedMaterials()
        {
            _lampMat = NewLit("LampBulb", new Color(1f, 0.85f, 0.6f));
            _lampMat.EnableKeyword("_EMISSION");
            _lampMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            _lampMat.SetColor("_EmissionColor", Color.black);

            _signMats = new Material[SignColors.Length];
            for (int i = 0; i < SignColors.Length; i++)
            {
                _signMats[i] = NewLit("Neon" + i, SignColors[i] * 0.6f);
                _signMats[i].EnableKeyword("_EMISSION");
                _signMats[i].globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                _signMats[i].SetColor("_EmissionColor", Color.black);
            }
        }
    }
}
