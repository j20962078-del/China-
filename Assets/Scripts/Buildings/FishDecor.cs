using UnityEngine;

namespace ChinaFishing
{
    // Рыба у зданий: на прилавке рынка, на столе кафе и трофейная щука на доске у магазина.
    // Положение приблизительное (дома в деревне стоят по-разному): подправьте смещения offset и поворот yaw ниже.
    public static class FishDecor
    {
        static readonly Vector3 offset = new Vector3(2.4f, 0f, 0f);   // относительно точки здания, м
        const float yaw = 0f;

        public static void Spawn(Transform building, BuildingType type)
        {
            var wood = Mats.Lit(120, 84, 52, 0.15f);
            var root = new GameObject("Рыба у здания").transform;
            root.SetParent(building, false);
            root.localPosition = offset;
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);

            if (type == BuildingType.Shop)
            {
                // трофей на стене: доска и щука, повёрнутая носом вверх
                WorldBuilder.Prim(PrimitiveType.Cube, root, new Vector3(0f, 1.5f, 0f), new Vector3(0.18f, 1.5f, 0.7f), wood);
                AddFish(root, "pike", 1.1f, new Vector3(0.14f, 1.5f, 0f), Quaternion.Euler(-90f, 0f, 0f) * Quaternion.Euler(0f, 90f, 0f));
                return;
            }
            // стол с ножками
            WorldBuilder.Prim(PrimitiveType.Cube, root, new Vector3(0f, 0.85f, 0f), new Vector3(1.8f, 0.08f, 0.9f), wood);
            foreach (float sx in new[] { -0.8f, 0.8f })
                foreach (float sz in new[] { -0.35f, 0.35f })
                    WorldBuilder.Prim(PrimitiveType.Cube, root, new Vector3(sx, 0.42f, sz), new Vector3(0.08f, 0.84f, 0.08f), wood);
            Quaternion lying = Quaternion.Euler(0f, 90f, 90f);   // рыба лежит на боку, нос вдоль стола
            if (type == BuildingType.Market)
            {
                AddFish(root, "carp", 0.55f, new Vector3(-0.5f, 0.95f, -0.2f), lying);
                AddFish(root, "pike", 0.8f, new Vector3(0.1f, 0.95f, 0.15f), Quaternion.Euler(0f, 80f, 90f));
                AddFish(root, "crucian", 0.28f, new Vector3(0.6f, 0.93f, -0.2f), Quaternion.Euler(0f, 100f, 90f));
                AddFish(root, "perch", 0.3f, new Vector3(0.55f, 0.93f, 0.25f), Quaternion.Euler(0f, 70f, 90f));
            }
            else
            {
                AddFish(root, "zander", 0.6f, new Vector3(-0.3f, 0.95f, 0f), lying);
                AddFish(root, "perch", 0.3f, new Vector3(0.4f, 0.93f, 0.1f), Quaternion.Euler(0f, 80f, 90f));
            }
        }

        static void AddFish(Transform parent, string id, float lengthM, Vector3 lp, Quaternion rot)
        {
            var f = GameData.GetFish(id);
            if (f == null) return;
            var go = FishModel.Build(f, lengthM);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = lp;
            go.transform.localRotation = rot;
        }
    }
}
