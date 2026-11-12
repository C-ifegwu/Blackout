using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Blackout.World.Editor
{
    /// <summary>
    /// Adds the lantern, guide spirit, key, plaque clues, braziers, seal, traps, hiding places and oil flasks to the
    /// city scene. Called at the end of CityBuilder.Build. Everything is placed on open ground by a physics check.
    /// </summary>
    public static class QuestBuilder
    {
        static readonly List<Vector3> Used = new List<Vector3>();
        static readonly List<Vector3> Keep = new List<Vector3>();   // places that must stay trap-free
        static System.Random _rng;

        public static void Build()
        {
            _rng = new System.Random(11);
            Used.Clear(); Keep.Clear();
            Physics.SyncTransforms();

            var root = new GameObject("Quest").transform;
            var spawn = GameObject.Find("PlayerSpawn");
            var gp = Object.FindFirstObjectByType<SonarGameplay>();
            if (spawn != null) Keep.Add(spawn.transform.position);
            if (gp != null) foreach (var r in gp.relays) if (r != null) Keep.Add(r.transform.position);
            var tgt = GameObject.Find("DeliveryTarget");
            Vector3 towerPos = tgt != null ? tgt.transform.position : CityBuilder.CellCenter(CityBuilder.Half, 2);
            Keep.Add(towerPos);

            var stone = Lit("QStone", new Color(0.55f, 0.55f, 0.52f));
            var wood = Lit("QWood", new Color(0.28f, 0.2f, 0.12f));
            var ring = Lit("QPitRim", new Color(0.5f, 0.42f, 0.3f));
            var hole = Lit("QPitHole", new Color(0.0f, 0.0f, 0.0f));
            var wire = Emissive("QWire", new Color(0.8f, 0.8f, 0.85f), new Color(0.5f, 0.5f, 0.55f));
            var brass = Emissive("QKey", new Color(0.9f, 0.7f, 0.15f), new Color(1f, 0.75f, 0.2f) * 1.2f);
            var flask = Emissive("QFlask", new Color(0.9f, 0.55f, 0.1f), new Color(1f, 0.6f, 0.15f) * 1.4f);
            var lanternBody = Emissive("QLanternBody", new Color(0.2f, 0.15f, 0.08f), new Color(1f, 0.6f, 0.25f) * 2f);
            var spirit = Emissive("QSpirit", Color.white, new Color(0.6f, 0.85f, 1f) * 2f);
            var sealMat = Emissive("QSeal", new Color(0.2f, 0.05f, 0.05f), new Color(1f, 0.1f, 0.08f) * 1.2f);

            var fadeMat = new Material(Shader.Find("Sprites/Default")) { name = "QFade", color = new Color(0f, 0f, 0f, 0f) };
            fadeMat.renderQueue = 4000;
            AssetDatabase.CreateAsset(fadeMat, AssetDatabase.GenerateUniqueAssetPath("Assets/_Sonar/Materials/QFade.mat"));

            // lantern, guide spirit, fader
            var lg = new GameObject("Lantern");
            lg.transform.SetParent(root, false);
            var lantern = lg.AddComponent<Lantern>();
            lantern.bodyMaterial = lanternBody;

            var fg = new GameObject("ScreenFader");
            fg.transform.SetParent(root, false);
            fg.AddComponent<ScreenFader>().fadeMaterial = fadeMat;

            var sg = new GameObject("GuideSpirit");
            sg.transform.SetParent(root, false);
            var gs = sg.AddComponent<GuideSpirit>();
            gs.gameplay = gp;
            gs.glowMaterial = spirit;

            var og = new GameObject("ObjectiveGlow");
            og.transform.SetParent(root, false);
            var glow = og.AddComponent<ObjectiveGlow>();
            glow.gameplay = gp;
            glow.glowMaterial = Emissive("QObjectiveGlow", new Color(0.1f, 0.3f, 0.8f), new Color(0.25f, 0.55f, 1f) * 3f);

            // plaques beside the relays
            var chainGo = new GameObject("PuzzleChain");
            chainGo.transform.SetParent(root, false);
            var chain = chainGo.AddComponent<PuzzleChain>();
            var plaques = new List<Plaque>();
            if (gp != null)
            {
                for (int i = 0; i < gp.relays.Length && i < 3; i++)
                {
                    Vector3 p = Clear(gp.relays[i].transform.position + new Vector3(2.8f, 0f, 1.8f), 0.8f);
                    var go = Prim(PrimitiveType.Cube, "Plaque_" + (i + 1), root, p + Vector3.up * 0.65f, new Vector3(0.9f, 1.3f, 0.25f), stone, true);
                    plaques.Add(go.AddComponent<Plaque>());
                    Used.Add(p);
                    Flask(root, Clear(p + new Vector3(1.6f, 0f, -1.2f), 0.5f), flask);
                }
            }
            chain.plaques = plaques.ToArray();

            // key on a pedestal, ringed by pits
            Vector3 keyPos = Clear(CityBuilder.CellCenter(20, 8), 1.0f);
            Keep.Add(keyPos);
            var ped = Prim(PrimitiveType.Cylinder, "KeyPedestal", root, keyPos + Vector3.up * 0.4f, new Vector3(0.9f, 0.4f, 0.9f), stone, true);
            var key = Prim(PrimitiveType.Cube, "Key", root, keyPos + Vector3.up * 1.05f, new Vector3(0.12f, 0.12f, 0.45f), brass, false);
            chain.key = key.transform;
            for (int i = 0; i < 3; i++)
            {
                float a = (i * 120f + 40f) * Mathf.Deg2Rad;
                Vector3 pp = Clear(keyPos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 4.2f, 1.6f);
                MakePit(root, pp, ring, hole);
            }

            // braziers near the third relay
            Vector3 hub = gp != null && gp.relays.Length > 2 ? gp.relays[2].transform.position : towerPos + new Vector3(0, 0, 24);
            string[] names = { "Cyan", "Green", "Amber" };
            Color[] cols = { new Color(0.35f, 0.95f, 1f), new Color(0.45f, 1f, 0.55f), new Color(1f, 0.7f, 0.25f) };
            Vector3[] bo = { new Vector3(-5f, 0f, 3f), new Vector3(5f, 0f, 3f), new Vector3(0f, 0f, -7f) };
            var braz = new List<Brazier>();
            for (int i = 0; i < 3; i++)
            {
                Vector3 bp = Clear(hub + bo[i], 1.0f);
                Used.Add(bp); Keep.Add(bp);
                var post = Prim(PrimitiveType.Cylinder, "Brazier_" + names[i], root, bp + Vector3.up * 0.55f, new Vector3(0.5f, 0.55f, 0.5f), stone, true);
                var fl = Prim(PrimitiveType.Sphere, "Flame", post.transform, bp + Vector3.up * 1.3f, Vector3.one * 0.45f, Emissive("QFlame" + names[i], cols[i] * 0.3f, cols[i] * 0.25f), false);
                fl.transform.localScale = Vector3.one * 0.8f;
                var lgo = new GameObject("FlameLight");
                lgo.transform.SetParent(post.transform, false);
                lgo.transform.position = bp + Vector3.up * 1.6f;
                var l = lgo.AddComponent<Light>();
                l.type = LightType.Point; l.range = 6f; l.intensity = 2.2f; l.color = cols[i]; l.shadows = LightShadows.None; l.enabled = false;
                var b = post.AddComponent<Brazier>();
                b.colorName = names[i]; b.color = cols[i]; b.flame = fl.GetComponent<Renderer>(); b.flameLight = l;
                braz.Add(b);
            }
            chain.braziers = braz.ToArray();

            // the seal at the tower
            var seal = Prim(PrimitiveType.Cube, "Seal", root, new Vector3(towerPos.x, 1.9f, towerPos.z), new Vector3(3.2f, 3.6f, 0.25f), sealMat, false);
            var sd = seal.AddComponent<SealDoor>();
            sd.glow = seal.GetComponent<Renderer>();
            chain.seal = sd;

            // traps, hiding places, oil
            for (int n = 0; n < 12; n++) { if (TryRoadPoint(out var p, 14f, 9f, 0.9f)) { MakePit(root, p, ring, hole); } }
            for (int n = 0; n < 7; n++)
            {
                if (TryRoadPoint(out var p, 12f, 14f, 0.6f, out bool alongX))
                {
                    var w = Prim(PrimitiveType.Cylinder, "Tripwire", root, p + Vector3.up * 0.18f, new Vector3(0.025f, 3.4f, 0.025f), wire, false);
                    w.transform.rotation = Quaternion.Euler(0f, alongX ? 0f : 90f, 90f);
                    var tw = w.AddComponent<Tripwire>();
                    tw.halfLength = 3.4f;
                    Used.Add(p);
                }
            }
            for (int n = 0; n < 10; n++)
            {
                if (TryRoadPoint(out var p, 8f, 16f, 1.4f))
                {
                    var spot = new GameObject("HidingSpot");
                    spot.transform.SetParent(root, false);
                    spot.transform.position = p;
                    spot.AddComponent<HidingSpot>();
                    Prim(PrimitiveType.Cube, "CrateA", spot.transform, p + new Vector3(-1.1f, 0.5f, 0f), new Vector3(0.9f, 1.0f, 1.3f), wood, true);
                    Prim(PrimitiveType.Cube, "CrateB", spot.transform, p + new Vector3(1.1f, 0.6f, 0f), new Vector3(0.9f, 1.2f, 1.3f), wood, true);
                    Prim(PrimitiveType.Cube, "CrateC", spot.transform, p + new Vector3(0f, 0.55f, -0.85f), new Vector3(1.4f, 1.1f, 0.5f), wood, true);
                    Used.Add(p);
                }
            }
            for (int n = 0; n < 9; n++) { if (TryRoadPoint(out var p, 5f, 8f, 0.5f)) { Flask(root, p, flask); Used.Add(p); } }

            // interactive consoles on each relay and at the tower radio, feedback effects, oil gauge
            var uiMat = new Material(Shader.Find("Sprites/Default")) { name = "QFx", color = Color.white };
            AssetDatabase.CreateAsset(uiMat, AssetDatabase.GenerateUniqueAssetPath("Assets/_Sonar/Materials/QFx.mat"));
            if (gp != null)
            {
                for (int i = 0; i < gp.relays.Length; i++)
                    MakeConsole(root, gp.relays[i].transform.position + new Vector3(0f, 0f, 0.8f), i, gp, gp.relays[i].color);
                MakeConsole(root, new Vector3(towerPos.x, 0f, towerPos.z + 1.7f), gp.relays.Length, gp, new Color(1f, 0.8f, 0.3f));
            }
            var fxGo = new GameObject("FeedbackFx");
            fxGo.transform.SetParent(root, false);
            var fx = fxGo.AddComponent<FeedbackFx>();
            fx.fxMaterial = uiMat;
            fx.successClip = gp != null ? gp.relayOnClip : null;
            var gaugeGo = new GameObject("OilGauge");
            gaugeGo.transform.SetParent(root, false);
            gaugeGo.AddComponent<OilGauge>().barMaterial = uiMat;

            if (gp != null) gp.chain = chain;
            EditorUtility.SetDirty(gp);
            Debug.Log("[Blackout] Quest objects built.");
        }

        // ---------------------------------------------------------------- helpers

        static void MakeConsole(Transform root, Vector3 ground, int index, SonarGameplay gp, Color color)
        {
            var mat = Emissive("QConsole" + index, color * 0.2f, color * 0.25f);
            var go = Prim(PrimitiveType.Cylinder, "Console_" + index, root, ground + Vector3.up * 1.0f, new Vector3(0.6f, 0.3f, 0.6f), mat, true);
            var rc = go.AddComponent<RelayConsole>();
            rc.index = index;
            rc.gameplay = gp;
            rc.panel = go.GetComponent<Renderer>();
            rc.color = color;
        }

        static void Flask(Transform root, Vector3 p, Material m)
        {
            var go = Prim(PrimitiveType.Cylinder, "OilFlask", root, p + Vector3.up * 0.22f, new Vector3(0.18f, 0.22f, 0.18f), m, false);
            go.AddComponent<OilFlask>();
        }

        static void MakePit(Transform root, Vector3 p, Material ring, Material hole)
        {
            var pit = new GameObject("Pit");
            pit.transform.SetParent(root, false);
            pit.transform.position = p;
            pit.AddComponent<Pit>();
            Prim(PrimitiveType.Cylinder, "Rim", pit.transform, p + Vector3.up * 0.015f, new Vector3(2.9f, 0.01f, 2.9f), ring, false);
            Prim(PrimitiveType.Cylinder, "Hole", pit.transform, p + Vector3.up * 0.03f, new Vector3(1.9f, 0.01f, 1.9f), hole, false);
            Used.Add(p);
        }

        static bool TryRoadPoint(out Vector3 p, float minFromKeep, float minFromUsed, float clearRadius) => TryRoadPoint(out p, minFromKeep, minFromUsed, clearRadius, out _);

        static bool TryRoadPoint(out Vector3 p, float minFromKeep, float minFromUsed, float clearRadius, out bool alongX)
        {
            alongX = true;
            for (int tries = 0; tries < 80; tries++)
            {
                int i = _rng.Next(2, CityBuilder.Size - 2), j = _rng.Next(2, CityBuilder.Size - 2);
                bool lineX = j % CityBuilder.Period == 0, lineZ = i % CityBuilder.Period == 0;
                if (!lineX && !lineZ) continue;
                var c = CityBuilder.CellCenter(i, j) + new Vector3(((float)_rng.NextDouble() - 0.5f) * 6f, 0f, ((float)_rng.NextDouble() - 0.5f) * 6f);
                if (!Open(c, clearRadius)) continue;
                if (Near(Keep, c, minFromKeep) || Near(Used, c, minFromUsed)) continue;
                alongX = lineZ && !lineX;   // wire across a north-south street runs east-west
                p = c;
                return true;
            }
            p = Vector3.zero;
            return false;
        }

        static bool Near(List<Vector3> list, Vector3 c, float d)
        {
            foreach (var v in list) { var dx = v.x - c.x; var dz = v.z - c.z; if (dx * dx + dz * dz < d * d) return true; }
            return false;
        }

        static bool Open(Vector3 c, float r) => !Physics.CheckSphere(c + Vector3.up * (0.95f), r);

        static Vector3 Clear(Vector3 want, float r)
        {
            if (Open(want, r)) return want;
            for (float ring = 1.5f; ring <= 9f; ring += 1.5f)
                for (int k = 0; k < 8; k++)
                {
                    float a = k * Mathf.PI / 4f;
                    var c = want + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * ring;
                    if (Open(c, r)) return c;
                }
            return want;
        }

        static GameObject Prim(PrimitiveType t, string name, Transform parent, Vector3 pos, Vector3 scale, Material m, bool collider)
        {
            var go = GameObject.CreatePrimitive(t);
            go.name = name;
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (!collider) { var c = go.GetComponent<Collider>(); if (c != null) Object.DestroyImmediate(c); }
            return go;
        }

        static Material Lit(string name, Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", 0f);
            m.SetFloat("_Metallic", 0f);
            AssetDatabase.CreateAsset(m, AssetDatabase.GenerateUniqueAssetPath("Assets/_World/Materials/" + name + ".mat"));
            return m;
        }

        static Material Emissive(string name, Color c, Color emission)
        {
            var m = Lit(name, c);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor("_EmissionColor", emission);
            return m;
        }
    }
}
