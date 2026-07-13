using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Spellblade
{
    /// <summary>
    /// SPELLBLADE WORLD MAP (Plan 03) — the front door of the game.
    /// Drop on one empty GameObject in the WorldMap scene; everything is
    /// runtime-generated like the arena: a dark-parchment 2.5D map of all
    /// 8 biome regions, clickable arena beacons in unlocked regions, fog and
    /// snow-glint flourishes with a whisper of mouse parallax, and a footer
    /// with currencies + the Sanctum placeholder (Plan 05 replaces it).
    ///
    /// Layering (ortho camera at z=-10 looking +Z):
    ///   z≈9    backdrop quad (parchment gradient)
    ///   z≈8    region blobs (soft-circle sprites, world space)
    ///   z≈5-7  flourish particles under a parallax root
    ///   z=-2   screen-space-camera canvas (labels, beacons, tooltip, bars)
    /// The canvas renders through the camera, so the URP bloom volume makes
    /// the beacons glow.
    /// </summary>
    public class WorldMapBootstrap : MonoBehaviour
    {
        [Header("Map")]
        public float orthoSize = 5f;
        [Tooltip("World units the flourish layer drifts against the mouse.")]
        public float parallaxAmount = 0.18f;

        [Header("Mood")]
        [Range(0f, 2f)] public float bloomIntensity = 1.4f;

        private Camera _cam;
        private Canvas _canvas;
        private Font _font;
        private Transform _parallaxRoot;
        private RectTransform _tooltip;
        private Text _tooltipText;

        private static Sprite _softCircle;

        private void Start()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            SetupCamera();
            SetupPostProcessing();
            BuildBackdrop();
            BuildCanvas();
            BuildRegions();
            BuildHeaderFooter();
            BuildTooltip();
            EnsureEventSystem();

            Debug.Log("[Spellblade] World map assembled. Click a glowing beacon to enter its arena.");
        }

        private void Update()
        {
            // Barely-perceptible parallax: flourishes drift a few pixels against the mouse.
            if (_parallaxRoot == null || Mouse.current == null) return;
            var mouse = Mouse.current.position.ReadValue();
            var norm = new Vector2(
                Mathf.Clamp01(mouse.x / Mathf.Max(1f, Screen.width)) - 0.5f,
                Mathf.Clamp01(mouse.y / Mathf.Max(1f, Screen.height)) - 0.5f);
            var target = new Vector3(-norm.x, -norm.y, 0f) * parallaxAmount;
            _parallaxRoot.localPosition = Vector3.Lerp(_parallaxRoot.localPosition, target, 4f * Time.deltaTime);
        }

        // ---------------------------------------------------------------- Camera

        private void SetupCamera()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                _cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }

            _cam.orthographic = true;
            _cam.orthographicSize = orthoSize;
            _cam.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.015f, 0.015f, 0.03f);
        }

        private void SetupPostProcessing()
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();

            // Lower threshold than the arena: UI colors cap at 1.0, and the
            // beacons need to catch the bloom to sing.
            var bloom = profile.Add<Bloom>();
            bloom.intensity.Override(bloomIntensity);
            bloom.threshold.Override(0.6f);
            bloom.scatter.Override(0.7f);

            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.42f);
            vignette.smoothness.Override(0.5f);
            vignette.color.Override(new Color(0.01f, 0.01f, 0.03f));

            var volumeGo = new GameObject("World Map Volume");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.profile = profile;

            var data = _cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        }

        // ---------------------------------------------------------------- World layer

        private float HalfWidth => orthoSize * _cam.aspect;

        /// <summary>Normalized map coords (0-1) → world position at depth z.</summary>
        private Vector3 MapToWorld(Vector2 norm, float z) =>
            new Vector3((norm.x - 0.5f) * 2f * HalfWidth, (norm.y - 0.5f) * 2f * orthoSize, z);

        private void BuildBackdrop()
        {
            // Dark parchment: a radial gradient — aged center, void edges.
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Map Backdrop";
            Destroy(quad.GetComponent<Collider>());
            quad.transform.position = new Vector3(0f, 0f, 9f);
            quad.transform.localScale = new Vector3(HalfWidth * 2.1f, orthoSize * 2.1f, 1f);

            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetTexture("_BaseMap", MakeParchmentTexture());
            quad.GetComponent<Renderer>().material = mat;

            // Region blobs + flourishes.
            var blobRoot = new GameObject("Region Blobs").transform;
            _parallaxRoot = new GameObject("Flourish Layer (parallax)").transform;

            foreach (var region in RegionDefs.All)
            {
                bool unlocked = GameSession.IsRegionUnlocked(region.id);
                var center = MapToWorld(region.mapPosition, 8f);
                var tint = unlocked
                    ? Color.Lerp(region.elementTint, new Color(0.5f, 0.5f, 0.55f), 0.35f)
                    : new Color(0.16f, 0.16f, 0.19f); // locked: desaturated slate

                // Irregular blob: 4 offset soft circles, big and faint.
                var offsets = new[]
                {
                    new Vector3(0f, 0f, 0f),
                    new Vector3(0.55f, 0.25f, 0.02f),
                    new Vector3(-0.45f, 0.35f, 0.04f),
                    new Vector3(0.15f, -0.45f, 0.06f),
                };
                for (int i = 0; i < offsets.Length; i++)
                {
                    float size = (i == 0 ? 2.6f : 1.8f) + (i * 0.13f);
                    float alpha = unlocked ? 0.34f : 0.22f;
                    MakeSoftDisc($"{region.id} blob {i}", blobRoot, center + offsets[i], size,
                                 new Color(tint.r, tint.g, tint.b, alpha));
                }

                BuildFlourish(region, unlocked, center);
            }
        }

        /// <summary>The "2.5" part: fog over the Shadow Reach, snow-glints over the
        /// Rimeholt, slow mist over everything locked.</summary>
        private void BuildFlourish(RegionDef region, bool unlocked, Vector3 center)
        {
            if (region.id == "shadow")
            {
                RegionParticles(region.id, center + Vector3.back * 2f, // z≈6, over the blob
                    new Color(0.55f, 0.60f, 0.58f, 0.10f), sizeMin: 1.6f, sizeMax: 2.6f,
                    rate: 2.5f, life: 11f, speed: 0.16f, box: new Vector3(2.8f, 1.9f, 0.1f));
            }
            else if (region.id == "frost" && unlocked)
            {
                // Gentle cyan glints: tiny, short-lived twinkles.
                RegionParticles(region.id, center + Vector3.back * 2f,
                    new Color(0.55f, 0.95f, 1.00f, 0.55f), sizeMin: 0.04f, sizeMax: 0.10f,
                    rate: 7f, life: 1.4f, speed: 0.05f, box: new Vector3(2.4f, 1.7f, 0.1f));
            }
            else if (!unlocked)
            {
                RegionParticles(region.id, center + Vector3.back * 1f, // z≈7
                    new Color(0.40f, 0.42f, 0.45f, 0.085f), sizeMin: 1.4f, sizeMax: 2.2f,
                    rate: 1.8f, life: 13f, speed: 0.12f, box: new Vector3(2.6f, 1.8f, 0.1f));
            }
        }

        private void RegionParticles(string regionId, Vector3 position, Color color,
                                     float sizeMin, float sizeMax, float rate, float life,
                                     float speed, Vector3 box)
        {
            var go = new GameObject($"Flourish ({regionId})");
            go.transform.SetParent(_parallaxRoot, false);
            go.transform.position = position;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            go.GetComponent<ParticleSystemRenderer>().material = SpellbladeParticles.MakeParticleMat(Color.white);

            var main = ps.main;
            main.startLifetime = life;
            main.startSpeed = speed;
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = color;
            main.maxParticles = 64;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.prewarm = true;
            main.loop = true;

            var emission = ps.emission;
            emission.rateOverTime = rate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.1f;
            noise.frequency = 0.15f;

            ps.Play();
        }

        // ---------------------------------------------------------------- Canvas layer

        private void BuildCanvas()
        {
            var go = new GameObject("World Map Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas = go.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = _cam;
            _canvas.planeDistance = 8f; // z=-2 — in front of the world layer

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }

        private void BuildRegions()
        {
            foreach (var region in RegionDefs.All)
            {
                bool unlocked = GameSession.IsRegionUnlocked(region.id);

                // Name label above the blob center.
                var label = MakeText($"{region.id} label", region.displayName.ToUpperInvariant(),
                                     region.mapPosition + new Vector2(0f, 0.085f),
                                     22, FontStyle.Bold,
                                     unlocked
                                         ? new Color(0.82f, 0.78f, 0.66f)          // aged gold-gray
                                         : new Color(0.45f, 0.45f, 0.50f, 0.85f)); // dimmed
                label.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

                // Sub-label: locked mists / unlocked-but-empty regions.
                if (!unlocked)
                {
                    MakeText($"{region.id} sub", "The mists have not parted.",
                             region.mapPosition + new Vector2(0f, 0.052f),
                             14, FontStyle.Italic, new Color(0.42f, 0.42f, 0.48f, 0.8f));
                }
                else if (region.nodes.Count == 0)
                {
                    MakeText($"{region.id} sub", "No arenas yet.",
                             region.mapPosition + new Vector2(0f, 0.052f),
                             14, FontStyle.Italic, new Color(0.55f, 0.55f, 0.60f, 0.85f));
                }

                if (!unlocked) continue; // locked regions show no nodes

                foreach (var placement in region.nodes)
                    MapNodeUI.Spawn(this, _canvas.transform, placement, region);
            }
        }

        private void BuildHeaderFooter()
        {
            // Title, small, top-left.
            var title = MakeText("Title", "S P E L L B L A D E", Vector2.zero, 26, FontStyle.Bold,
                                 new Color(0.85f, 0.80f, 0.95f, 0.9f));
            var titleRect = title.GetComponent<RectTransform>();
            titleRect.anchorMin = titleRect.anchorMax = new Vector2(0f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.anchoredPosition = new Vector2(28f, -22f);
            title.GetComponent<Text>().alignment = TextAnchor.UpperLeft;

            // Bottom bar: currencies left, Sanctum placeholder right.
            var bar = new GameObject("Bottom Bar", typeof(Image));
            bar.transform.SetParent(_canvas.transform, false);
            var barRect = bar.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(1f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.sizeDelta = new Vector2(0f, 56f);
            bar.GetComponent<Image>().color = new Color(0.02f, 0.02f, 0.05f, 0.78f);

            var save = SaveSystem.Data;
            var wallet = MakeText("Wallet", $"ARCANE ESSENCE  {save.arcaneEssence}      ELEMENT SHARDS  {save.elementShards}",
                                  Vector2.zero, 18, FontStyle.Normal, new Color(0.75f, 0.72f, 0.62f));
            var walletRect = wallet.GetComponent<RectTransform>();
            walletRect.SetParent(bar.transform, false);
            walletRect.anchorMin = new Vector2(0f, 0.5f);
            walletRect.anchorMax = new Vector2(0f, 0.5f);
            walletRect.pivot = new Vector2(0f, 0.5f);
            walletRect.anchoredPosition = new Vector2(28f, 0f);
            walletRect.sizeDelta = new Vector2(800f, 40f);
            wallet.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;

            // Sanctum button — placeholder; Plan 05 replaces it with the upgrade panel.
            var button = new GameObject("Sanctum Button", typeof(Image), typeof(Button));
            button.transform.SetParent(bar.transform, false);
            var btnRect = button.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(1f, 0.5f);
            btnRect.anchorMax = new Vector2(1f, 0.5f);
            btnRect.pivot = new Vector2(1f, 0.5f);
            btnRect.anchoredPosition = new Vector2(-28f, 0f);
            btnRect.sizeDelta = new Vector2(170f, 38f);
            button.GetComponent<Image>().color = new Color(0.25f, 0.18f, 0.40f, 0.9f);
            button.GetComponent<Button>().onClick.AddListener(
                () => Debug.Log("[Spellblade] Sanctum opens with Plan 05 (progression)."));

            var btnLabel = MakeText("Sanctum Label", "SANCTUM", Vector2.zero, 18, FontStyle.Bold,
                                    new Color(0.9f, 0.85f, 1f));
            var btnLabelRect = btnLabel.GetComponent<RectTransform>();
            btnLabelRect.SetParent(button.transform, false);
            btnLabelRect.anchorMin = Vector2.zero;
            btnLabelRect.anchorMax = Vector2.one;
            btnLabelRect.sizeDelta = Vector2.zero;
            btnLabelRect.anchoredPosition = Vector2.zero;
            btnLabel.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
        }

        private void BuildTooltip()
        {
            var go = new GameObject("Node Tooltip", typeof(Image));
            go.transform.SetParent(_canvas.transform, false);
            _tooltip = go.GetComponent<RectTransform>();
            _tooltip.sizeDelta = new Vector2(320f, 92f);
            go.GetComponent<Image>().color = new Color(0.03f, 0.03f, 0.07f, 0.92f);

            var textGo = MakeText("Tooltip Text", "", Vector2.zero, 16, FontStyle.Normal,
                                  new Color(0.88f, 0.86f, 0.80f));
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.SetParent(go.transform, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = new Vector2(-24f, -16f);
            textRect.anchoredPosition = Vector2.zero;
            _tooltipText = textGo.GetComponent<Text>();
            _tooltipText.alignment = TextAnchor.MiddleLeft;

            go.SetActive(false);
        }

        public void ShowTooltip(MapNodeUI node, string body)
        {
            _tooltip.gameObject.SetActive(true);
            _tooltipText.text = body;

            // Sit beside the node; flip to the left near the right edge.
            var nodeRect = (RectTransform)node.transform;
            var pos = nodeRect.anchorMin; // nodes anchor at their normalized map position
            bool flip = pos.x > 0.66f;
            _tooltip.anchorMin = _tooltip.anchorMax = pos;
            _tooltip.pivot = new Vector2(flip ? 1f : 0f, 0.5f);
            _tooltip.anchoredPosition = new Vector2(flip ? -34f : 34f, 0f);
        }

        public void HideTooltip() => _tooltip.gameObject.SetActive(false);

        // ---------------------------------------------------------------- Helpers

        private GameObject MakeText(string name, string content, Vector2 normPos,
                                    int size, FontStyle style, Color color)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(_canvas.transform, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = normPos;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(460f, 60f);

            var text = go.GetComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.text = content;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return go;
        }

        private void MakeSoftDisc(string name, Transform parent, Vector3 position, float size, Color color)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * size;

            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = SoftCircle;
            sr.color = color;
            sr.material = SpellbladeParticles.MakeParticleMat(Color.white); // transparent, soft
        }

        /// <summary>Soft radial-gradient sprite, generated once — blobs and beacons share it.</summary>
        public static Sprite SoftCircle
        {
            get
            {
                if (_softCircle != null) return _softCircle;

                const int size = 128;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                float center = (size - 1) / 2f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                        float a = Mathf.Clamp01(1f - dist);
                        a = a * a * (3f - 2f * a); // smoothstep falloff
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                }
                tex.Apply();
                _softCircle = Sprite.Create(tex, new Rect(0, 0, size, size),
                                            new Vector2(0.5f, 0.5f), size);
                return _softCircle;
            }
        }

        private static Texture2D MakeParchmentTexture()
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var center = new Vector2(size / 2f, size / 2f);
            var inner = new Color(0.115f, 0.105f, 0.095f); // aged umber heart
            var outer = new Color(0.025f, 0.025f, 0.045f); // void edges
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float t = Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), center) / (size * 0.62f));
                    // Faint hand-drawn grain so the parchment isn't a sterile gradient.
                    float grain = (Mathf.PerlinNoise(x * 0.11f, y * 0.11f) - 0.5f) * 0.018f;
                    var c = Color.Lerp(inner, outer, t * t);
                    tex.SetPixel(x, y, new Color(c.r + grain, c.g + grain, c.b + grain, 1f));
                }
            }
            tex.Apply();
            return tex;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            // InputSystemUIInputModule, NOT StandaloneInputModule — this project is
            // new-Input-System-only and the legacy module would throw every frame.
        }
    }
}
