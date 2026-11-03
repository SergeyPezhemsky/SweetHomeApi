"""
HTTP-обёртка над flatlay.py для SweetHome: API отдаёт сюда фото вещи и получает «чистую» версию.

POST /process  (multipart, поле file) -> image/jpeg
GET  /health                          -> {"status": "ok"}

Сервис доступен только внутри docker-сети (порт наружу не публикуется).
"""
import ctypes
import io
import os
import threading

import cv2
import numpy as np
import onnxruntime as ort
from fastapi import FastAPI, File, HTTPException, UploadFile
from fastapi.responses import Response
from PIL import Image, ImageOps

import flatlay

MODEL = os.environ.get("FLATLAY_MODEL", "isnet-general-use")
BG_HEX = os.environ.get("FLATLAY_BG", "F9F7F5")
RATIO = tuple(float(x) for x in os.environ.get("FLATLAY_RATIO", "3:4").split(":"))
SIZE = int(os.environ.get("FLATLAY_SIZE", "1280"))
MARGIN = float(os.environ.get("FLATLAY_MARGIN", "0.08"))
MAX_BYTES = int(os.environ.get("FLATLAY_MAX_BYTES", str(15 * 1024 * 1024)))
# Сторона кадра для сегментации: на выходе всё равно 1280 px, а память растёт с площадью кадра.
MAX_SIDE = int(os.environ.get("FLATLAY_MAX_SIDE", "1600"))
# Защита от «бомб» вроде PNG 9000×9000 при крошечном размере файла (JPEG декодируется уменьшенным, см. _decode).
MAX_PIXELS = int(os.environ.get("FLATLAY_MAX_PIXELS", str(50_000_000)))

try:
    _libc = ctypes.CDLL("libc.so.6")
except OSError:  # не glibc — просто не возвращаем память системе явно
    _libc = None

app = FastAPI(title="SweetHome flatlay")
# Одна модель в памяти и строго по одному фото за раз (вся обработка, не только сегментация):
# предсказуемое потребление RAM на VPS.
_lock = threading.Lock()


def _create_session():
    """Сессия rembg без арены памяти ONNX Runtime: иначе пик обработки остаётся занятым навсегда."""
    from rembg import new_session

    opts = ort.SessionOptions()
    opts.enable_cpu_mem_arena = False
    opts.enable_mem_pattern = False
    threads = int(os.environ.get("OMP_NUM_THREADS", "2"))
    opts.intra_op_num_threads = threads
    opts.inter_op_num_threads = 1
    # flatlay.segment берёт готовую сессию из модуля и не создаёт свою.
    flatlay._session = new_session(MODEL, sess_opts=opts)


def _bg_bgr(hex_color: str):
    hx = hex_color.lstrip("#")
    return int(hx[4:6], 16), int(hx[2:4], 16), int(hx[0:2], 16)


def _decode(data: bytes):
    try:
        im = Image.open(io.BytesIO(data))
        # JPEG сразу декодируем в уменьшенном виде (в 2/4/8 раз): 48-Мп фото не разворачивается целиком.
        im.draft("RGB", (MAX_SIDE, MAX_SIDE))
        too_large = im.width * im.height > MAX_PIXELS
    except Exception as exc:  # noqa: BLE001 — любой мусор вместо картинки
        raise HTTPException(status_code=400, detail="not an image") from exc
    if too_large:
        raise HTTPException(status_code=413, detail="image too large")
    try:
        im = ImageOps.exif_transpose(im).convert("RGB")
    except Exception as exc:  # noqa: BLE001 — любой мусор вместо картинки
        raise HTTPException(status_code=400, detail="not an image") from exc

    # Сегментации не нужно больше 2048 px: быстрее и меньше памяти.
    scale = min(1.0, MAX_SIDE / max(im.size))
    if scale < 1:
        im = im.resize((round(im.width * scale), round(im.height * scale)), Image.LANCZOS)
    return cv2.cvtColor(np.array(im), cv2.COLOR_RGB2BGR)


def _nothing_found(reason: str) -> HTTPException:
    # Код NOTHING_FOUND API отличает от прочих 422 (например, ошибок формата запроса).
    return HTTPException(status_code=422, detail={"code": "NOTHING_FOUND", "reason": reason})


def process_bytes(data: bytes) -> bytes:
    with _lock:
        img = _decode(data)
        alpha = flatlay.segment(img, MODEL)
        try:
            mask = flatlay.clean_mask(alpha)
        except RuntimeError as exc:
            raise _nothing_found(str(exc)) from exc
        if not mask.any():
            raise _nothing_found("empty mask")
        img = flatlay.white_balance(img, mask)
        out = flatlay.compose(img, mask, _bg_bgr(BG_HEX), RATIO, SIZE, True, MARGIN)
        ok, buf = cv2.imencode(".jpg", out, [cv2.IMWRITE_JPEG_QUALITY, 90])
        del img, alpha, mask, out
        if _libc is not None:
            # Крупные буферы numpy/OpenCV glibc иначе держит у процесса — память не возвращается системе.
            _libc.malloc_trim(0)
    if not ok:
        raise HTTPException(status_code=500, detail="encode failed")
    return buf.tobytes()


@app.on_event("startup")
def warm_up():
    # Создаём сессию заранее, чтобы первое фото не ждало загрузки модели.
    with _lock:
        _create_session()
        flatlay.segment(np.full((64, 64, 3), 255, np.uint8), MODEL)


@app.get("/health")
def health():
    return {"status": "ok", "model": MODEL}


@app.post("/process")
def process(file: UploadFile = File(...)):
    data = file.file.read(MAX_BYTES + 1)
    if not data:
        raise HTTPException(status_code=400, detail="empty file")
    if len(data) > MAX_BYTES:
        raise HTTPException(status_code=413, detail="file too large")
    return Response(content=process_bytes(data), media_type="image/jpeg")
