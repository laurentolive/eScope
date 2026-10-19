# Pilotage d'eScope : agents IA (MCP) et scripts (REST)

[English](README.md) | **Français**

eScope peut être piloté par un agent IA (Claude Code, Cursor, VS Code Copilot… par le
[Model Context Protocol](https://modelcontextprotocol.io/)) ou par un script (HTTP + JSON simple) :
acquisitions, channels (y compris calculés), données, analyse, annotations, exports, et lectures / écritures
de la mémoire de la cible. L'agent pilote la fenêtre eScope que vous avez ouverte : vous voyez ce qu'il fait,
et vous pouvez annuler ses modifications avec Ctrl+Z.

## Activer le serveur

**Tools > AI automation** dans eScope :

- *Enable the automation server* : désactivé par défaut. Il n'écoute que sur `http://localhost:<port>`
  (port 47653 par défaut) et refuse les requêtes venant d'autres machines et des pages web.
- *Token* : chaque requête doit porter `Authorization: Bearer <token>`. Le copier, ou le régénérer pour
  révoquer les clients configurés.
- *Allow writing target memory* : désactivé par défaut. Sans cette option, `write_variable` / `write_memory`
  sont refusés (`WRITE_DISABLED`). Avec elle, **n'importe quelle variable ou adresse de la cible en marche
  peut être écrite** : l'IA peut changer le comportement de l'équipement piloté par le firmware.

Les réglages sont propres à l'utilisateur (`%LocalAppData%\eScope\automation.json`), pas à l'espace de
travail. Tant que le serveur tourne, **AI** s'affiche à droite de la barre d'outils, avec le dernier outil
appelé.

## Connecter un agent IA

La fenêtre donne la commande pour Claude Code :

```
claude mcp add --transport http escope http://localhost:47653/mcp --header "Authorization: Bearer <token>"
```

Les autres clients MCP prennent la même URL (Streamable HTTP) et le même en-tête, par exemple :

```jsonc
// Cursor : .cursor/mcp.json
{ "mcpServers": { "escope": { "url": "http://localhost:47653/mcp",
                               "headers": { "Authorization": "Bearer <token>" } } } }

// VS Code : .vscode/mcp.json
{ "servers": { "escope": { "type": "http", "url": "http://localhost:47653/mcp",
                           "headers": { "Authorization": "Bearer <token>" } } } }
```

Demander ensuite, par exemple : *« Enregistre 2 s de motor_speed et motor_current, donne-moi la fréquence
de la vitesse et dis-moi si le courant oscille, puis annote une période sur le tracé. »*

## Scripts (REST)

Mêmes outils, mêmes arguments : `POST /api/<outil>` avec un objet JSON d'arguments (envoyer au moins `{}`),
`GET /api/tools` pour la liste avec le schéma JSON des arguments. Erreurs :
`{"error": {"code": "...", "message": "..."}}` avec un statut HTTP.

```bash
curl -H "Authorization: Bearer $TOKEN" -d '{"duration_ms": 2000, "clear": true}' http://localhost:47653/api/acquire
```

[`escope_api.py`](escope_api.py) est un petit client Python (bibliothèque standard uniquement),
utilisable comme bibliothèque ou en ligne de commande :

```bash
set ESCOPE_TOKEN=...
python escope_api.py tools                       # outils et arguments
python escope_api.py acquire "{\"duration_ms\": 2000}"
python escope_api.py screenshot > plot.png
```

Utiliser `localhost`, pas `127.0.0.1` : Windows (HTTP.sys) n'accepte que le nom d'hôte enregistré par le
serveur.

## Outils

| Domaine | Outils |
|---|---|
| Espace de travail | `get_workspace`, `list_traces`, `create_trace`, `select_trace`, `rename_trace`, `save_workspace`, `undo`, `redo` |
| Devices, symboles | `list_devices`, `device_status`, `search_symbols`, `resolve_symbol` |
| Acquisition | `acquire`, `start_recording`, `stop_recording`, `get_recording_state` |
| Channels | `list_channels`, `add_channel`, `add_computed_channel`, `configure_channel`, `remove_channel` |
| Données | `get_channel_data`, `get_value_at`, `delete_data`, `clear_data` |
| Analyse | `analyze` (statistiques, fréquence, rapport cyclique, FFT), `find_edges` |
| Tracé | `list_annotations`, `add_marker`, `add_delta`, `add_annotation`, `add_data_marker`, `remove_annotation`, `set_view`, `zoom_fit`, `set_selection` |
| Cible | `read_variable`, `read_variables`, `write_variable`, `read_memory`, `write_memory` |
| Fichiers | `screenshot`, `export_csv`, `export_png`, `save_trace`, `load_trace` |

Bon à savoir :

- Les temps sont en **ms** sur l'axe X de la trace ; `value` est la valeur tracée (physique), `raw` la
  valeur de la cible.
- L'enregistrement s'applique à la **trace courante**, qui ne peut pas changer pendant l'enregistrement
  (`BUSY`).
- La cible se lit / s'écrit **avec ou sans enregistrement** : la liaison avec le device s'ouvre à la demande
  et se ferme 3 s après le dernier accès hors enregistrement (`NOT_CONNECTED` quand la cible ne répond pas).
- Avec les symboles GDB (ELF), `add_channel` déduit la taille et le format du type C, et `read_variable`
  accepte toute expression C (pointeurs suivis sur la cible).
- Les modifications du modèle sont annulables (`AI: ...` dans Edit > Undo) ; `undo` utilise le même
  historique que l'utilisateur.
- `get_channel_data` renvoie tous les échantillons de la plage : pour les longs enregistrements, réduire la
  plage ou utiliser `export_csv` / `save_trace`.

Spécification complète : [.claude/docs/automation.md](../../.claude/docs/automation.md).

## Essayer sans matériel : le simulateur V3

[`sim/sim_v3.py`](sim/sim_v3.py) est un device qui parle le protocole eScope V3 en TCP (échantillonnage
1 ms) : une sinusoïde à 50 Hz (`motor_speed`), un carré à 10 Hz (`led_state`) et un courant
(`motor_current`) qui dépend d'un paramètre modifiable (`motor_cfg`).

1. `python tools/automation/sim/sim_v3.py` (écoute sur 127.0.0.1:47700).
2. Ouvrir `tools/automation/sim/sim.escope` dans eScope (*Files > Open...*, en le déposant sur la fenêtre, ou
   `eScope.exe tools\automation\sim\sim.escope`) : il déclare le device `Sim` et une trace `Motor`.
3. Activer le serveur d'automatisation (et *Allow writing target memory* pour la partie écriture).
4. `python tools/automation/example_motor.py` : ajoute des channels, acquiert, analyse, augmente `motor_cfg`
   et remesure le courant, annote une période, enregistre `out/motor.png` et `out/motor.csv`, puis remet
   `motor_cfg` à sa valeur.
