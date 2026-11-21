#!/usr/bin/env bash
set -euo pipefail

docker compose build api
docker compose up -d --wait postgres

running_api="$(docker compose ps --status running --quiet api)"
if [ -n "$running_api" ]; then
    legacy_key_dir="$(docker compose exec -T api sh -c '
        dir="$HOME/.aspnet/DataProtection-Keys"
        if [ -d "$dir" ]; then printf "%s" "$dir"; fi
    ')"
    if [ -n "$legacy_key_dir" ]; then
        docker compose exec -T api tar -C "$legacy_key_dir" -cf - . |
            docker compose run --rm --no-deps -T api --import-data-protection-keys
    fi
fi

docker compose up -d --no-build api postgres
