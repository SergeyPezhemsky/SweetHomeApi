# Docker deploy

## Что добавлено

- `Dockerfile` собирает `SweetHomeApi` в production-образ на .NET 9.
- `docker-compose.yml` поднимает API и PostgreSQL.
- `.env.example` показывает переменные для VPS.
- `.github/workflows/docker-image.yml` публикует образ в GitHub Container Registry.

## Публикация образа

После push в ветку `main` GitHub Actions соберет и опубликует образ:

```text
ghcr.io/<github-owner>/<repo-name>:latest
```

Например:

```text
ghcr.io/your-github-username/sweethomeapi:latest
```

Если репозиторий или package приватный, на VPS нужен login в `ghcr.io` через GitHub token с правом `read:packages`.

```bash
docker login ghcr.io -u <github-username>
```

## Первый запуск на VPS

Установи Docker и Docker Compose plugin, затем положи на VPS файлы:

- `docker-compose.yml`
- `.env`

Создай `.env` из примера:

```bash
cp .env.example .env
```

В `.env` замени:

```text
SWEETHOME_API_IMAGE=ghcr.io/<github-owner>/<repo-name>:latest
POSTGRES_PASSWORD=<strong-password>
```

Запуск:

```bash
docker compose pull
docker compose up -d
```

API будет доступен на порту `5000` хоста. Текущий nginx-конфиг из `_deploy/sweethome-api.nginx.conf` уже проксирует API на `127.0.0.1:5000`.

## Обновление на VPS

После нового push в `main` и успешной GitHub Actions сборки:

```bash
docker compose pull
docker compose up -d
```

## Локальный запуск

Для локальной проверки с Docker:

```bash
cp .env.example .env
docker compose up --build
```
