#!/usr/bin/env python3
"""
flatlay.py — фото одежды «как в Pinterest»: вещь на чистом светлом фоне с мягкой тенью.

Установка (один раз):
    pip install "rembg[cpu]" opencv-python pillow pillow-heif
    # при первом запуске rembg скачает модель (~180 МБ) в ~/.u2net или ~/.rembg

Примеры:
    python flatlay.py IMG_4216.jpeg
    python flatlay.py photos/ -o out/                         # вся папка
    python flatlay.py shirt.heic --erase 516,566,586,730      # стереть бирку/наклейку (x0,y0,x1,y1 в пикселях исходника)
    python flatlay.py coat.jpg --bg FFFFFF --ratio 1:1 --size 2000
    python flatlay.py jeans.jpg --lint                         # убрать ворсинки/катышки
    python flatlay.py tee.jpg --debug                          # сохранить маску для проверки

Что делает:
  1. Вырезает вещь нейросетью (rembg, модель isnet-general-use) — работает для любого цвета и фона.
  2. Оставляет самый крупный объект (отбрасывает мусор на фоне), сглаживает край.
  3. Выравнивает баланс белого по исходному фону (убирает жёлтый/синий оттенок от лампы).
  4. По желанию стирает указанные прямоугольники (бирки, наклейки), подставляя соседнюю фактуру.
  5. Кладёт на фон заданного цвета и пропорции с мягкой тенью. Если вещь обрезана краем кадра,
     этот край уводится за край холста, чтобы не было «отрезанного» рукава посреди белого.
"""
import argparse
import sys
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageOps

try:
    import pillow_heif
    pillow_heif.register_heif_opener()
except ImportError:
    pass

EXTS = {".jpg", ".jpeg", ".png", ".webp", ".heic", ".heif", ".bmp", ".tif", ".tiff"}
_session = None


def load(path):
    im = ImageOps.exif_transpose(Image.open(path)).convert("RGB")  # учёт поворота с телефона
    return cv2.cvtColor(np.array(im), cv2.COLOR_RGB2BGR)


def segment(bgr, model):
    global _session
    from rembg import new_session, remove
    if _session is None:
        _session = new_session(model)
    rgb = Image.fromarray(cv2.cvtColor(bgr, cv2.COLOR_BGR2RGB))
    return np.array(remove(rgb, session=_session, only_mask=True, post_process_mask=True))


def clean_mask(a):
    m = (a > 127).astype(np.uint8) * 255
    n, lab, st, _ = cv2.connectedComponentsWithStats(m)
    if n <= 1:
        raise RuntimeError("вещь не найдена на фото")
    m = np.where(lab == 1 + np.argmax(st[1:, cv2.CC_STAT_AREA]), 255, 0).astype(np.uint8)
    # заполнить мелкие дыры (блики, пуговицы), но не большие просветы между рукавом и телом
    inv = cv2.bitwise_not(m)
    n, lab, st, _ = cv2.connectedComponentsWithStats(inv, connectivity=4)
    lim = m.size * 0.0004
    for i in range(1, n):
        if st[i, cv2.CC_STAT_AREA] < lim:
            m[lab == i] = 255
    sigma = max(1.5, min(m.shape) / 400)
    m = ((cv2.GaussianBlur(m, (0, 0), sigma) > 140) * 255).astype(np.uint8)
    return m


def white_balance(bgr, mask):
    bg = cv2.erode(cv2.bitwise_not(mask), np.ones((25, 25), np.uint8)) > 0
    if bg.sum() < 1000:
        return bgr
    px = bgr[bg].astype(np.float32)
    lum = px.mean(1)
    px = px[lum > np.percentile(lum, 50)]  # светлая часть фона — эталон белого
    mean = px.mean(0)
    gain = mean.mean() / np.maximum(mean, 1)
    gain = np.clip(gain, 0.8, 1.25)
    return np.clip(bgr.astype(np.float32) * gain, 0, 255).astype(np.uint8)


def erase_box(img, mask, box):
    x0, y0, x1, y1 = box
    h, w = img.shape[:2]
    x0, x1 = sorted((max(0, x0), min(w, x1)))
    y0, y1 = sorted((max(0, y0), min(h, y1)))
    bw, bh = x1 - x0, y1 - y0
    mask[y0:y1, x0:x1] = 255
    # ищем рядом участок той же вещи с похожей яркостью краёв → копируем фактуру
    ref = np.concatenate([img[max(0, y0 - 6):y0, x0:x1].reshape(-1, 3),
                          img[y1:y1 + 6, x0:x1].reshape(-1, 3),
                          img[y0:y1, max(0, x0 - 6):x0].reshape(-1, 3),
                          img[y0:y1, x1:x1 + 6].reshape(-1, 3)]).astype(np.float32)
    target = ref.mean(0) if len(ref) else None
    best, best_score = None, 1e18
    for k in (1.05, 1.3, 1.6, 2.0, 2.5):
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, 1), (1, -1), (-1, -1)):
            sx, sy = x0 + int(dx * bw * k), y0 + int(dy * bh * k)
            if sx < 0 or sy < 0 or sx + bw > w or sy + bh > h:
                continue
            if mask[sy:sy + bh, sx:sx + bw].mean() < 250:
                continue  # участок должен целиком лежать на вещи
            p = img[sy:sy + bh, sx:sx + bw].astype(np.float32)
            gp = cv2.cvtColor(p.astype(np.uint8), cv2.COLOR_BGR2GRAY).astype(np.float32)
            grad = np.abs(cv2.Sobel(cv2.GaussianBlur(gp, (0, 0), 3), cv2.CV_32F, 1, 1)).mean()
            # похожая яркость + без швов/складок внутри (низкий градиент и разброс)
            score = np.abs(p.mean((0, 1)) - target).sum() + gp.std() * 2 + grad * 20
            if score < best_score:
                best, best_score = (sx, sy), score
    if best is None:  # некуда копировать — обычный inpaint
        m = np.zeros((h, w), np.uint8); m[y0:y1, x0:x1] = 255
        return cv2.inpaint(img, m, 7, cv2.INPAINT_TELEA)
    sx, sy = best
    patch = img[sy:sy + bh, sx:sx + bw].copy()
    pm = np.full(patch.shape[:2], 255, np.uint8)
    return cv2.seamlessClone(patch, img, pm, ((x0 + x1) // 2, (y0 + y1) // 2), cv2.NORMAL_CLONE)


def remove_lint(img, mask):
    """Мелкие контрастные ниточки/катышки. Тонкие объекты — да, круглые блики (пуговицы) — нет."""
    g = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
    med = cv2.medianBlur(g, 9)
    inside = cv2.erode(mask, np.ones((7, 7), np.uint8)) > 0
    cand = ((np.abs(g.astype(int) - med) > 22) & inside).astype(np.uint8) * 255
    n, lab, st, _ = cv2.connectedComponentsWithStats(cand)
    fix = np.zeros_like(cand)
    for i in range(1, n):
        x, y, ww, hh, a = st[i]
        fill = a / max(1, ww * hh)
        if a < 120 and (fill < 0.45 or max(ww, hh) > 3 * max(1, min(ww, hh))):
            fix[lab == i] = 255
    fix = cv2.dilate(fix, np.ones((3, 3), np.uint8))
    return cv2.inpaint(img, fix, 4, cv2.INPAINT_TELEA)


def compose(img, mask, bg_color, ratio, size, shadow=True, margin=0.1):
    h, w = mask.shape
    ys, xs = np.where(mask > 0)
    x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
    edge = max(2, min(h, w) // 200)
    cut = dict(l=x0 < edge, r=x1 > w - edge, t=y0 < edge, b=y1 > h - edge)

    rw, rh = ratio
    CW, CH = (size, int(size * rh / rw)) if rw <= rh else (int(size * rw / rh), size)
    gw, gh = x1 - x0, y1 - y0
    # масштаб: обрезанные стороны прижимаются к краю холста, остальные — с полями
    sx = CW / gw if (cut["l"] and cut["r"]) else CW * (1 - 2 * margin) / gw
    sy = CH / gh if (cut["t"] and cut["b"]) else CH * (1 - 2 * margin) / gh
    s = min(sx, sy) if not ((cut["l"] and cut["r"]) or (cut["t"] and cut["b"])) else \
        (sx if cut["l"] and cut["r"] else sy)
    nw, nh = int(round(gw * s)), int(round(gh * s))
    ox = 0 if cut["l"] else (CW - nw if cut["r"] else (CW - nw) // 2)
    oy = 0 if cut["t"] else (CH - nh if cut["b"] else (CH - nh) // 2)
    if cut["l"] and cut["r"]: ox = (CW - nw) // 2
    if cut["t"] and cut["b"]: oy = (CH - nh) // 2

    g = cv2.resize(img[y0:y1, x0:x1], (nw, nh), interpolation=cv2.INTER_LANCZOS4).astype(np.float32)
    a = cv2.resize(cv2.GaussianBlur(mask, (5, 5), 0)[y0:y1, x0:x1], (nw, nh)).astype(np.float32) / 255

    canvas = np.zeros((CH, CW, 3), np.float32); canvas[:] = bg_color
    big_a = np.zeros((CH, CW), np.float32)
    big_g = np.zeros((CH, CW, 3), np.float32)
    # вставка с обрезкой по холсту (вещь может выходить за край)
    cx0, cy0 = max(0, ox), max(0, oy)
    cx1, cy1 = min(CW, ox + nw), min(CH, oy + nh)
    big_a[cy0:cy1, cx0:cx1] = a[cy0 - oy:cy1 - oy, cx0 - ox:cx1 - ox]
    big_g[cy0:cy1, cx0:cx1] = g[cy0 - oy:cy1 - oy, cx0 - ox:cx1 - ox]
    if shadow:
        d = int(CH * 0.008)
        sh = np.zeros_like(big_a); sh[d:, d // 2:] = big_a[:CH - d, :CW - d // 2]
        sh = cv2.GaussianBlur(sh, (0, 0), CH * 0.012) * 0.22
        canvas *= (1 - sh[..., None])
    out = canvas * (1 - big_a[..., None]) + big_g * big_a[..., None]
    return np.clip(out, 0, 255).astype(np.uint8)


def parse_box(s):
    v = [int(float(x)) for x in s.split(",")]
    if len(v) != 4:
        raise argparse.ArgumentTypeError("формат: x0,y0,x1,y1")
    return v


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("inputs", nargs="+", help="файлы или папки")
    ap.add_argument("-o", "--out", default=None, help="папка для результатов (по умолчанию рядом с исходником)")
    ap.add_argument("--bg", default="F9F7F5", help="цвет фона HEX (по умолчанию тёплый off-white F9F7F5; чисто белый FFFFFF)")
    ap.add_argument("--ratio", default="4:5", help="пропорции холста: 4:5 (Pinterest/Instagram), 2:3, 1:1 …")
    ap.add_argument("--size", type=int, default=1600, help="ширина холста в px")
    ap.add_argument("--margin", type=float, default=0.1, help="поля вокруг вещи, доля холста")
    ap.add_argument("--erase", type=parse_box, action="append", default=[], help="стереть прямоугольник x0,y0,x1,y1 (можно несколько)")
    ap.add_argument("--lint", action="store_true", help="убрать ворсинки и катышки")
    ap.add_argument("--no-wb", action="store_true", help="не выравнивать баланс белого")
    ap.add_argument("--no-shadow", action="store_true")
    ap.add_argument("--model", default="isnet-general-use", help="модель rembg: isnet-general-use | birefnet-general | u2net")
    ap.add_argument("--debug", action="store_true", help="сохранить маску рядом с результатом")
    args = ap.parse_args()

    hx = args.bg.lstrip("#")
    bg = (int(hx[4:6], 16), int(hx[2:4], 16), int(hx[0:2], 16))  # BGR
    rw, rh = (float(x) for x in args.ratio.split(":"))

    files = []
    for p in map(Path, args.inputs):
        files += sorted(f for f in p.iterdir() if f.suffix.lower() in EXTS) if p.is_dir() else [p]
    if args.erase and len(files) > 1:
        sys.exit("--erase задаётся в координатах конкретного фото — используй его для одного файла")

    for f in files:
        try:
            img = load(f)
            alpha = segment(img, args.model)
            mask = clean_mask(alpha)
            if not args.no_wb:
                img = white_balance(img, mask)
            for box in args.erase:
                img = erase_box(img, mask, box)
            if args.lint:
                img = remove_lint(img, mask)
            out = compose(img, mask, bg, (rw, rh), args.size, not args.no_shadow, args.margin)
            od = Path(args.out) if args.out else f.parent
            od.mkdir(parents=True, exist_ok=True)
            dst = od / f"{f.stem}_flatlay.jpg"
            cv2.imwrite(str(dst), out, [cv2.IMWRITE_JPEG_QUALITY, 94])
            if args.debug:
                cv2.imwrite(str(od / f"{f.stem}_mask.png"), mask)
            print(f"✓ {f.name} → {dst}")
        except Exception as e:
            print(f"✗ {f.name}: {e}", file=sys.stderr)


if __name__ == "__main__":
    main()
