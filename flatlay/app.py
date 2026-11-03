"""
HTTP-обёртка над flatlay.py для SweetHome: API отдаёт сюда фото вещи и получает «чистую» версию.

POST /process  (multipart, поле file) -> image/jpeg
GET  /health                          -> {"status": "ok"}

Сервис доступен только внутри docker-сети (порт наружу не публикуется).
"""
import io
import os
import threading

import cv2
import numpy as np
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
MAX_SIDE = int(os.environ.get("FLATLAY_MAX_SIDE", "2048"))

app = FastAPI(title="SweetHome flatlay")
# Одна модель в памяти и строго по одному фото за раз: предсказуемое потребление RAM на VPS.
_lock = threading.Lock()


def _bg_bgr(hex_color: str):
    hx = hex_color.lstrip("#")
    return int(hx[4:6], 16), int(hx[2:4], 16), int(hx[0:2], 16)


def _decode(data: bytes):
    try:
        im = ImageOps.exif_transpose(Image.open(io.BytesIO(data))).convert("RGB")
    except Exception as exc:  # noqa: BLE001 — любой мусор вместо картинки
        raise HTTPException(status_code=400, detail="not an image") from exc

    # Сегментации не нужно больше 2048 px: быстрее и меньше памяти.
    scale = min(1.0, MAX_SIDE / max(im.size))
    if scale < 1:
        im = im.resize((round(im.width * scale), round(im.height * scale)), Image.LANCZOS)
    return cv2.cvtColor(np.array(im), cv2.COLOR_RGB2BGR)


def process_bytes(data: bytes) -> bytes:
    img = _decode(data)
    with _lock:
        alpha = flatlay.segment(img, MODEL)
    try:
        mask = flatlay.clean_mask(alpha)
    except RuntimeError as exc:
        raise HTTPException(status_code=422, detail=str(exc)) from exc
    img = flatlay.white_balance(img, mask)
    out = flatlay.compose(img, mask, _bg_bgr(BG_HEX), RATIO, SIZE, True, MARGIN)
    ok, buf = cv2.imencode(".jpg", out, [cv2.IMWRITE_JPEG_QUALITY, 90])
    if not ok:
        raise HTTPException(status_code=500, detail="encode failed")
    return buf.tobytes()


@app.on_event("startup")
def warm_up():
    # Создаём сессию rembg заранее, чтобы первое фото не ждало загрузки модели.
    with _lock:
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
