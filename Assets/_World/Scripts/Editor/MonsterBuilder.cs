using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace Blackout.World.Editor
{
    /// <summary>
    /// Adds the creatures and townspeople to the city scene using the imported Asset Store packs (Assets/_ThirdParty),
    /// converts their materials to URP, shrinks textures and audio for Quest 2, and wires the MonsterDirector.
    /// </summary>
    public static class MonsterBuilder
    {
        const string Root = "Assets/_ThirdParty/";
        const string OrcFbx = Root + "Monster_Orc/Monster_Orc (Troll)/Meshes/monster_orc.fbx";
        const string OrcPrefab = Root + "Monster_Orc/Monster_Orc (Troll)/Prefabs/monster_orc.prefab";
        const string SkelDir = Root + "Skeleton/Skeleton/";
        const string SkelPrefab = SkelDir + "Prefab/Skeleton.prefab";
        const string PeopleDir = Root + "People_Soldiers/LowPolySoldiers_demo/";
        const string PeoplePrefab = PeopleDir + "Soldier_demo_Prefab.prefab";
        const string AnimDir = "Assets/_World/Animation/";
        const string MatDir = "Assets/_World/Materials/";

        static readonly Vector2Int[] Homes =
        {
            new Vector2Int(3, 3), new Vector2Int(6, 5), new Vector2Int(1, 1),
            new Vector2Int(1, 5), new Vector2Int(6, 1), new Vector2Int(4, 0)
        };

        [MenuItem("Blackout/World/Prepare Monster Assets")]
        public static void PrepareAssets()
        {
            int tex = 0, aud = 0, mats = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root.TrimEnd('/') }))
                    if (ShrinkTexture(AssetDatabase.GUIDToAssetPath(guid))) tex++;
                foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Root.TrimEnd('/') }))
                    if (CompressAudio(AssetDatabase.GUIDToAssetPath(guid))) aud++;
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh();
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { Root.TrimEnd('/') }))
                if (ConvertMaterial(AssetDatabase.GUIDToAssetPath(guid))) mats++;
            AssetDatabase.SaveAssets();
            Debug.Log($"[Blackout] Monster assets prepared: {tex} textures, {aud} audio clips, {mats} materials.");
        }

        static bool ShrinkTexture(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return false;
            var android = imp.GetPlatformTextureSettings("Android");
            if (imp.maxTextureSize <= 512 && android.overridden && android.maxTextureSize <= 512) return false;
            imp.maxTextureSize = 512;
            imp.isReadable = false;
            android.overridden = true;
            android.maxTextureSize = 512;
            android.format = TextureImporterFormat.ASTC_6x6;
            imp.SetPlatformTextureSettings(android);
            imp.SaveAndReimport();
            return true;
        }

        static bool CompressAudio(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as AudioImporter;
            if (imp == null) return false;
            var s = imp.defaultSampleSettings;
            if (imp.forceToMono && s.loadType == AudioClipLoadType.CompressedInMemory && s.compressionFormat == AudioCompressionFormat.Vorbis) return false;
            imp.forceToMono = true;
            s.loadType = AudioClipLoadType.CompressedInMemory;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.45f;
            imp.defaultSampleSettings = s;
            imp.SetOverrideSampleSettings("Android", s);
            imp.SaveAndReimport();
            return true;
        }

        static bool ConvertMaterial(string path)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null || m.shader == null) return false;
            string n = m.shader.name;
            if (n.StartsWith("Universal Render Pipeline")) return false;
            var urp = Shader.Find("Universal Render Pipeline/Lit");
            if (urp == null) return false;

            Texture main = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
            Texture bump = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null;
            Texture emis = m.HasProperty("_EmissionMap") ? m.GetTexture("_EmissionMap") : null;
            Color col = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
            Color emisCol = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
            float gloss = m.HasProperty("_Glossiness") ? m.GetFloat("_Glossiness") : 0.2f;

            m.shader = urp;
            m.SetColor("_BaseColor", col);
            if (main != null) m.SetTexture("_BaseMap", main);
            if (bump != null) { m.SetTexture("_BumpMap", bump); m.EnableKeyword("_NORMALMAP"); }
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", Mathf.Min(gloss, 0.35f));
            if (emis != null || emisCol.maxColorComponent > 0.01f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emisCol);
                if (emis != null) m.SetTexture("_EmissionMap", emis);
            }
            EditorUtility.SetDirty(m);
            return true;
        }

        // ------------------------------------------------------------------ scene

        public static void Build()
        {
            PrepareAssets();
            Directory.CreateDirectory(AnimDir);

            var sonar = UnityEngine.Object.FindFirstObjectByType<SonarPing>();
            var eyeMat = MakeEyeMaterial();

            var orcClips = LoadOrcClips();
            var skelClips = LoadSkeletonClips();
            var orcCtrl = MakeController("Orc", orcClips);
            var skelCtrl = MakeController("Skeleton", skelClips);

            var orcAlert = Clips("Monster_Roars", "Attack Roar", "Warning Growl", "Chilling Roar", "Haunting Growl");
            var orcIdle = Clips("Monster_Roars", "Lurking Growl", "Searching Growl", "Rumbling Growl", "Menacing Growl");
            var orcDeath = Clips("Monster_Roars", "Chilling Roar");
            var zombieAlert = Clips("Zombie_Audio", "_Attack_A_");
            var zombieIdle = Clips("Zombie_Audio", "_Idle_A_");
            var zombieDeath = Clips("Zombie_Audio", "_Hurt_A_00");
            var heavySteps = Clips("Zombie_Audio", "Footsteps_Zombie_concret");
            var lightSteps = Clips("Footsteps", "Floor_step");
            var attack = Clips("Zombie_Audio", "Zombie_Attack_Bite_001");

            var root = new GameObject("Creatures").transform;
            var list = new List<Monster>();
            for (int i = 0; i < Homes.Length; i++)
            {
                bool orc = i % 2 == 0 && i != 4 || i == 5;
                // pattern: orc, skeleton, orc, skeleton, skeleton, orc
                orc = new[] { true, false, true, false, false, true }[i];
                GameObject m;
                if (orc)
                    m = BuildMonster("Orc_" + (i + 1), OrcPrefab, 2.7f, orcCtrl, orcClips.death != null, orcIdle, orcAlert, heavySteps,
                        First(orcDeath), First(attack), eyeMat, root, Homes[i], 0.92f);
                else
                    m = BuildMonster("Skeleton_" + (i + 1), SkelPrefab, 1.95f, skelCtrl, skelClips.death != null, zombieIdle, zombieAlert, lightSteps,
                        First(zombieDeath), First(attack), eyeMat, root, Homes[i], 1f);
                if (m != null) list.Add(m.GetComponent<Monster>());
            }

            var dirGo = new GameObject("MonsterDirector");
            var dir = dirGo.AddComponent<MonsterDirector>();
            dir.monsters = list.ToArray();
            dir.sonar = sonar;
            dir.roadOrigin = new Vector2(CityBuilder.CellCenter(0, 0).x, CityBuilder.CellCenter(0, 0).z);
            dir.roadSpacing = CityBuilder.Period * CityBuilder.Tile;
            dir.roadCount = CityBuilder.Blocks + 1;

            BuildPeople();
            Debug.Log($"[Blackout] Added {list.Count} creatures and townspeople. Triangles per creature: " +
                      string.Join(", ", list.Select(x => x.name + "=" + Triangles(x.gameObject))));
        }

        static int Triangles(GameObject go)
        {
            int t = 0;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                Mesh m = null;
                if (r is SkinnedMeshRenderer s) m = s.sharedMesh;
                else if (r.TryGetComponent<MeshFilter>(out var f)) m = f.sharedMesh;
                if (m != null) t += m.triangles.Length / 3;
            }
            return t;
        }

        static Material MakeEyeMaterial()
        {
            const string path = MatDir + "MonsterEye.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "MonsterEye" };
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", new Color(1f, 0.08f, 0.03f));
            EditorUtility.SetDirty(m);
            return m;
        }

        static AudioClip[] Clips(string folder, params string[] nameParts)
        {
            var found = new List<AudioClip>();
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Root + folder }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                string file = Path.GetFileNameWithoutExtension(p);
                if (nameParts.Any(np => file.IndexOf(np, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    var c = AssetDatabase.LoadAssetAtPath<AudioClip>(p);
                    if (c != null) found.Add(c);
                }
            }
            if (found.Count == 0) Debug.LogWarning("[Blackout] No audio found for " + folder + ": " + string.Join(", ", nameParts));
            return found.ToArray();
        }

        static AudioClip First(AudioClip[] a) => a != null && a.Length > 0 ? a[0] : null;

        // ------------------------------------------------------------------ animation

        struct ClipSet { public AnimationClip idle, walk, run, attack, death; }

        static AnimationClip[] AllClips(string fbx)
        {
            return AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
        }

        static AnimationClip Pick(IEnumerable<AnimationClip> clips, params string[] parts)
        {
            foreach (var p in parts)
            {
                var hit = clips.FirstOrDefault(c => c.name.EndsWith("|" + p, StringComparison.OrdinalIgnoreCase) || string.Equals(c.name, p, StringComparison.OrdinalIgnoreCase));
                if (hit != null) return hit;
            }
            foreach (var p in parts)
            {
                var hit = clips.FirstOrDefault(c => c.name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0);
                if (hit != null) return hit;
            }
            return null;
        }

        static ClipSet LoadOrcClips()
        {
            SetLoops(OrcFbx, n => n.IndexOf("Idle_1", StringComparison.OrdinalIgnoreCase) >= 0 || n.EndsWith("Walk") || n.EndsWith("Run"));
            var all = AllClips(OrcFbx);
            Debug.Log("[Blackout] Orc clips: " + string.Join(", ", all.Select(c => c.name)));
            return new ClipSet
            {
                idle = Pick(all, "Idle_1"), walk = Pick(all, "Walk"), run = Pick(all, "Run"),
                attack = Pick(all, "Atack"), death = Pick(all, "Death")
            };
        }

        static ClipSet LoadSkeletonClips()
        {
            string[] files = { "IDLE", "Walk", "Run", "Skeleton" };
            foreach (var f in files)
            {
                var path = SkelDir + "FBX/" + f + ".fbx";
                SetLoops(path, n => n.IndexOf("Death", StringComparison.OrdinalIgnoreCase) < 0 && n.IndexOf("Attack", StringComparison.OrdinalIgnoreCase) < 0 && n.IndexOf("Damage", StringComparison.OrdinalIgnoreCase) < 0);
            }
            var idle = AllClips(SkelDir + "FBX/IDLE.fbx");
            var walk = AllClips(SkelDir + "FBX/Walk.fbx");
            var run = AllClips(SkelDir + "FBX/Run.fbx");
            var body = AllClips(SkelDir + "FBX/Skeleton.fbx");
            Debug.Log("[Blackout] Skeleton clips: IDLE=" + string.Join("/", idle.Select(c => c.name)) + " Walk=" + string.Join("/", walk.Select(c => c.name)) +
                      " Run=" + string.Join("/", run.Select(c => c.name)) + " Body=" + string.Join("/", body.Select(c => c.name)));
            return new ClipSet
            {
                idle = idle.FirstOrDefault(), walk = walk.FirstOrDefault(), run = run.FirstOrDefault(),
                attack = Pick(body, "Attack"), death = Pick(body, "Death")
            };
        }

        static void SetLoops(string fbx, Func<string, bool> loop)
        {
            var imp = AssetImporter.GetAtPath(fbx) as ModelImporter;
            if (imp == null) return;
            var clips = imp.clipAnimations;
            if (clips == null || clips.Length == 0) clips = imp.defaultClipAnimations;
            bool changed = false;
            foreach (var c in clips)
            {
                bool want = loop(c.name);
                if (c.loopTime != want) { c.loopTime = want; changed = true; }
            }
            if (!changed) return;
            imp.clipAnimations = clips;
            imp.SaveAndReimport();
        }

        static RuntimeAnimatorController MakeController(string name, ClipSet c)
        {
            string path = AnimDir + name + ".controller";
            AssetDatabase.DeleteAsset(path);   // generated asset: rebuilt on every city build
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            var sm = ctrl.layers[0].stateMachine;

            var loco = ctrl.CreateBlendTreeInController("Locomotion", out BlendTree tree);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            var idle = c.idle != null ? c.idle : c.walk;
            if (idle != null) tree.AddChild(idle, 0f);
            if (c.walk != null) tree.AddChild(c.walk, 1.3f);
            if (c.run != null) tree.AddChild(c.run, 3.0f);
            sm.defaultState = loco;

            if (c.attack != null)
            {
                var atk = sm.AddState("Attack");
                atk.motion = c.attack;
                var into = loco.AddTransition(atk);   // from Locomotion on trigger
                into.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
                into.hasExitTime = false; into.duration = 0.1f;
                var back = atk.AddTransition(loco);
                back.hasExitTime = true; back.exitTime = 0.9f; back.duration = 0.15f;
            }
            if (c.death != null)
            {
                var die = sm.AddState("Death");
                die.motion = c.death;
                var any = sm.AddAnyStateTransition(die);
                any.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
                any.hasExitTime = false; any.duration = 0.15f; any.canTransitionToSelf = false;
            }
            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        // ------------------------------------------------------------------ creatures

        static GameObject BuildMonster(string name, string prefabPath, float height, RuntimeAnimatorController ctrl, bool hasDeath,
            AudioClip[] idle, AudioClip[] alert, AudioClip[] steps, AudioClip death, AudioClip attack, Material eyeMat,
            Transform parent, Vector2Int home, float speedScale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) { Debug.LogWarning("[Blackout] Missing creature prefab " + prefabPath); return null; }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, go.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            var b0 = RenderBounds(model);
            float scale = height / Mathf.Max(0.01f, b0.size.y);
            model.transform.localScale = Vector3.one * scale;
            var b = RenderBounds(model);
            model.transform.position += new Vector3(go.transform.position.x - b.center.x, go.transform.position.y - b.min.y, go.transform.position.z - b.center.z);
            b = RenderBounds(model);

            var animator = model.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.runtimeAnimatorController = ctrl;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            }
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = false;
            }

            var cc = go.AddComponent<CharacterController>();
            cc.height = Mathf.Max(1.4f, b.size.y);
            cc.radius = 0.45f;
            cc.center = new Vector3(0f, cc.height * 0.5f, 0f);
            cc.stepOffset = 0.2f; cc.slopeLimit = 45f; cc.skinWidth = 0.03f;

            var monster = go.AddComponent<Monster>();
            monster.animator = animator;
            monster.home = home;
            monster.speedScale = speedScale;
            monster.hasDeathClip = hasDeath;
            monster.eyeHeight = Mathf.Min(b.size.y * 0.9f, 2.4f);
            monster.idleClips = idle; monster.alertClips = alert; monster.stepClips = steps;
            monster.deathClip = death; monster.attackClip = attack;
            monster.voice = AddAudio(go, "Voice", b.size.y * 0.85f, 3f, 45f);
            monster.feet = AddAudio(go, "Feet", 0.1f, 2f, 25f);
            monster.eyes = AddEyes(model, go.transform, b.size.y, eyeMat);
            return go;
        }

        static Bounds RenderBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        static AudioSource AddAudio(GameObject owner, string name, float y, float minD, float maxD)
        {
            var g = new GameObject(name);
            g.transform.SetParent(owner.transform, false);
            g.transform.localPosition = new Vector3(0f, y, 0f);
            var s = g.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = false;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.minDistance = minD;
            s.maxDistance = maxD;
            s.dopplerLevel = 0f;
            return s;
        }

        static Renderer[] AddEyes(GameObject model, Transform owner, float height, Material eyeMat)
        {
            var bones = model.GetComponentsInChildren<Transform>();
            Transform head = bones.FirstOrDefault(t => t.name.EndsWith("head", StringComparison.OrdinalIgnoreCase) || t.name.Equals("Head", StringComparison.OrdinalIgnoreCase));
            if (head == null) head = bones.FirstOrDefault(t => t.name.IndexOf("head", StringComparison.OrdinalIgnoreCase) >= 0);
            Vector3 headPos = head != null ? head.position : owner.position + Vector3.up * height * 0.9f;
            var eyes = new List<Renderer>();
            float size = Mathf.Clamp(height * 0.04f, 0.06f, 0.12f);
            for (int side = -1; side <= 1; side += 2)
            {
                var e = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                e.name = "Eye";
                UnityEngine.Object.DestroyImmediate(e.GetComponent<Collider>());
                e.transform.SetParent(head != null ? head : owner, true);
                e.transform.position = headPos + owner.forward * (height * 0.045f) + owner.right * (side * height * 0.025f) + Vector3.up * (height * 0.015f);
                e.transform.localScale = Vector3.one * size;
                var r = e.GetComponent<MeshRenderer>();
                r.sharedMaterial = eyeMat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                eyes.Add(r);
            }
            return eyes.ToArray();
        }

        // ------------------------------------------------------------------ townspeople

        static void BuildPeople()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PeoplePrefab);
            if (prefab == null) { Debug.LogWarning("[Blackout] Townspeople prefab missing."); return; }

            var idleClip = AllClips(PeopleDir + "animation/demo_combat_idle.FBX").FirstOrDefault();
            var path = AnimDir + "Townsperson.controller";
            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            var state = ctrl.layers[0].stateMachine.AddState("Idle");
            state.motion = idleClip;
            ctrl.layers[0].stateMachine.defaultState = state;

            var root = new GameObject("Townspeople").transform;
            var rng = new System.Random(2026);
            int made = 0;
            for (int bx = 0; bx < CityBuilder.Blocks && made < 18; bx++)
            for (int bz = 0; bz < CityBuilder.Blocks && made < 18; bz++)
            {
                if (!CityBuilder.IsMarketBlock(bx, bz)) continue;
                var origin = CityBuilder.CellCenter(bx * CityBuilder.Period + 2, bz * CityBuilder.Period + 2);
                var slots = new List<Vector3>();
                for (int col = -2; col <= 1; col++)
                for (int row = -1; row <= 1; row += 2)
                    slots.Add(origin + new Vector3(col * 7f + 3.5f, 0f, row * 5f));
                slots.RemoveAll(s => (s - (origin + new Vector3(3.5f, 0f, 5f))).sqrMagnitude < 16f);   // keep the relay clear
                for (int k = 0; k < 3 && slots.Count > 0 && made < 18; k++)
                {
                    int i = rng.Next(slots.Count);
                    var p = slots[i]; slots.RemoveAt(i);
                    if (MakePerson(prefab, ctrl, root, p, rng.Next(360), ++made)) { }
                }
            }
            Debug.Log($"[Blackout] Townspeople placed: {made}.");
        }

        static bool MakePerson(GameObject prefab, RuntimeAnimatorController ctrl, Transform parent, Vector3 pos, float yaw, int index)
        {
            var go = new GameObject("Person_" + index);
            go.transform.SetParent(parent, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, go.transform);
            model.name = "Model";
            var b0 = RenderBounds(model);
            float s = 1.75f / Mathf.Max(0.01f, b0.size.y);
            model.transform.localScale = Vector3.one * s;
            var b = RenderBounds(model);
            model.transform.position += new Vector3(-b.center.x, -b.min.y, -b.center.z);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));

            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                // These civilians are not armed.
                string n = r.name.ToLowerInvariant();
                if (n.Contains("weapon") || n.Contains("gun") || n.Contains("rifle")) r.gameObject.SetActive(false);
            }
            var animator = model.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.runtimeAnimatorController = ctrl;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                animator.gameObject.AddComponent<AnimatorRandomStart>();
            }
            var cap = go.AddComponent<CapsuleCollider>();
            cap.radius = 0.3f; cap.height = 1.75f; cap.center = new Vector3(0f, 0.875f, 0f);
            return true;
        }
    }
}
