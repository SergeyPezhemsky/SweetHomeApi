# syntax=docker/dockerfile:1
FROM python:3.12-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    U2NET_HOME=/models \
    OMP_NUM_THREADS=2 \
    MALLOC_ARENA_MAX=2 \
    NUMBA_CACHE_DIR=/tmp/numba \
    PIP_DISABLE_PIP_VERSION_CHECK=1 \
    PIP_DEFAULT_TIMEOUT=120

RUN apt-get update \
    && apt-get install -y --no-install-recommends libglib2.0-0 curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir --retries 5 -r requirements.txt

# Пользователь и пустой каталог модели заранее: модель скачивается уже от его имени
# (rembg сохраняет её с правами 600), без chown -R, который продублировал бы ~170 МБ слоем.
RUN useradd --create-home --uid 10001 flatlay \
    && mkdir -p /models \
    && chown flatlay /models
USER flatlay

# Модель скачиваем при сборке образа: в работе сервис не ходит в интернет.
RUN python -c "from rembg import new_session; new_session('isnet-general-use')"

COPY flatlay.py app.py ./

EXPOSE 8000
HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 \
    CMD curl -fsS http://127.0.0.1:8000/health || exit 1

CMD ["uvicorn", "app:app", "--host", "0.0.0.0", "--port", "8000", "--workers", "1"]
