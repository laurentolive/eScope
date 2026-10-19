# Automation — Pilotage d'eScope par une IA (MCP) ou un script (REST)

Statut : implémenté (étapes 1 à 7). Guide utilisateur, client Python, simulateur et exemple : [tools/automation/README.md](../../tools/automation/README.md).

## Objectif

Permettre à un agent IA (Claude Code, Claude Desktop, Cursor… via MCP) ou à un script (Python, CI, banc de test,
via REST) de faire tout ce que fait l'utilisateur : acquisitions, lecture / écriture de variables sur la target,
channels (y compris calculés), manipulation et analyse des données, annotations, exports.

### Périmètre V1

- eScope **ouvert** (fenêtre visible) : l'API pilote l'application en cours, l'utilisateur voit ce que fait l'IA.
- Toutes les éditions (pas de restriction de licence).
- Serveur local uniquement (`localhost`).

### Hors périmètre V1

- Mode headless (sans fenêtre) : viendra avec `eScope.Core` (port Linux, branche `worktree-linux-port-core`).
- Accès distant (autre machine), multi-utilisateurs.
- Création / édition de devices (transport, protocole, fichier symboles) : lecture seule en V1.
- Notifications poussées (streaming des échantillons, événements) : tout est en requête / réponse.

---

## Architecture

```
Client IA (MCP)          Script (HTTP)
      │ POST /mcp              │ POST /api/<outil>
      └──────────┬─────────────┘
                 ▼
  AutomationHost  (HttpListener, localhost:<port>, auth jeton, contrôle client local / Origin)
                 │
  ToolRegistry  ← Tools/* : méthodes [McpServerTool], mêmes noms et mêmes schémas en MCP et REST
                 │
  AutomationService                   ← façade : validation, exécution sur le thread UI,
                 │                       mutations via commandMgr
                 ▼
  UiDispatcher.InvokeAsync(...)  ──►  thread UI : DeviceCtrl / TraceCtrl / ChannelCtrl / commandMgr
```

Un seul catalogue d'outils : les méthodes `[McpServerTool]` des classes `[McpServerToolType]` de
`eScope.Automation.Tools`, découvertes par `ToolRegistry`. MCP utilise les `McpServerTool` construits à partir
d'elles ; REST appelle les mêmes méthodes par réflexion (propriétés du corps JSON → paramètres par nom). Un
outil ajouté apparaît donc dans les deux. Les noms de paramètres C# sont les noms de l'API (`snake_case`),
les DTO sont sérialisés en `snake_case` (`AutomationJson.Options`).

### Fichiers

```
eScope/Automation/
  AutomationHost.cs        HttpListener, auth, routes /mcp (MCP sans session) et /api
  AutomationManager.cs     réglages ↔ serveur : démarrage / arrêt / redémarrage, dernier outil appelé
  AutomationSettings.cs    activé, port, écriture autorisée, jeton (voir Sécurité)
  AutomationService*.cs    façade (une méthode async par outil, un fichier partiel par domaine), thread UI
  ToolRegistry.cs          catalogue : outils MCP + invocation REST + GET /api/tools ; AutomationJson
  UiDispatcher.cs          marshalling vers le thread UI
  Dto.cs                   types de sortie sérialisés (System.Text.Json)
  AutomationError.cs       codes d'erreur, AutomationException (dérive de McpException)
  Tools/*.cs               [McpServerToolType] par domaine, délèguent au service
  Commands.cs              AiCommand (préfixe « AI: » des commandes), CmdConfigureChannel (état avant / après)
  AutomationForm.cs        Tools > AI automation : activation, port, jeton, commande `claude mcp add`
eScope/eScope.Automation.cs   partie de la form principale : menu Tools, indicateur « AI », cycle de vie
```

### Dépendances

| Package | Rôle |
|---|---|
| `ModelContextProtocol.Core` 2.2.0 | SDK MCP officiel C# (Apache-2.0) : `McpServer`, `McpServerTool`, `StreamableHttpServerTransport` |

**Pas d'ASP.NET Core** : la release est *framework-dependent* (runtime .NET Desktop seul) ; référencer
`Microsoft.AspNetCore.App` empêcherait eScope de démarrer sans le runtime ASP.NET Core installé. Le serveur HTTP est
donc `HttpListener` (HTTP.sys, inclus dans .NET) et `AutomationHost` refait ce que `MapMcp` fait en mode
sans session : par POST, un `StreamableHttpServerTransport { Stateless = true }` et un `McpServer` à usage unique,
réponse en SSE (`text/event-stream`). GET / DELETE sur `/mcp` → 405. Les révisions MCP avec `initialize` et
la révision 2026-07-28 (métadonnées par requête, sans `initialize`) sont servies.

`ModelContextProtocol.Core` 2.2.0 impose `System.IO.Pipelines` ≥ 10.0.10 (référence relevée, compatible net8).

Aucune dépendance Windows dans `Automation/` hormis `UiDispatcher` et `AutomationForm` (WinForms, à remplacer
sur le port Linux). `HttpListener` existe aussi sous Linux (implémentation managée).

---

## Threading

Tout le modèle tourne sur le thread UI (voir [architecture.md](architecture.md)). Les requêtes arrivent sur
des threads du pool (une tâche par requête HttpListener).

- Chaque opération du service s'exécute sur le thread UI : `UiDispatcher.InvokeAsync(Func<T>)` =
  `Control.BeginInvoke` + `TaskCompletionSource`. Le thread de la requête attend la tâche, jamais le thread UI.
- Les opérations longues ne bloquent pas le thread UI : elles sont découpées en étapes sur le thread UI
  séparées par des `await` hors UI. Exemples :
  - `acquire(duration_ms)` : démarre l'enregistrement (UI), attend les premières trames puis la durée par
    pas de 100 ms hors UI (en vérifiant que l'utilisateur n'a pas arrêté), arrête (UI), résumé 200 ms plus tard ;
  - `read_variable` : `SymbolGDB.EvaluateAsync` attendue hors UI (les lectures mémoire de GDB sont servies par
    `Device.Process`), puis `Device.ReadMem` (UI), le callback `onReadComplete` / `onReadFailed` complète un
    `TaskCompletionSource` (appelé dans `Process()`, thread UI), la requête attend cette tâche.
- **Aucune boîte de dialogue modale** depuis l'API : `eScope.Save()` (MessageBox) et les `*_Click` qui ouvrent
  des éditeurs ne sont pas appelés. Les opérations utilisent des chemins sans UI (à extraire si besoin :
  `SaveWorkspace()` sans confirmation, `AddMarker(pos, name)` sans éditeur…).
- Pas de timeout générique : chaque étape sur le thread UI est courte et s'exécute dans l'ordre d'arrivée
  (le thread UI sérialise les appels). Les attentes longues sont bornées par l'outil (`acquire` : 600 s max,
  lectures one-shot : timeout du protocole). `acquire` arrête toujours l'enregistrement qu'il a lancé (fin, erreur,
  arrêt du serveur) ; ⚠ la déconnexion du client n'est pas détectée (HttpListener) : un `acquire` abandonné va
  jusqu'au bout de sa durée. ⚠ Le thread UI peut être bloqué par eScope lui-même (ex. `ComTcp` qui tente de se connecter à un hôte
  absent, ~2 s par essai) : les appels attendent.
- Pendant un `acquire` (drapeau `acquiring`), les lectures et les modifications restent possibles ; démarrer /
  arrêter l'enregistrement, un autre `acquire` ou changer de trace → `BUSY`.
- **Changement d'onglet bloqué pendant l'enregistrement**, dans l'UI comme par l'API : l'enregistrement
  s'applique à la trace courante, elle ne change pas tant qu'il est en cours. Côté UI, `tabControl1.Selecting`
  annulé si `bRecord` (une trace créée pendant l'enregistrement n'est pas sélectionnée) ; côté API,
  `select_trace` (et `start_recording { trace }` vers une autre trace) → `BUSY`.

---

## Activation et sécurité

| Réglage | Défaut | Stockage |
|---|---|---|
| Serveur activé | **non** | `%LocalAppData%\eScope\automation.json` (par utilisateur, pas dans le workspace `.escope` qui peut être partagé) |
| Port | 47653 | idem |
| Écriture sur la target autorisée | **non** | idem |
| Jeton | aléatoire 32 octets (base64url), généré à la première activation | idem, régénérable |

- Préfixe HttpListener `http://localhost:<port>/` : enregistrable sans droits admin (`127.0.0.1` exigerait une
  URL ACL). HTTP.sys filtre sur l'en-tête `Host` : seuls `localhost` / `localhost:<port>` sont acceptés (une URL
  `http://127.0.0.1:…` reçoit `400 Invalid hostname`, idem pour un `Host` étranger → protection DNS rebinding).
- HTTP.sys ouvre le port sur toutes les interfaces : toute requête dont l'adresse distante n'est pas loopback → 403.
- HTTP.sys exige `Content-Length` (ou chunked) sur un POST : envoyer au moins `{}` (`curl -X POST` sans `-d` → 411).
- Chaque requête (`/mcp` et `/api`) exige `Authorization: Bearer <jeton>` → sinon 401.
- Contrôle de `Origin` (exigé par la spec MCP) : une requête venant d'une page web (`Origin` présent et non
  local) est refusée → 403.
- 401 sans en-tête `WWW-Authenticate` : un client MCP n'essaie pas de lancer une découverte OAuth.
- Écriture désactivée : `write_variable` / `write_memory` renvoient `WRITE_DISABLED` sans rien envoyer.
  Écriture activée : toute variable / adresse peut être écrite (pas de liste blanche).
- Indicateur à droite de la barre d'outils : « AI » quand le serveur écoute, « AI: <outil> » pendant 5 s après
  un appel, « AI ⚠ » si le serveur ne démarre pas (port pris…) ; infobulle = URL et dernier appel ; clic = réglages.
- Mutations sur le modèle → `commandMgr` avec une description préfixée `AI:` (`Undo AI: add channel speed`) :
  l'utilisateur annule ce que l'IA a fait avec Ctrl+Z.

La fenêtre « Tools > AI automation » affiche l'URL, le jeton et la commande à copier :

```
claude mcp add --transport http escope http://localhost:47653/mcp --header "Authorization: Bearer <jeton>"
```

---

## Conventions

- **Identification par nom** : device (`Device.Name`), trace (`Trace.Name`), channel (`Channel.Name`),
  marker / delta / annotation (`Name`). Noms uniques dans leur portée (déjà le cas pour les traces).
- `trace` facultatif : trace courante (onglet actif) par défaut.
- `device` facultatif s'il n'y a qu'un seul device (même règle que la Watch).
- **Temps** : millisecondes, sur l'axe X de la trace (même échelle que le plot et l'export CSV).
- **Valeurs** : `value` = valeur physique (colonne `Data` du CSV, après taille / shift / format, et script éventuel) ;
  `raw` = valeur brute lue sur la target (colonne `Raw`). Gain / offset Y sont de l'affichage, ils ne
  changent pas `value`.
- **Plage** (outils de données et d'analyse) : `range` = `"all"` (défaut, toutes les données), `"selection"`
  (sélection du plot, `INVALID_ARGUMENT` s'il n'y en a pas) ou `"view"` (partie visible, `XPos` .. `XPos + XWidth`) ;
  `from_ms` / `to_ms` facultatifs remplacent la borne correspondante. Un échantillon est dans la plage si son
  instant `XCreation + XOffset + i × période` y est.
- **Non calculable** : `null` (jamais `NaN`), ex. fréquence d'un signal plat.
- Tous les paramètres sont plats (pas d'objet imbriqué) : les noms du schéma MCP sont les noms REST.
- **Erreurs** : MCP → résultat d'outil `isError: true`, texte `An error occurred invoking '<outil>': CODE: message`
  (préfixe ajouté par le SDK). REST → HTTP 4xx/5xx,
  corps `{ "error": { "code": "...", "message": "..." } }`.

| Code | HTTP | Cas |
|---|---|---|
| `NOT_FOUND` | 404 | device / trace / channel / objet inconnu |
| `INVALID_ARGUMENT` | 400 | paramètre manquant ou invalide, expression non résolue, script qui ne compile pas |
| `NOT_CONNECTED` | 409 | liaison au device impossible à ouvrir (cible absente) |
| `NOT_SUPPORTED` | 409 | protocole sans `ReadMem` (V1/V2), évaluation sans symboles GDB |
| `WRITE_DISABLED` | 403 | écriture sur la target non autorisée dans les réglages |
| `BUSY` | 409 | acquisition en cours lancée par `acquire`, changement de trace pendant un enregistrement |
| `TIMEOUT` | 504 | pas de réponse de la target / opération trop longue |
| `READ_FAILED` | 502 | `onReadFailed` (pas de réponse sous 1 s, reconfiguration) |
| `IO_ERROR` | 500 | fichier illisible / non inscriptible (droits, fichier verrouillé) |
| `UNAVAILABLE` | 503 | fenêtre eScope pas encore créée ou en fermeture |

---

## Catalogue des outils

Noms en `snake_case`, identiques en MCP et en REST. ↺ = annulable (passe par `commandMgr`).
Chaque outil MCP porte les annotations `readOnlyHint` / `destructiveHint` adaptées, pour que le client
demande confirmation sur les outils destructifs.

### Workspace

| Outil | Paramètres | Résultat |
|---|---|---|
| `get_workspace` | — | devices (nom, protocole, transport, connecté), traces (nom, courante, nb channels), enregistrement en cours, version eScope |
| `list_traces` | — | `[{ name, current, channels, duration_ms, view {from_ms, to_ms}, selection? }]` |
| `create_trace` | `name?` | `{name}` (`TraceN` si absent) ; devient la trace courante sauf pendant un enregistrement. Non annulable (comme dans l'UI) |
| `select_trace` | `trace` | `{name}` ; `BUSY` pendant un enregistrement |
| `rename_trace` ↺ | `trace?`, `name` | `{name}` |
| `save_workspace` | — | `{path}` du `.escope` (`eScope.WriteWorkspace`, sans dialogue de confirmation ; erreur si le workspace n'a pas encore de fichier) |
| `undo` / `redo` | — | `{done, command}` ; historique **partagé** avec l'utilisateur (peut annuler une action de l'utilisateur) |

### Devices et symboles

| Outil | Paramètres | Résultat |
|---|---|---|
| `list_devices` | — | `[{ name, protocol, com, symbol_type, symbol_file, connected, supports_read_mem }]` |
| `device_status` | `device?` | symboles chargés, connecté, lectures one-shot supportées, trames périodiques reçues, channels enregistrés |
| `search_symbols` | `device?`, `prefix`, `limit=100` | noms sans préfixe device (complétion : membres après `.` / `->` avec GDB ; `.map` : noms contenant `prefix`) |
| `resolve_symbol` | `device?`, `expression` | `{ device, expression, resolved, address ("0x…"), size_bits, shift, type? }` |

### Target (lecture / écriture)

Lectures one-shot (`CMD_READ_APERIODIC`) et écritures (`CMD_WRITE_APERIODIC`), V3 uniquement, une lecture à la
fois sur le fil (voir [constraints.md](constraints.md#communication)) : environ une par tick de 50 ms.

**Liaison** : ouverte à la demande, avec ou sans enregistrement (`IProtocol.RequestLink()`, comme pour la Watch).
Hors enregistrement, seules les commandes one-shot sont envoyées (la trace ne se remplit pas) et la liaison
se referme après 3 s sans accès. L'ouverture (en tâche de fond, connexion TCP bornée à 2 s) est attendue
jusqu'à 3,5 s, puis `NOT_CONNECTED` (cible absente).

| Outil | Paramètres | Résultat |
|---|---|---|
| `read_variable` | `expression`, `device?`, `display?`, `size_bits?` | `VariableValue` `{ device, expression, address, size_bits, type, value, raw, hex, text }` |
| `read_variables` | `expressions[]`, `device?` | `VariableValue[]` ; une expression en échec a `error` (les autres sont lues) |
| `write_variable` ⚠ | `expression`, `value`, `device?`, `display?`, `size_bits?` | valeur relue après l'écriture |
| `read_memory` | `address`, `size_bits` (8/16/32/64), `device?` | `VariableValue` (`hex`) |
| `write_memory` ⚠ | `address`, `size_bits`, `value`, `device?` | valeur relue |

⚠ = `destructiveHint`, soumis au réglage « écriture autorisée » (`WRITE_DISABLED` sinon), non annulables.

- **Expression** (pas d'outil `evaluate` séparé) : avec GDB, toute expression C, évaluée à chaque lecture
  (`SymbolGDB.EvaluateAsync`, pointeurs suivis sur la cible) ; lvalue ≤ 64 bits → adresse puis lecture one-shot ;
  sinon (arithmétique, struct > 8 octets) → `text` calculé par GDB, sans adresse (non inscriptible). Avec une map :
  le symbole, taille de la map, sinon `size_bits` (défaut 32).
- **Interprétation** (`display`) : donnée, sinon déduite du type GDB (`ClassifyType`) : entier signé / non signé,
  `float` (32 bits), `double` (64 bits, lisible ici même s'il n'est pas traçable), pointeur / petite struct →
  `hex` ; map → `signed`. `type` = type C (GDB) ou l'interprétation.
- **Valeur écrite** : nombre JSON ou chaîne (`"0x1F"`, `"-3"`, `"1.5"`), vérifiée contre la taille et le type
  (plage signée / non signée, entier / flottant) ; `write_memory` accepte un négatif (complément à deux).
- **Adresse** : nombre ou chaîne (`"0x20000010"`).
- Écriture mise en file avant la relecture : envoyées dans cet ordre (`ProtocolV3.UpdateConfig`). Lecture sans
  réponse : `READ_FAILED` (échec signalé par le protocole après 1 s) ou `TIMEOUT` (5 s, rappel perdu).

### Acquisition

L'enregistrement est global et s'applique à la trace courante (`eScope.ToggleRecord`) ; les échantillons
s'ajoutent aux données existantes.

| Outil | Paramètres | Résultat |
|---|---|---|
| `get_recording_state` | — | `{ recording, trace, elapsed_ms, acquiring }` |
| `acquire` | `trace?`, `duration_ms` (1 à 600 000), `clear=false` | enregistre puis arrête, la requête attend la fin ; `clear` : `clear_data` annulable avant ; `BUSY` si un enregistrement tourne |
| `start_recording` | `trace?` | sélectionne la trace (`BUSY` si un enregistrement tourne sur une autre) et démarre ; sans effet si déjà en cours |
| `stop_recording` | — | arrête et renvoie le résumé |

**Résumé** (`RecordingSummary`) : `trace`, `recording`, `duration_ms`, `stopped_by_user`, par channel
`{ samples, new_samples, lost_samples, timeouts }`, `warnings`. `new_samples` / pertes / timeouts : depuis le
démarrage par l'API (référence liée à cet enregistrement : `null` s'il a été lancé depuis la fenêtre).
Avertissements : device absent du workspace, aucune trame reçue d'un device, pas de trame après 3 s
(`acquire`), trace sans channel, arrêt par l'utilisateur.

**Durée de `acquire`** : au moins `duration_ms` d'échantillons. Le chronomètre démarre quand chaque device de
la trace a reçu une trame (`DeviceCtrl.FrameCount`, au plus 3 s : `Protocol.IsConnected` passe à vrai dès
l'ouverture du port, avant la première trame) ; le premier tick dépile les trames déjà arrivées, d'où quelques
dizaines de ms en plus. À l'arrêt, `ProtocolV3` ferme le transport au tick suivant : les trames encore en route
sont perdues (comme depuis la fenêtre). Le résumé est calculé 200 ms après l'arrêt (trames reçues dépilées).

Testé avec un simulateur de device V3 sur TCP (RAM simulée, 1 ms).

### Channels

| Outil | Paramètres | Résultat |
|---|---|---|
| `list_channels` | `trace?` | `[{ name, device, symbol, computed, unit, color, visible, size_bits, display, samples, period_ms, y_gain, y_offset }]` |
| `add_channel` ↺ | `trace?`, `device?`, `symbol`, `name?` (l'expression), `unit?`, `color?`, `size_bits?`, `display?` | `ChannelInfo` (comme `list_channels`) |
| `add_computed_channel` ↺ | `trace?`, `name`, `script`, `unit?`, `color?`, `period_ms?` (défaut 2 ; le script lit les channels par index : donner leur période) | `ChannelInfo` ; `INVALID_ARGUMENT` avec l'erreur de compilation Roslyn |
| `configure_channel` ↺ | `trace?`, `channel`, champs facultatifs : `name`, `unit`, `color`, `visible`, `display`, `size_bits`, `y_gain` (unités / division), `y_offset` (unités), `x_offset` (ms), `script` (`""` : retire le script d'un channel device), `period_ms` (channel sans device seulement) | `ChannelInfo` après modification (`CmdConfigureChannel`, état avant / après) |
| `remove_channel` ↺ | `trace?`, `channel` | `{name}` |

Script d'un channel calculé : même syntaxe que l'éditeur de channel (`Channel.CompileScript`), corps C#
évalué pour chaque index `i`, renvoyant un `double` : `Y[i]` = le channel lui-même, `data["nom"][i]` = un autre
channel de la trace. Compilé avant d'être appliqué (erreur → `INVALID_ARGUMENT`, rien n'est modifié). C'est la
réponse aux « fonctions locales sur les channels » : filtrage, conversions, combinaisons.
⚠ Un channel calculé sans device a une période fixe de 2 ms (`ChannelCtrl.Configure`) : `data["x"][i]` lit
l'échantillon `i` de `x`, pas l'instant `2·i` ms. Pour transformer un channel device à sa cadence, mettre le
script sur ce channel (`configure_channel { script }`).

**Format** : `display` donné, sinon **déduit du type GDB** (`SymbolGDB.ResolvedType` = `ptype`, typedefs
dépliés) : pointeur / pointeur de fonction → `hex`, `float` → `float`, enum, `_Bool` ou non signé →
`unsigned`, sinon `signed` ; tableau, struct / union, `double` → `INVALID_ARGUMENT` (donner un élément ou un
membre). Symboles `.map` : pas de type → `display` donné ou `signed`.

**Taille** : celle du symbole quand elle est connue (`sizeof` GDB, taille de la map GNU) : `Device.ApplySymbol`
la réapplique au channel à chaque résolution, une autre taille ne tiendrait pas → `size_bits` différent refusé
(`add_channel` et `configure_channel`). Symbole de taille inconnue (`*_Scope.map`, map sans taille) :
`size_bits` donné, sinon 16 bits. `float` exige 32 bits. `Shift` reste à 0.

### Données

| Outil | Paramètres | Résultat |
|---|---|---|
| `get_channel_data` | `trace?`, `channels[]?`, `range`, `from_ms?`, `to_ms?`, `include_raw=false` | par channel : `{ channel, unit, period_ms, t_ms[], value[], raw[]?, lost[{time_ms, count, timeout}] }` |
| `get_value_at` | `trace?`, `time_ms`, `channels[]?` | échantillon le plus proche par channel `{ t_ms, value, raw }`, `null` hors des données |
| `delete_data` ↺ | `trace?`, `channels[]?`, `range` (`selection` / `view`) ou `from_ms` / `to_ms` (obligatoire) | `{removed}` ; les échantillons suivants reculent dans le temps (comme Edit > Delete) |
| `clear_data` ↺ | `trace?`, `channels[]?` | `{removed}` : toutes les données des channels |

Volume : pas de limite ni de décimation, tous les échantillons de la plage sont renvoyés. Pour un gros
historique, l'IA restreint `range` ou passe par un fichier : `save_trace` (`.etrace`, JSON gzippé = modèle
`Trace` complet) ou `export_csv`.

### Analyse

| Outil | Paramètres | Résultat |
|---|---|---|
| `analyze` | `trace?`, `channels[]?`, `range`, `from_ms?`, `to_ms?`, `fft=false`, `fft_points=256` | par channel : `AnalysisResult` (min, max, crête-crête, moyenne, RMS, écart-type, fréquence, période, rapport cyclique, fronts montants, pic FFT) ; avec `fft`, spectre réduit à `fft_points` bins |
| `find_edges` | `trace?`, `channel`, `range`, `from_ms?`, `to_ms?`, `threshold?` (mi-niveau), `hysteresis?` (10 % crête-crête), `direction=both` (`rising`/`falling`/`both`) | `{ threshold, hysteresis, rising_ms[], falling_ms[] }`, instants interpolés |

`analyze` réutilise `SignalAnalysis.Analyze` (même calcul que la fenêtre Analyzer, mais sur tous les channels
demandés, visibles ou non). `find_edges` utilise `SignalAnalysis.FindEdges`, extraite de `ComputeEdges`.

### Annotations et vue

| Outil | Paramètres | Résultat |
|---|---|---|
| `add_marker` ↺ | `trace?`, `time_ms`, `name?` (`MarkerN`), `color?` | `{name}` |
| `add_delta` ↺ | `trace?`, `from_ms`, `to_ms`, `name?` (`DeltaN`), `color?` | `DeltaInfo` |
| `add_annotation` ↺ | `trace?`, `channel`, `time_ms`, `text` (= nom, unique), `color?` | `{name}` ; pointe la courbe à `time_ms`, texte une division au-dessus |
| `add_data_marker` ↺ | `trace?`, `channel`, `value`, `name?` (`DataMarkerN`) | `{name}` ; ligne horizontale à une valeur (seuil) |
| `list_annotations` | `trace?` | sélection, markers `{name, time_ms}`, deltas `{name, from_ms, to_ms, width_ms}`, annotations `{text, channel, time_ms}`, data markers `{name, channel, value}` |
| `remove_annotation` ↺ | `trace?`, `name` | `{name}` ; marker, delta, annotation ou data marker |
| `set_view` | `trace?`, `from_ms`, `to_ms` | `{from_ms, to_ms}` (`eScopePlot.SetView` = `XPos` / `XWidth`) |
| `zoom_fit` | `trace?` | vue (`eScopePlot.FitAll`, la sélection est conservée) |
| `set_selection` | `trace?`, `from_ms?`, `to_ms?` | sélection (utilisée par `range: "selection"`) ; sans bornes : sélection retirée |

### Export et fichiers

| Outil | Paramètres | Résultat |
|---|---|---|
| `screenshot` | `trace?` | PNG de l'onglet tel qu'affiché (`TraceCtrl.RenderPng`) ; MCP : bloc image ; REST : `image/png`. Trace courante uniquement (`NOT_SUPPORTED` sinon) : les autres onglets ne sont pas mis en page |
| `export_csv` | `path`, `trace?`, `range`, `from_ms?`, `to_ms?`, `overwrite=false` | `{ path, bytes, rows }` ; même format que File > Export (`TraceCtrl.ExportCsv`, limité à la plage) |
| `export_png` | `path`, `trace?`, `overwrite=false` | `{ path, bytes }` ; trace courante uniquement (comme `screenshot`) |
| `save_trace` | `path`, `trace?`, `overwrite=false` | `{ path, bytes }` du `.etrace` (JSON gzippé du modèle `Trace`) |
| `load_trace` | `path` | `{ name, path, channels, missing_devices }` ; nouvelle trace au nom rendu unique (`eScope.AddTraceFromFile`, partagé avec File > Open Trace), courante sauf pendant un enregistrement |

Chemins sur la machine d'eScope : absolus, ou relatifs au répertoire de travail d'eScope ; extension ajoutée si
absente (`.csv`, `.png`, `.etrace`) ; dossier inexistant → `NOT_FOUND` (pas de création) ; fichier existant
remplacé seulement avec `overwrite: true` ; `.etrace` invalide → `INVALID_ARGUMENT`.

---

## REST

- `POST /api/{outil}` — corps JSON = paramètres de l'outil, réponse JSON = résultat (même schéma qu'en MCP).
- `GET /api/tools` — liste des outils avec leur JSON Schema d'entrée (généré depuis les mêmes DTO).
- `screenshot` renvoie `image/png`.

```bash
curl -s -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
     -d '{"duration_ms": 2000, "clear": true}' http://localhost:47653/api/acquire
```

---

## Exemple de session IA

> « Lance une acquisition de 2 s sur `motor.speed` et `motor.current`, et dis-moi si la vitesse oscille. »

1. `add_channel { symbol: "motor.speed" }`, `add_channel { symbol: "motor.current" }`
2. `acquire { duration_ms: 2000, clear: true }`
3. `analyze { channels: ["motor.speed"], fft: true }` → fréquence dominante 12,5 Hz, crête-crête 340 tr/min
4. `screenshot` → l'IA vérifie visuellement
5. `add_delta` sur une période, `add_annotation` « oscillation 12,5 Hz »
6. Proposition : `write_variable { expression: "motor.cfg.kp", value: 0.8 }` (si autorisé), nouvelle acquisition, comparaison

---

## Plan d'implémentation

1. ✅ **Socle** : SDK MCP Core + HttpListener, `AutomationHost`, réglages, jeton, contrôles client local /
   Origin, fenêtre de réglages, indicateur de barre d'outils, outil `get_workspace` de bout en bout (MCP + REST).
2. ✅ **Lecture seule** : workspace, devices, symboles, channels, données, analyse, screenshot.
3. ✅ **Mutations** : traces, channels (dont calculés), données, annotations, vue, undo/redo ; blocage du
   changement d'onglet en enregistrement (UI et API).
4. ✅ **Acquisition** : `get_recording_state`, `acquire`, `start/stop_recording`, `BUSY`.
5. ✅ **Target** : `read_variable(s)`, `read_memory`, `write_variable`, `write_memory` (garde « écriture autorisée »).
6. ✅ **Fichiers** : `export_csv`, `export_png`, `save_trace`, `load_trace`.
7. ✅ **Docs** : `CLAUDE.md`, `architecture.md`, `product.md`, `README.md` ; `tools/automation/` (guide,
   client `escope_api.py`, `example_motor.py`, simulateur V3 `sim/`) ; instructions du serveur MCP enrichies.

Vérification (pas de tests automatisés) : chaque outil testé par REST (`curl`, client Python) et MCP
(JSON-RPC : `initialize`, `tools/list`, `tools/call`, révisions 2025-06-18 et 2026-07-28) sur eScope lancé :
workspaces de test, simulateur V3 (`tools/automation/sim`, acquisitions, lectures / écritures), symboles GDB sur
un ELF réel (déduction des types), maps GNU / FormatGNU réelles ; client MCP officiel (SDK Python `mcp` 2.3,
Streamable HTTP) sur le simulateur : 46 outils, acquisition, analyse, undo / redo, export, capture, lecture /
écriture. **Pas encore fait** : essai avec Claude Code / Cursor et sur une carte (NUCLEO / RA en V3).

## Décisions

- Changement d'onglet bloqué pendant l'enregistrement (UI et API).
- Taille et format des channels déduits du type GDB.
- Pas de liste blanche en écriture : avec l'écriture autorisée, tout peut être écrit.
- Pas de limite de points sur `get_channel_data` ; gros volumes via `.etrace` / CSV.
