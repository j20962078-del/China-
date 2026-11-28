using UnityEngine;

namespace ChinaFishing
{
    // Рыба на крючке: 3D-модель под водой у поплавка во время вываживания.
    // Это только картинка: игровая сила, натяжение и усталость считаются в FishingController (физика Rigidbody/Joint здесь не нужна).
    // Рыба держится ртом у крючка, тело уходит от игрока, качается по повадкам вида, на рывках мечется,
    // а уставшая заваливается на бок и всплывает к поверхности.
    public class FishOnHook : MonoBehaviour
    {
        FishDef def; FishBehaviourData beh; float len, seed;

        public static FishOnHook Spawn(FishDef f, float lengthM)
        {
            float L = Mathf.Clamp(lengthM, 0.12f, 2.6f);
            var go = FishModel.Build(f, L);
            go.name = "Рыба на крючке: " + f.name;
            var h = go.AddComponent<FishOnHook>();
            h.def = f; h.len = L; h.beh = FishBehaviour.For(f.id); h.seed = Random.value * 100f;
            return h;
        }

        // hook — точка крючка (у поплавка), player — где стоит игрок
        public void Tick(Vector3 hook, Vector3 player, float stamina, float burst)
        {
            Vector3 to = player - hook; to.y = 0f;
            if (to.sqrMagnitude < 0.01f) to = Vector3.back;
            to.Normalize();

            float t = Time.time + seed;
            float live = Mathf.Lerp(0.3f, 1f, stamina);
            float tired = Mathf.Clamp01((0.25f - stamina) / 0.25f);
            float swing = Mathf.Sin(t * beh.wiggleSpeed * live) * beh.wiggleAmount * live * (1f + burst * beh.thrash);
            float roll = Mathf.Sin(t * beh.wiggleSpeed * 0.7f + 1.3f) * 8f * live + tired * 65f;
            float pitch = -burst * 18f;

            var look = Quaternion.LookRotation(to, Vector3.up);      // нос к игроку: рыбу тянут за рот
            var yaw = Quaternion.Euler(0f, swing, 0f);
            Vector3 center = hook + yaw * (-to * len * 0.5f);         // рот у крючка, тело позади
            center.y = WorldBuilder.WaterLevel - Mathf.Lerp(0.3f + len * 0.15f, 0.1f, tired);
            transform.position = center;
            transform.rotation = yaw * look * Quaternion.Euler(pitch, 0f, roll);
        }
    }
}
