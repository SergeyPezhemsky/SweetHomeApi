# Wardrobe: background removal (flatlay) and outfit builder

## Why
Photos of clothes taken at home have cluttered backgrounds and uneven light, so the wardrobe grid and outfit cards look messy. The user already has a working `flatlay.py` script (rembg + OpenCV) that produces clean "Pinterest-style" product shots. Users also want to compose outfits themselves, edit saved outfits and tweak a suggested outfit before saving it — today only suggested outfits can be saved as-is.

## What Changes
- New `flatlay` service (Python, FastAPI, rembg `isnet-general-use`) built from the user's script, running as a separate container in `docker-compose.yml`, reachable only from the API.
- Item photos: the original upload is kept; a flatlay version is produced automatically in the background after every upload. Items expose processing status and which variant is displayed; the user can switch between the clean and the original photo or retry a failed processing.
- Outfits: `PUT api/v1/wardrobe/outfits/{id}` to rename, change items, occasion, temperature and note.
- Client: processing badges and polling, original/clean toggle in the item form, an outfit builder page (create and edit), «Изменить» on saved outfits and on suggestions in the picker.

## Impact
- Affected spec: `wardrobe`.
- Affected code: `flatlay/` (new), `docker-compose.yml`, `Application.Modules.Wardrobe`, `Persistance` (migration), `SweetHomeApi` (controller, background worker, options), client `wardrobe-module`.
- Ops: the VPS builds one more image (~1.2 GB with the model) and the container needs up to ~1.5 GB RAM while processing.
