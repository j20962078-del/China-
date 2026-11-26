using UnityEngine;

namespace ChinaFishing
{
    // Логика поклёвки: пружина, которая гнёт вершинку удилища (RodBend) — лёгкие клевки, резкий загиб при подсечке и рывки рыбы
    public class FishBite : MonoBehaviour
    {
        public RodBend rod;
        public float maxFishWeight = 20f;   // кг, при таком весе изгиб максимальный
        public float spring = 45f;
        public float damping = 7f;
        public float jerk = 0.35f;          // сила рывков рыбы
        public float jerkSpeed = 3f;

        float pos, vel, fishPower, seed;
        bool hooked;

        // лёгкий клевок до подсечки
        public void Nibble(float strength = 1f) { vel += 2.5f * strength; }

        // подсечка: рыба села на крючок
        public void Hook(float weightKg)
        {
            hooked = true;
            fishPower = Mathf.Clamp01(weightKg / maxFishWeight);
            seed = Random.value * 100f;
            vel += 4f * fishPower;          // резкий загиб при подсечке
        }

        public void Release() { hooked = false; }   // рыба сошла или поймана

        void Update()
        {
            float target = 0f;
            if (hooked)
            {
                target = Mathf.Lerp(0.3f, 0.9f, fishPower);
                float n = Mathf.PerlinNoise(Time.time * jerkSpeed, seed) - 0.5f;
                target += n * 2f * jerk * fishPower;
            }
            float dt = Time.deltaTime;
            vel += ((target - pos) * spring - vel * damping) * dt;
            pos += vel * dt;
            if (rod != null) rod.bend = Mathf.Clamp01(pos);   // удилища может не быть (например, меняется в рюкзаке)
        }
    }
}
