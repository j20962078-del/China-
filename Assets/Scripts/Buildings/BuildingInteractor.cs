using UnityEngine;
using UnityEngine.UI;

namespace ChinaFishing
{
    // Вешается на каждое здание. Когда игрок ближе distance метров, над зданием появляется маркер:
    // белый круг с бегущим человечком, название и «Клик чтобы войти». Нажатие открывает окно здания.
    // Маркер — картинка интерфейса, которая каждый кадр ставится в экранную точку над зданием, поэтому он всегда
    // «смотрит» на камеру (billboard) и не закрывается ни свайпом камеры, ни джойстиком.
    public class BuildingInteractor : MonoBehaviour
    {
        public BuildingType type;
        public float distance = 11f;        // расстояние показа маркера по горизонтали, м
        public float markerHeight = 3.4f;   // высота маркера над точкой здания, м

        RectTransform marker;
        Transform playerT; Camera cam;

        public static string NameOf(BuildingType t)
        {
            return t == BuildingType.Shop ? "Магазин" : t == BuildingType.Market ? "Рынок" : "Кафе";
        }

        // Ставит три здания на карте. На «Камышевском озере» — у вывесок первых трёх домов деревни,
        // на других картах — рядом с деревенской точкой (там нет подходящих домов, поэтому здания условные).
        public static void SpawnAll(WorldBuilder world)
        {
            if (world == null) return;
            var pts = new Vector2[3];
            if (world.MapId == KamMap.Id)
            {
                pts[0] = new Vector2(18.2f, 12.0f);
                pts[1] = new Vector2(38.8f, 14.6f);
                pts[2] = new Vector2(57.5f, 18.8f);
            }
            else
            {
                Vector3 c = world.spawn;
                foreach (var sp in world.spots) if (!sp.fishing) { c = sp.pos; break; }
                pts[0] = new Vector2(c.x - 12f, c.z); pts[1] = new Vector2(c.x, c.z); pts[2] = new Vector2(c.x + 12f, c.z);
            }
            var types = new[] { BuildingType.Shop, BuildingType.Market, BuildingType.Cafe };
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject("Здание: " + NameOf(types[i]));
                go.transform.position = new Vector3(pts[i].x, world.Ground(pts[i].x, pts[i].y), pts[i].y);
                var bi = go.AddComponent<BuildingInteractor>();
                bi.type = types[i];
                FishDecor.Spawn(go.transform, types[i]);
            }
        }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null || !gm.Ready || GameUI.I == null) return;
            if (playerT == null && gm.player != null) playerT = gm.player.transform;
            if (cam == null) cam = gm.cam;

            bool show = false; Vector3 sp = Vector3.zero;
            bool free = !gm.InMenu && !Inp.UiBlocking && (FishingController.I == null || FishingController.I.state == FState.Нет);
            if (free && playerT != null && cam != null)
            {
                Vector3 d = playerT.position - transform.position; d.y = 0f;
                if (d.magnitude <= distance)
                {
                    sp = cam.WorldToScreenPoint(transform.position + Vector3.up * markerHeight);
                    show = sp.z > 0.5f;
                }
            }
            if (!show) { if (marker != null && marker.gameObject.activeSelf) marker.gameObject.SetActive(false); return; }

            if (marker == null) CreateMarker();
            if (!marker.gameObject.activeSelf) { marker.gameObject.SetActive(true); marker.SetAsLastSibling(); }
            Vector2 lp;
            var parent = marker.parent as RectTransform;
            if (parent != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, new Vector2(sp.x, sp.y), null, out lp))
                marker.anchoredPosition = lp;
            float k = Mathf.Clamp(14f / Mathf.Max(1f, sp.z), 0.7f, 1.25f);   // дальше — чуть меньше
            marker.localScale = new Vector3(k, k, 1f);
        }

        void CreateMarker()
        {
            marker = UIKit.R("Маркер: " + NameOf(type), GameUI.I.HudRoot);
            marker.anchorMin = marker.anchorMax = new Vector2(0.5f, 0.5f); marker.pivot = new Vector2(0.5f, 0.5f);
            marker.sizeDelta = new Vector2(400, 280);
            var hit = marker.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0.002f);   // невидимая зона нажатия
            var b = marker.gameObject.AddComponent<Button>();
            b.targetGraphic = hit; b.transition = Selectable.Transition.None;
            b.onClick.AddListener(Enter);

            var disc = UIParts.Disc(marker, Color.white, new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(150, 150));
            var ic = UIKit.R("Человечек", disc.transform); UIKit.Stretch(ic, 20, 20, 20, 20);
            var ii = ic.gameObject.AddComponent<Image>(); ii.sprite = UIParts.Runner; ii.preserveAspect = true; ii.raycastTarget = false;

            Shade(UIParts.Label(marker, NameOf(type), 46, new Vector2(0.5f, 1), new Vector2(0, -164), new Vector2(400, 58), TextAnchor.MiddleCenter, Color.white, FontStyle.Bold));
            Shade(UIParts.Label(marker, "Клик чтобы войти", 30, new Vector2(0.5f, 1), new Vector2(0, -222), new Vector2(400, 42), TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.95f)));
        }

        static void Shade(Text t)
        {
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0, 0, 0, 0.9f); o.effectDistance = new Vector2(2f, -2f);
        }

        void Enter()
        {
            if (marker != null) marker.gameObject.SetActive(false);
            if (UIManager.I != null) UIManager.I.Open(type);
        }

        void OnDestroy() { if (marker != null) Destroy(marker.gameObject); }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, distance);
        }
    }
}
