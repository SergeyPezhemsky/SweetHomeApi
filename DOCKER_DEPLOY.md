# Docker deploy

## Схема

Основной деплой работает без registry:

```text
push в master -> GitHub Actions -> SSH на VPS -> git pull/reset -> docker compose up -d --build
```

Готовый Docker image никуда не публикуется. Он собирается прямо на VPS из `Dockerfile`.

## Подготовка VPS

Один раз установи на VPS:

- Docker Engine
- Docker Compose plugin
- Git

Потом склонируй проект:

```bash
git clone https://github.com/SergeyPezhemsky/SweetHomeApi.git /opt/sweethome-api
cd /opt/sweethome-api
cp .env.example .env
```

В `.env` задай нормальный пароль:

```text
POSTGRES_PASSWORD=<strong-password>
```

Первый ручной запуск:

```bash
docker compose up -d --build
```

API будет слушать порт VPS из `API_PORT`, по умолчанию `5000`.

## SSH ключ для GitHub Actions

На своей машине или на VPS создай отдельный ключ для деплоя:

```bash
ssh-keygen -t ed25519 -C "github-actions-sweethome" -f sweethome_deploy_key
```

Публичный ключ `sweethome_deploy_key.pub` добавь на VPS в:

```text
~/.ssh/authorized_keys
```

Приватный ключ `sweethome_deploy_key` добавь в GitHub repository secrets.

## GitHub secrets

В GitHub открой:

```text
Repository -> Settings -> Secrets and variables -> Actions -> New repository secret
```

Добавь:

```text
VPS_HOST=<ip-or-domain>
VPS_USER=<ssh-user>
VPS_PORT=22
VPS_SSH_KEY=<private-ssh-key>
VPS_APP_DIR=/opt/sweethome-api
```

## Автодеплой

Workflow `.github/workflows/deploy-vps.yml` запускается после каждого push в `master` и выполняет на VPS:

```bash
cd "$VPS_APP_DIR"
git fetch origin master
git reset --hard origin/master
docker compose up -d --build
docker image prune -f
```

## Проверка на VPS

```bash
docker compose ps
docker compose logs -f api
```

Если nginx использует текущий конфиг из `_deploy/sweethome-api.nginx.conf`, он проксирует API на:

```text
http://127.0.0.1:5000
```
