using UnityEngine;

namespace ChinaFishing
{
    // Понятные координаты вместо сырых X/Z: карта делится на 10×10 секторов (А..К, 1..10),
    // внутри сектора ещё 10×10 клеток → «Д4-37». Работает для любой карты через MapUv.
    public static class GridCoord
    {
        static readonly string[] Cols = { "А", "Б", "В", "Г", "Д", "Е", "Ж", "З", "И", "К" };

        public static string Format(Vector2 uv)
        {
            float fx = Mathf.Clamp01(uv.x) * 10f, fy = Mathf.Clamp01(uv.y) * 10f;
            int cx = Mathf.Min(9, Mathf.FloorToInt(fx)), cy = Mathf.Min(9, Mathf.FloorToInt(fy));
            int sx = Mathf.Min(9, Mathf.FloorToInt((fx - cx) * 10f)), sy = Mathf.Min(9, Mathf.FloorToInt((fy - cy) * 10f));
            return Cols[cx] + (cy + 1) + "-" + sx + sy;
        }

        public static string Format(Vector3 world)
        {
            var gm = GameManager.I;
            if (gm == null || gm.world == null) return "";
            return Format(gm.world.MapUv(world));
        }
    }
}
