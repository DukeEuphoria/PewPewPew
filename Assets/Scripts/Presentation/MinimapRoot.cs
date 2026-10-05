using System.Collections.Generic;
using PewPewPew.Core;
using PewPewPew.Ships;
using PewPewPew.World;
using UnityEngine;
using UnityEngine.UI;

namespace PewPewPew.Presentation
{
    public enum MinimapActivation { AlwaysOn, RequiresSubsystem, Off }

    [RequireComponent(typeof(RectTransform))]
    public class MinimapRoot : MonoBehaviour
    {
        [SerializeField] private MinimapActivation m_Activation = MinimapActivation.AlwaysOn;
        [SerializeField] private RadarEmitterDef m_DefaultEmitter;
        [SerializeField, Min(1f)] private float m_DisplayRange = 500f;
        [SerializeField, Min(0.1f)] private float m_DisplayRefreshRate = 30f;
        [SerializeField] private bool m_FadeContacts = true;
        [SerializeField, Min(0.1f)] private float m_ContactLifetime = 3f;
        [SerializeField, Range(0f, 1f)] private float m_BackgroundOpacity = 0.35f;
        [SerializeField, Range(0f, 1f)] private float m_MarkerOpacity = 0.9f;
        [SerializeField, Min(0f)] private float m_BorderWidth = 2f;
        [SerializeField] private Color m_BackgroundColor = new Color(0.03f, 0.06f, 0.07f);
        [SerializeField] private Color m_BorderColor = new Color(0.55f, 0.7f, 0.7f, 0.8f);
        [SerializeField] private Color m_PlayerColor = new Color(0.3f, 1f, 0.5f);
        [SerializeField] private Color m_ShipColor = new Color(1f, 0.4f, 0.3f);
        [SerializeField] private Color m_OrbitalColor = new Color(0.3f, 0.8f, 1f);
        [SerializeField] private Color m_AsteroidColor = new Color(0.75f, 0.75f, 0.7f);
        [SerializeField] private bool m_ShowScan = true;

        internal sealed class Contact
        {
            internal SpaceObject Source;
            internal Vector2 Position;
            internal Vector2 Forward;
            internal float WorldRadius;
            internal double LastSeen;
        }

        internal sealed class Emitter
        {
            internal object Key;
            internal RadarEmitterDef Def;
            internal Transform Mount;
            internal double Started;
            internal double Previous = -0.000001d;
            internal double NextSample;
            internal bool Present;

            internal Vector2 Forward => Def.Mode == RadarScanMode.Sweep && Def.HalfAngle >= 180f
                ? Vector2.up : (Vector2)Mount.up;
        }

        private readonly Dictionary<SpaceObject, Contact> m_Contacts = new Dictionary<SpaceObject, Contact>();
        private readonly List<SpaceObject> m_Expired = new List<SpaceObject>();
        private readonly List<Emitter> m_Emitters = new List<Emitter>();
        private Ship m_Ship;
        private GameObject m_Display;
        private MinimapGraphic m_Background;
        private MinimapGraphic m_Border;
        private MinimapGraphic m_Markers;
        private double m_NextDisplay;

        internal IEnumerable<Contact> Contacts => m_Contacts.Values;
        internal IReadOnlyList<Emitter> Emitters => m_Emitters;
        internal Ship LocalShip => m_Ship;
        internal float DisplayRange => Mathf.Max(1f, m_DisplayRange);
        internal bool ShowScan => m_ShowScan;
        internal float BorderWidth => Mathf.Max(0f, m_BorderWidth);
        internal Color BorderColor => m_BorderColor;
        internal Color BackgroundColor => WithOpacity(m_BackgroundColor, m_BackgroundOpacity);
        internal Color PlayerColor => WithOpacity(m_PlayerColor, m_MarkerOpacity);

        private void Awake()
        {
            m_Display = new GameObject("Radar Display", typeof(RectTransform));
            Stretch((RectTransform)m_Display.transform, transform);
            m_Border = CreateGraphic("Border", m_Display.transform, MinimapGraphic.Layer.Border);
            m_Background = CreateGraphic("Disc", m_Display.transform, MinimapGraphic.Layer.Background);
            RectTransform disc = m_Background.rectTransform;
            disc.offsetMin = Vector2.one * BorderWidth;
            disc.offsetMax = -Vector2.one * BorderWidth;
            m_Background.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            m_Markers = CreateGraphic("Contacts", disc, MinimapGraphic.Layer.Contacts);
            m_Display.SetActive(false);
        }

        private void OnDisable()
        {
            ResetRadar();
            if (m_Display != null) m_Display.SetActive(false);
        }

        private void LateUpdate()
        {
            Ship local = null;
            foreach (SpaceObject source in SpaceObject.ActiveObjects)
            {
                if (source is Ship ship && ship.isClient && ship.isOwned && !ship.HasExploded)
                {
                    local = ship;
                    break;
                }
            }
            if (local != m_Ship)
            {
                ResetRadar();
                m_Ship = local;
            }
            if (m_Ship == null || m_Activation == MinimapActivation.Off)
            {
                ResetRadar();
                m_Display.SetActive(false);
                return;
            }

            double now = Time.timeAsDouble;
            foreach (Emitter emitter in m_Emitters) emitter.Present = false;
            if (m_Activation == MinimapActivation.AlwaysOn)
            {
                if (m_DefaultEmitter != null) AddEmitter(this, m_DefaultEmitter, m_Ship.transform, now);
            }
            else
            {
                foreach (SubSystem system in m_Ship.SubSystems)
                {
                    if (system.IsRadarActive) AddEmitter(system, system.Def.Radar, system.System.Points[0], now);
                }
            }
            m_Emitters.RemoveAll(emitter => !emitter.Present);
            bool visible = m_Emitters.Count > 0;
            m_Display.SetActive(visible);
            if (!visible)
            {
                m_Contacts.Clear();
                return;
            }

            foreach (Emitter emitter in m_Emitters)
            {
                if (now < emitter.NextSample) continue;
                Scan(emitter, now);
                float rate = emitter.Def.Mode == RadarScanMode.JustRefresh ? emitter.Def.RefreshRate : emitter.Def.SampleRate;
                emitter.NextSample = now + 1d / rate;
            }
            m_Expired.Clear();
            foreach (Contact contact in m_Contacts.Values)
            {
                if (contact.Source == null || !contact.Source.isActiveAndEnabled ||
                    now - contact.LastSeen >= Mathf.Max(0.1f, m_ContactLifetime)) m_Expired.Add(contact.Source);
            }
            foreach (SpaceObject source in m_Expired) m_Contacts.Remove(source);
            if (now < m_NextDisplay) return;
            m_NextDisplay = now + 1d / Mathf.Max(0.1f, m_DisplayRefreshRate);
            m_Background.rectTransform.offsetMin = Vector2.one * BorderWidth;
            m_Background.rectTransform.offsetMax = -Vector2.one * BorderWidth;
            m_Background.SetVerticesDirty();
            m_Border.SetVerticesDirty();
            m_Markers.SetVerticesDirty();
        }

        private void ResetRadar()
        {
            m_Contacts.Clear();
            m_Emitters.Clear();
            m_NextDisplay = 0d;
        }

        private void AddEmitter(object key, RadarEmitterDef def, Transform mount, double now)
        {
            Emitter emitter = m_Emitters.Find(item => ReferenceEquals(item.Key, key));
            if (emitter == null || emitter.Def != def || emitter.Mount != mount)
            {
                if (emitter != null) m_Emitters.Remove(emitter);
                emitter = new Emitter { Key = key, Def = def, Mount = mount, Started = now };
                m_Emitters.Add(emitter);
            }
            emitter.Present = true;
        }

        private void Scan(Emitter emitter, double now)
        {
            double elapsed = now - emitter.Started;
            Vector2 origin = emitter.Mount.position;
            Vector2 forward = emitter.Forward;
            foreach (SpaceObject source in SpaceObject.ActiveObjects)
            {
                if (source == m_Ship || !source.isClient ||
                    !(source is Ship || source is OrbitalBody || source is Asteroid)) continue;
                if (source is Ship ship && ship.HasExploded) continue;
                Vector2 offset = (Vector2)source.transform.position - origin;
                float distance = offset.magnitude;
                float angle = Vector2.SignedAngle(forward, offset);
                if (distance > emitter.Def.Range || Mathf.Abs(angle) > emitter.Def.HalfAngle) continue;
                bool detected = emitter.Def.Mode switch
                {
                    RadarScanMode.CircularPulse => RadarMath.PulseCrosses(emitter.Previous, elapsed, distance,
                        emitter.Def.PulseSpeed, emitter.Def.RefreshRate),
                    RadarScanMode.Sweep => RadarMath.SweepCrosses(emitter.Previous, elapsed, angle,
                        emitter.Def.HalfAngle, emitter.Def.SweepSpeed),
                    _ => true,
                };
                if (!detected) continue;
                if (!m_Contacts.TryGetValue(source, out Contact contact))
                {
                    contact = new Contact { Source = source, WorldRadius = RadiusOf(source) };
                    m_Contacts.Add(source, contact);
                }
                contact.Position = source.transform.position;
                contact.Forward = source.transform.up;
                contact.LastSeen = now;
            }
            emitter.Previous = elapsed;
        }

        internal Color ContactColor(Contact contact)
        {
            Color tint = contact.Source is Ship ? m_ShipColor : contact.Source is OrbitalBody ? m_OrbitalColor : m_AsteroidColor;
            float opacity = m_MarkerOpacity;
            if (m_FadeContacts) opacity *= RadarMath.Fade(Time.timeAsDouble - contact.LastSeen, Mathf.Max(0.1f, m_ContactLifetime));
            return WithOpacity(tint, opacity);
        }

        private static float RadiusOf(SpaceObject source)
        {
            if (!(source is OrbitalBody)) return 0f;
            float radius = 0f;
            foreach (Renderer renderer in source.GetComponentsInChildren<Renderer>())
                radius = Mathf.Max(radius, Mathf.Max(renderer.bounds.extents.x, renderer.bounds.extents.y));
            return radius;
        }

        private static Color WithOpacity(Color tint, float opacity)
        {
            tint.a *= Mathf.Clamp01(opacity);
            return tint;
        }

        private MinimapGraphic CreateGraphic(string name, Transform parent, MinimapGraphic.Layer layer)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(MinimapGraphic));
            Stretch((RectTransform)child.transform, parent);
            MinimapGraphic graphic = child.GetComponent<MinimapGraphic>();
            graphic.Initialize(this, layer);
            return graphic;
        }

        private static void Stretch(RectTransform rect, Transform parent)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.pivot = Vector2.one * 0.5f;
        }
    }
}