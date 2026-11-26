#!/usr/bin/env python3
"""Генератор 3D-моделей 5 телескопических поплавочных удилищ (формат как у Assets/Resources/Rods).

Запуск из корня проекта:  python3 tools/gen_tele_float_rods.py
Пишет Assets/Resources/Rods/<id>.obj/.mtl и копию .mtl в Assets/Resources/RodMtl/<id>.txt.

Система координат как у остальных удилищ: ось Z вдоль удилища, торец рукояти в нуле, вершинка на Z = длина,
кольца сидят на стороне +Y. Размеры (длина, число секций, транспортная длина) взяты из описаний магазинов,
цвета и мелкие детали — приближение по облику серии, не копия с чертежа производителя.
"""
import math, os, collections

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
ROD_DIR = os.path.join(ROOT, "Assets", "Resources", "Rods")
MTL_DIR = os.path.join(ROOT, "Assets", "Resources", "RodMtl")

ORDER = ["Blank", "Grip", "Seat_Metal", "Seat_Body", "Guides_Metal", "Accent", "Joint", "Butt_Rubber"]


class Mesh:
    def __init__(self):
        self.g = collections.OrderedDict((m, ([], [], [])) for m in ORDER)   # material -> (verts, normals, faces)

    def add(self, mat, verts, norms, tris):
        V, N, F = self.g[mat]
        base = len(V)
        V.extend(verts); N.extend(norms)
        for a, b, c in tris:
            # ориентация треугольника по нормалям вершин: если против нормали — разворачиваем
            pa, pb, pc = verts[a], verts[b], verts[c]
            e1 = (pb[0]-pa[0], pb[1]-pa[1], pb[2]-pa[2]); e2 = (pc[0]-pa[0], pc[1]-pa[1], pc[2]-pa[2])
            n = (e1[1]*e2[2]-e1[2]*e2[1], e1[2]*e2[0]-e1[0]*e2[2], e1[0]*e2[1]-e1[1]*e2[0])
            na = norms[a]
            if n[0]*na[0] + n[1]*na[1] + n[2]*na[2] < 0:
                b, c = c, b
            F.append((base + a, base + b, base + c))

    def write(self, path, title, mtlname):
        with open(path, "w") as f:
            f.write("# %s\nmtllib %s\n" % (title, mtlname))
            off = 0
            for m, (V, N, F) in self.g.items():
                if not F:
                    continue
                f.write("g %s\nusemtl %s\n" % (m, m))
                for v in V: f.write("v %.5f %.5f %.5f\n" % v)
                for n in N: f.write("vn %.5f %.5f %.5f\n" % n)
                for a, b, c in F:
                    a += off + 1; b += off + 1; c += off + 1
                    f.write("f %d//%d %d//%d %d//%d\n" % (a, a, b, b, c, c))
                off += len(V)


def tube(mesh, mat, z0, z1, r0, r1, seg=16, cap0=False, cap1=False, cx=0.0, cy=0.0):
    """Усечённый цилиндр вдоль Z от z0 до z1, радиус r0 -> r1, при желании с торцевыми крышками."""
    V, N, T = [], [], []
    slope = (r0 - r1) / (z1 - z0) if z1 != z0 else 0.0
    for z, r in ((z0, r0), (z1, r1)):
        for i in range(seg):
            a = 2 * math.pi * i / seg
            s, c = math.sin(a), math.cos(a)
            V.append((cx + r * s, cy + r * c, z))
            L = math.sqrt(1 + slope * slope)
            N.append((s / L, c / L, slope / L))
    for i in range(seg):
        j = (i + 1) % seg
        T.append((i, seg + i, j)); T.append((j, seg + i, seg + j))
    mesh.add(mat, V, N, T)
    for z, r, up, on in ((z0, r0, -1.0, cap0), (z1, r1, 1.0, cap1)):
        if not on:
            continue
        cv = [(cx, cy, z)] + [(cx + r * math.sin(2 * math.pi * i / seg), cy + r * math.cos(2 * math.pi * i / seg), z) for i in range(seg)]
        cn = [(0.0, 0.0, up)] * (seg + 1)
        ct = [(0, 1 + i, 1 + (i + 1) % seg) for i in range(seg)]
        mesh.add(mat, cv, cn, ct)


def profile_tube(mesh, mat, pts, seg=16, cap0=True, cap1=True):
    """Тело вращения по списку (z, r)."""
    for k in range(len(pts) - 1):
        (za, ra), (zb, rb) = pts[k], pts[k + 1]
        tube(mesh, mat, za, zb, ra, rb, seg, cap0 and k == 0, cap1 and k == len(pts) - 2)


def torus(mesh, mat, center, R, r, axis="z", maj=14, mnr=6):
    """Тор (кольцо проводки). axis: 'z' — отверстие вдоль удилища, 'x' — кольцо лежит в плоскости YZ (петля на кончике)."""
    V, N, T = [], [], []
    cx, cy, cz = center
    for i in range(maj):
        a = 2 * math.pi * i / maj
        ca, sa = math.cos(a), math.sin(a)
        for j in range(mnr):
            b = 2 * math.pi * j / mnr
            cb, sb = math.cos(b), math.sin(b)
            rad = R + r * cb
            if axis == "z":
                p = (cx + rad * ca, cy + rad * sa, cz + r * sb); n = (cb * ca, cb * sa, sb)
            else:
                p = (cx + r * sb, cy + rad * ca, cz + rad * sa); n = (sb, cb * ca, cb * sa)
            V.append(p); N.append(n)
    for i in range(maj):
        i2 = (i + 1) % maj
        for j in range(mnr):
            j2 = (j + 1) % mnr
            a, b, c, d = i * mnr + j, i2 * mnr + j, i2 * mnr + j2, i * mnr + j2
            T.append((a, b, c)); T.append((a, c, d))
    mesh.add(mat, V, N, T)


def lerp(a, b, t): return a + (b - a) * t


def build(spec):
    L, Ns, z1 = spec["L"], spec["N"], spec["z1"]
    guided = spec["guided"]
    m = Mesh()
    # границы секций: секция 0 — рукояточная (до z1), остальные делят остаток поровну
    zs = [0.0, z1] + [z1 + (L - z1) * k / (Ns - 1) for k in range(1, Ns)]
    # радиусы начала секций (геометрическая сходимость) и конца (чуть шире начала следующей — виден ступенчатый стык)
    rs = [spec["butt_r"] * (spec["tip_r"] / spec["butt_r"]) ** (i / Ns) for i in range(Ns + 1)]
    step = spec.get("step", 0.0005)
    re_ = [rs[i + 1] + step for i in range(Ns)]
    re_[-1] = spec["tip_r"]

    blank_start = 0.42 if guided else 0.60
    # ---- рукоять ----
    if guided:
        profile_tube(m, "Butt_Rubber", [(0.0, 0.0105), (0.012, 0.0115), (0.025, 0.0115)])
        grip = [(0.025, 0.0115), (0.08, 0.0125), (0.16, 0.0132), (0.22, 0.0128)]
        profile_tube(m, "Grip", grip, cap0=True, cap1=False)
        # катушечное гнездо: две металлические втулки и корпус между ними
        profile_tube(m, "Seat_Metal", [(0.22, 0.0140), (0.232, 0.0145), (0.252, 0.0145), (0.255, 0.0138)], cap0=True, cap1=True)
        profile_tube(m, "Seat_Body", [(0.255, 0.0125), (0.285, 0.0125)], cap0=False, cap1=False)
        profile_tube(m, "Seat_Metal", [(0.285, 0.0138), (0.288, 0.0145), (0.308, 0.0145), (0.320, 0.0140)], cap0=True, cap1=True)
        profile_tube(m, "Grip", [(0.320, 0.0128), (0.37, 0.0120), (0.42, 0.0105)], cap0=False, cap1=True)
        if spec.get("accent_ring"):
            tube(m, "Accent", 0.036, 0.046, 0.0120, 0.0120, cap0=True, cap1=True)
            tube(m, "Accent", 0.405, 0.417, 0.0108, 0.0108, cap0=True, cap1=True)
    else:
        # бесфурнитурная «батовая» секция: торцевая заглушка + матовое покрытие рукояти
        profile_tube(m, "Butt_Rubber", [(0.0, 0.0125), (0.012, 0.0138), (0.026, 0.0140)])
        rr = rs[0]
        profile_tube(m, "Grip", [(0.026, rr + 0.0006), (0.30, rr + 0.0008), (0.60, rr + 0.0003)], cap0=True, cap1=True)

    # ---- секции бланка ----
    for i in range(Ns):
        za = blank_start if i == 0 else zs[i]
        zb = zs[i + 1] if i < Ns - 1 else L
        ra = lerp(rs[i], re_[i], (za - zs[i]) / (zb - zs[i])) if i == 0 else rs[i]
        # секция бланка — гладкий конус; торец (виден ступенька к следующей секции) закрыт
        tube(m, "Blank", za, zb, ra, re_[i], seg=16, cap0=(i == 0), cap1=True)
        if i < Ns - 1:
            # стык: цветное кольцо на верхнем конце нижней секции (+ тонкая тень-ступенька)
            jr = re_[i] + 0.0006
            tube(m, "Joint", zb - 0.024, zb, jr, jr, seg=16, cap0=True, cap1=True)
            if spec.get("joint_band"):
                tube(m, "Accent", zb - 0.050, zb - 0.040, jr - 0.0002, jr - 0.0002, seg=16, cap0=True, cap1=True)

    # ---- кольца проводки (на каждой секции, у верхнего конца), сторона +Y ----
    if guided:
        for i in range(Ns):
            zt = zs[i + 1] if i < Ns - 1 else L
            zg = zt - (0.045 if i < Ns - 1 else 0.004)
            r_here = lerp(rs[i], re_[i], 0.9) if i < Ns - 1 else spec["tip_r"]
            t = i / (Ns - 1)
            Rg = lerp(spec["guide_r0"], spec["guide_r1"], t)
            cy = r_here + Rg + 0.0016
            if i < Ns - 1:
                # намотка под ножкой кольца (цветная нитка)
                tube(m, "Accent", zg - 0.009, zg + 0.009, r_here + 0.0004, r_here + 0.0004, seg=10, cap0=True, cap1=True)
            # ножка: тонкий цилиндр вдоль Y — строим как «вращение» вокруг оси Y вручную
            leg_v, leg_n, leg_t = [], [], []
            seg = 6
            for yy in (r_here, cy - Rg):
                for k in range(seg):
                    a = 2 * math.pi * k / seg
                    leg_v.append((0.0012 * math.cos(a), yy, zg + 0.0012 * math.sin(a)))
                    leg_n.append((math.cos(a), 0.0, math.sin(a)))
            for k in range(seg):
                k2 = (k + 1) % seg
                leg_t.append((k, seg + k, k2)); leg_t.append((k2, seg + k, seg + k2))
            m.add("Guides_Metal", leg_v, leg_n, leg_t)
            torus(m, "Guides_Metal", (0.0, cy, zg), Rg, 0.0012, "z")
    else:
        # вершинка: петля для лески (tip loop) — кольцо в плоскости YZ у верхнего конца
        torus(m, "Accent", (0.0, spec["tip_r"] + 0.0045, L - 0.006), 0.0040, 0.0007, "x", maj=14, mnr=5)
        tube(m, "Accent", L - 0.018, L - 0.004, spec["tip_r"] + 0.0004, spec["tip_r"] + 0.0004, seg=8, cap0=True, cap1=True)
    return m


# цвета: Kd (r,g,b 0..1), Ks, Ns
def mtl_text(c):
    out = []
    for name in ORDER:
        kd, ks, ns = c[name]
        out.append("newmtl %s\nKd %.4f %.4f %.4f\nKa 0.1 0.1 0.1\nKs %.2f %.2f %.2f\nNs %d\nd 1\n" % (name, kd[0], kd[1], kd[2], ks, ks, ks, ns))
    return "\n".join(out)


def hexc(h): return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


def palette(blank, grip, accent, joint, seat_body="4A4F55", metal="CFD4DA", guides="CFD4DA", butt="101112", blank_ks=0.30):
    return {
        "Blank": (hexc(blank), blank_ks, 140), "Grip": (hexc(grip), 0.15, 30),
        "Seat_Metal": (hexc(metal), 0.69, 140), "Seat_Body": (hexc(seat_body), 0.63, 120),
        "Guides_Metal": (hexc(guides), 0.69, 150), "Accent": (hexc(accent), 0.21, 100),
        "Joint": (hexc(joint), 0.40, 90), "Butt_Rubber": (hexc(butt), 0.15, 19),
    }


SPECS = [
    # Mikado Sasori Tele Float: чёрный карбон, оранжево-красные акценты, 7 секций, кольца на каждой секции, винтовое гнездо
    dict(id="31_float_Mikado_Sasori_Tele_Float", title="Mikado Sasori Tele Float", L=4.0, N=7, z1=0.92, guided=True,
         butt_r=0.0112, tip_r=0.0014, guide_r0=0.0105, guide_r1=0.0032, accent_ring=True,
         pal=palette("14171A", "202326", "E8590C", "2B2F33", seat_body="30343A")),
    # Mikado Intro II Tele Float: 6 секций, пробковый рисунок рукояти, красный акцент (как у серии Intro II в игре)
    dict(id="32_float_Mikado_Intro_II_Tele_Float", title="Mikado Intro II Tele Float", L=3.6, N=6, z1=0.95, guided=True,
         butt_r=0.0115, tip_r=0.0016, guide_r0=0.0100, guide_r1=0.0032, accent_ring=True, joint_band=True,
         pal=palette("141619", "B98A58", "C0161D", "1B1D20")),
    # Mikado Nihonto Mini Float: чёрный, 8 секций, очень компактная (транспортная 70 см), тонкий бланк
    dict(id="33_float_Mikado_Nihonto_Mini_Float", title="Mikado Nihonto Mini Float", L=3.6, N=8, z1=0.70, guided=True,
         butt_r=0.0105, tip_r=0.0012, guide_r0=0.0085, guide_r1=0.0028, accent_ring=True, step=0.0004,
         pal=palette("0C0D0F", "17191C", "B8935A", "1A1C1F", seat_body="25282C", metal="B9BEC4", guides="B9BEC4")),
    # Daiwa Aqualite Tele Whip: телескопическая «вожжа» без колец, углеродный бланк, матовый (satin) комель, петля на кончике, 5 секций
    dict(id="34_float_Daiwa_Aqualite_Tele_Whip", title="Daiwa Aqualite Tele Whip", L=5.0, N=5, z1=1.0, guided=False,
         butt_r=0.0120, tip_r=0.0011, joint_band=True, step=0.0005,
         pal=palette("101820", "2A3138", "2F7FD1", "9AA3AB")),
    # DAM Sensomax Tele (Pole): телескопическое махолётное удилище без колец, 4 секции
    dict(id="35_float_DAM_Sensomax_Tele", title="DAM Sensomax Tele Pole", L=4.0, N=4, z1=1.0, guided=False,
         butt_r=0.0125, tip_r=0.0013, joint_band=True, step=0.0006,
         pal=palette("15181B", "23272B", "F2A900", "3A3F45")),
]


def main():
    os.makedirs(ROD_DIR, exist_ok=True); os.makedirs(MTL_DIR, exist_ok=True)
    for s in SPECS:
        m = build(s)
        base = os.path.join(ROD_DIR, s["id"])
        m.write(base + ".obj", s["title"], s["id"] + ".mtl")
        txt = mtl_text(s["pal"])
        open(base + ".mtl", "w").write(txt)
        open(os.path.join(MTL_DIR, s["id"] + ".txt"), "w").write(txt)
        nv = sum(len(g[0]) for g in m.g.values()); nf = sum(len(g[2]) for g in m.g.values())
        zmax = max(v[2] for g in m.g.values() for v in g[0]); zmin = min(v[2] for g in m.g.values() for v in g[0])
        print("%s: вершин %d, треугольников %d, Z %.3f..%.3f" % (s["id"], nv, nf, zmin, zmax))


if __name__ == "__main__":
    main()
