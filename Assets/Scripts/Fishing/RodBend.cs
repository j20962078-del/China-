using System.Collections.Generic;
using UnityEngine;

namespace ChinaFishing
{
    // Изгиб удилища: bend 0..1. Вершины модели смещаются по параболе от руки к вершинке (чем дальше от руки — тем сильнее),
    // а точка кончика (rodTip, к ней крепится леска) переезжает вместе с изгибом. Гнётся в сторону добычи (поплавка), иначе — вниз.
    public class RodBend : MonoBehaviour
    {
        public float bend;                 // 0 — прямое, 1 — максимальный изгиб
        public float maxDeflection = 0.38f; // смещение вершинки при bend = 1, в долях длины удилища
        public Transform tip;               // точка кончика
        public Transform pullTarget;        // куда тянет рыба (поплавок); null — вниз

        class Part { public Mesh mesh; public Vector3[] orig, work; public Matrix4x4 toRod, fromRod; }
        readonly List<Part> parts = new List<Part>();
        Vector3 tipBase;
        float length = 1f, lastBend = -1f;
        Vector3 dir = Vector3.forward, lastDir;

        // modelRoot — экземпляр 3D-модели внутри этого объекта; tipPivot — точка кончика, её текущая позиция считается прямой
        public void Init(GameObject modelRoot, Transform tipPivot)
        {
            tip = tipPivot; tipBase = tipPivot.localPosition;
            float maxY = 0.1f;
            foreach (var mf in modelRoot.GetComponentsInChildren<MeshFilter>())
            {
                var src = mf.sharedMesh;
                if (src == null || !src.isReadable) continue;       // нечитаемый меш гнуть нельзя — удилище останется прямым
                var p = new Part();
                p.mesh = Instantiate(src); p.mesh.MarkDynamic();
                mf.sharedMesh = p.mesh;
                p.orig = p.mesh.vertices; p.work = new Vector3[p.orig.Length];
                p.toRod = transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                p.fromRod = p.toRod.inverse;
                for (int i = 0; i < p.orig.Length; i++) { float y = p.toRod.MultiplyPoint3x4(p.orig[i]).y; if (y > maxY) maxY = y; }
                parts.Add(p);
            }
            length = Mathf.Max(0.5f, maxY);
        }

        void LateUpdate()
        {
            if (parts.Count == 0 && tip == null) return;
            // направление изгиба: от оси удилища к цели, всё в локальных осях
            Vector3 axis = transform.up;
            Vector3 want = pullTarget != null && tip != null ? pullTarget.position - tip.position : Vector3.down;
            want = Vector3.ProjectOnPlane(want, axis);
            if (want.sqrMagnitude < 1e-4f) want = Vector3.ProjectOnPlane(Vector3.down, axis);
            if (want.sqrMagnitude < 1e-4f) want = transform.forward;
            Vector3 local = transform.InverseTransformDirection(want.normalized);
            local.y = 0f;
            if (local.sqrMagnitude < 1e-4f) local = Vector3.forward;
            dir = Vector3.Slerp(dir, local.normalized, 1f - Mathf.Exp(-10f * Time.deltaTime));

            float b = Mathf.Clamp01(bend);
            if (Mathf.Abs(b - lastBend) < 0.002f && (b < 0.002f || (dir - lastDir).sqrMagnitude < 0.0004f)) return;
            lastBend = b; lastDir = dir;

            float amp = b * maxDeflection * length;
            foreach (var p in parts)
            {
                for (int i = 0; i < p.orig.Length; i++)
                {
                    Vector3 r = p.toRod.MultiplyPoint3x4(p.orig[i]);
                    float t = Mathf.Clamp01(r.y / length);          // за ладонью (y < 0) — жёстко
                    r += dir * (amp * t * t);
                    p.work[i] = p.fromRod.MultiplyPoint3x4(r);
                }
                p.mesh.vertices = p.work;
                p.mesh.RecalculateBounds();
            }
            if (tip != null)
            {
                float tt = Mathf.Clamp01(tipBase.y / length);
                tip.localPosition = tipBase + dir * (amp * tt * tt);
            }
        }

        void OnDestroy() { foreach (var p in parts) if (p.mesh != null) Destroy(p.mesh); }
    }
}
