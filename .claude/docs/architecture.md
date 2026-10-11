# Architecture — Décisions techniques

## Vue d'ensemble

```
eScope.cs (WinForms)
  │
  └─ Timer 50 ms (DeviceProcessTimer)
       ├─ pour chaque device : DeviceCtrl.Process()
       │     ├─ IProtocol.Process() ── trames → FrameComplete → Channel.Y[]
       │     └─ SymbolGDB.Bridge.Service() ── requêtes mémoire de GDB → ReadMem / WriteMem
       └─ trace courante : TraceCtrl.Refresh() ── ScottPlot

Automation/ (serveur local MCP + REST, désactivé par défaut)
  └─ requête HTTP (thread du pool) ── AutomationService ── UiDispatcher.InvokeAsync ──► thread UI
```

Tout le modèle tourne sur le thread UI. V3 délègue la lecture du transport à deux tâches
background (pipeline) ; les trames sont dépilées et les callbacks déclenchés dans `Process()`.

---

## Couche protocole (Protocol/)

### V3 — actif (protocolV3.cs)

**Format de trame Device → PC :**
```
55  flowId  dataSize  cmd  [payload…]  AA
```
- `0x55` / `0xAA` : sync bytes
- `flowId` : compteur 0-255, incrément par trame ; `FlowCounter` en déduit les trames perdues
  (écart > 64 : resynchronisation au lieu d'inventer des échantillons)
- `cmd` : `0x43` = données, `ERR_FLAG` (0x80) = erreur

**Paquet de config PC → Device (6 octets par commande) :**
```
CMD(1)  SIZE(1)  ADDR[0..3] little-endian
```

Codes et règles des lectures one-shot : voir [api.md](api.md#format-filaire-protocol-v3).

**Liaison et acquisition :**
- `Start()` / `Stop()` (enregistrement de la trace) : lectures périodiques envoyées / effacées (`CMD_CLEAR`)
- Hors enregistrement, un accès one-shot (`ReadMem` / `WriteMem` : Watch, bridge GDB, API) ouvre la liaison ;
  les lectures périodiques restent en attente, `FrameComplete` n'est pas levé, et la liaison est refermée
  après 3 s sans accès (le port série / la sonde RTT restent libres pour les autres outils)
- Échec de connexion : les one-shot en attente échouent ; hors enregistrement, nouvel essai au prochain accès
  (échec immédiat pendant 1 s)

**Timeout & récupération :**
- 1000 ms sans trame alors que des lectures périodiques sont configurées → événement `Timeout`
  + `Reconfigure()` = `CMD_CLEAR` puis renvoi des périodiques ; les one-shot en cours échouent (`onReadFailed`)
- Lecture one-shot sans réponse sous 1 s → même reconfiguration
- Sans lecture périodique configurée, aucune trame n'est attendue : pas de timeout
- Reconfiguration pour une autre raison (désenregistrement d'un canal) : les one-shot en cours sont renvoyées

**I/O asynchrone :**
- Task `ReadCom` : boucle sur `ICom.ReadAsync()` → écrit dans un `System.IO.Pipelines.PipeWriter`
- Task `FetchFrame` : lit le `PipeReader`, extrait les trames, enqueue `(payload, trames perdues)` dans une `ConcurrentQueue`
- `Process()` (timer 50 ms) dépile, associe les données aux lectures configurées, lève `FrameComplete`
  et appelle les callbacks one-shot, puis envoie la config en attente (`UpdateConfig()`)

### V1 / V2 — dépréciés
Voir [constraints.md](constraints.md).

---

## Couche communication (Com/)

**Ouverture** : `ICom.Open()` peut bloquer (hôte TCP absent, bridge RTT qui démarre). Les protocoles
l'appellent en tâche de fond via `ComOpener` : `Process()` ne bloque jamais le thread UI. Un échec
est retenté 1 s plus tard tant que la liaison est demandée ; une tentative encore en cours à l'arrêt
est refermée dès qu'elle aboutit. `ComTcp` borne la connexion à 2 s.

**`ComSerial`** (RJCP.IO.Ports.SerialPortStream) :
- Config string : `"Serial:COM3,115200,8,N,1,500"`
- `ReadAsync` : event-driven via `DataReceived` + `TaskCompletionSource`
- `ReadBufferSize = 16` octets (latence minimale), `WriteBufferSize = 4096`
- Handshake = None (pas de RTS/CTS)

**`ComTcp`** : client TCP brut, `"TCP:host,port"`.

**`ComRtt`** : client TCP vers `bridge_TcpRtt.py` (RTT par SWD via pyOCD ou J-Link). Si l'hôte est
local, `Open()` lance le bridge (`PythonLauncher`) et attend sa ligne `Listening on`. Voir
[rtt-transport.md](rtt-transport.md).

---

## Couche symboles (Symbol/)

Deux implémentations de `ISymbol` :

| | SymbolMap | SymbolGDB |
|---|---|---|
| Source | Fichier `.map` | ELF via GDB (interpréteur MI) |
| Parsing | Format détecté (GNU ld, `FormatGNU`, lignes adresse / nom) ou regex `Device.MapRegex` | Commandes MI (`-data-evaluate-expression`, `ptype`) |
| Structs | Non | Toute expression C : champs, tableaux, pointeurs, casts |

### SymbolGDB et pont GDB (Symbol/Gdb/)

- `GdbLocator` : GDB configuré, sinon celui embarqué (`tools/gdb`), sinon un GDB du `PATH`.
- `GdbMi` : processus GDB piloté en MI, commandes sérialisées et associées à leur réponse par token.
- `GdbRemoteBridge` : mini gdbserver (RSP) sur 127.0.0.1, port libre. GDB y est **toujours** connecté
  (`target remote`). Lectures/écritures mémoire (`m`/`M`) → `IProtocol.ReadMem`/`WriteMem`, 8 octets max
  par paquet (GDB redemande la suite). Registres à zéro, cible jamais arrêtée.
- Device non connecté (ou protocole sans `ReadMem`) : les lectures échouent → une expression qui passe par
  un pointeur n'est pas résolue (`SymbolData.Resolved = false`) au lieu d'être calculée sur les valeurs
  initiales de l'ELF. Une expression sans pointeur n'accède jamais à la cible.
- `trust-readonly-sections on` : `.text`/`.rodata` lus dans l'ELF.
- Threading : les requêtes mémoire sont servies par `Bridge.Service()` dans `DeviceCtrl.Process()` (thread UI).
  Une requête GDB synchrone faite sur ce thread pompe `Protocol.Process()` pendant l'attente (pas de deadlock).
  Les canaux sont re-résolus en tâche de fond à la connexion du device et au rechargement des symboles.
- Watch : l'expression est réévaluée à chaque rafraîchissement (`SymbolGDB.EvaluateAsync`) : lvalue ≤ 8 octets →
  adresse puis lecture one-shot (éditable) ; sinon valeur formatée par GDB (lecture seule).

### SymbolMap (Symbol/SymbolMap.cs)

`Device.MapRegex` vide (défaut) → format détecté d'après le contenu :

| Format | Reconnu par | Lu |
|---|---|---|
| Map GNU ld complète | ligne `Linker script and memory map` | lignes `0xADDR  nom` de la memory map ; taille = section d'entrée `.bss.nom` / `.data.nom`… à la même adresse (`-fdata-sections`) |
| `FormatGNU` (postbuild PH4 `*_Scope.map`) | 1re ligne `FormatGNU` | lignes `0xADDR  _nom`, nom **sans** le `_` ajouté par le générateur |
| Autre | — | lignes `0xADDR nom` ou `nom 0xADDR` |

Regex personnalisée : groupes `var` et `address` (hex, `0x` facultatif) obligatoires, `size` (octets, décimal ou
`0x…`) facultatif. Regex invalide ou sans ces groupes → détection automatique (message dans le log).
L'ancienne regex par défaut `((?<var>…` (parenthèse non fermée, invalide) est traitée comme vide.

Taille : retenue seulement si 1, 2, 4 ou 8 octets ; sinon (struct, tableau, map sans taille)
`SymbolData.Size = 0` = inconnue → le channel garde sa taille (`ApplySymbol`), l'éditeur de channel ne change
pas le champ, la Watch prend 32 bits. En cas de doublon (static de même nom), le premier l'emporte.

Hot-reload via `FileSystemWatcher` sur le fichier symboles (debounce 300 ms), rechargement fait dans `Process()`.

---

## Cycle de vie Device (Device.cs)

```
Configure(data)
  └─ dispose IProtocol précédent
  └─ loadSymbols() : dispose l'ancien ISymbol, crée le nouveau
        map → met à jour les adresses des channels
        gdb → re-résolution des channels en tâche de fond
  └─ FileSystemWatcher sur le fichier symboles
  └─ crée IProtocol.New(version, comString), abonne FrameComplete / Timeout
  └─ ré-enregistre tous les channels actifs

AddChannel(channel)
  └─ ISymbol.Get(symbole) → adresse/taille
  └─ IProtocol.ReadMemRegister(addr, size) → token (clé des valeurs dans FrameComplete)

Process()  [appelé toutes les 50 ms]
  └─ rechargement des symboles demandé par le watcher
  └─ résultats des re-résolutions en tâche de fond (adresse changée → désenregistre / réenregistre)
  └─ IProtocol.Process()
  └─ SymbolGDB.Bridge.Service(Protocol)
  └─ connexion du device pendant un enregistrement → re-résolution des channels (pointeurs lus sur la cible)
     (hors enregistrement, les lectures de la résolution ouvrent la liaison à la demande)
  └─ Channel.Update() si nouveaux échantillons

OnFrameComplete(lost, frame)
  └─ trames perdues : la dernière valeur est répétée et la perte marquée
  └─ un channel qui démarre s'aligne sur le temps de la trace
OnTimeout()
  └─ interruption marquée sur les channels
```

---

## Boucle de rendu (Plot/Plot.cs)

```
eScopePlot.Refresh()   [50 ms]
  ├─ objet survolé (légende puis objet le plus proche) → sélection
  ├─ détection du sens de déplacement (seuil 5 px), déplacement de l'objet sélectionné
  ├─ throttle : sauté si < 50 ms depuis le dernier rendu
  ├─ UpdateXEnd() / UpdateXLimit()  ← fin affichable selon les buffers, axe X
  ├─ pan X / déplacement Y global
  ├─ Play() si rafraîchissement demandé
  ├─ légendes à la position de la watch line (suit le curseur si watchFollow)
  └─ repaint ScottPlot
```

---

## Modèle de données

```
eScopeModel (eScope.json)
  ├─ Devices[]
  │   └─ Device: Name, Com, Protocol, SymbolType, SymbolFilePath, GdbFilePath, MapRegex,
  │              SamplingTime, V1ChannelMax, V1DataSize
  └─ Traces[]
      └─ Trace: Name, XPos, XWidth, Grid…
          ├─ Channels[]  → Y: List<UInt64>, X: List<double>, Lost[], couleur, taille, shift, script
          ├─ Markers[]
          ├─ Deltas[]
          ├─ Annotations[]
          └─ Watches[]
```

Sérialisation `System.Text.Json` dans `eScope.json` au répertoire courant. Une trace se sauvegarde
seule en `.etrace` (JSON gzip du modèle `Trace`) ; les devices restent dans le workspace, référencés par nom.

---

## Undo/Redo (Command/)

- Deux piles statiques : `_undoStack` / `_redoStack` dans `commandMgr`
- Toute mutation passe par `ExecuteCommand(ICommand)` → `Execute()` → push undo
- Undo : pop undo → `Undo()` → push redo
- Types : `CmdAddObj`, `CmdDeleteObj`, `CompositeCommand`, et pour les données
  `PlotCmdDeleteData`, `ChannelCmdDeleteData`, `PlotCmdPasteData`, `ISignalPlotCmdPasteData`
- `commandMgr.StateChanged` met à jour les labels du menu ("Undo 'Add Channel1'")
- Pilotage par l'API (Automation/) : les commandes sont enveloppées dans `AiCommand` (libellé `AI: ...`) ;
  `CmdConfigureChannel` (état avant / après des réglages d'un channel). Modifier un objet par son éditeur
  n'est pas annulable (#14)

---

## Automation (Automation/)

Serveur local qui expose les mêmes outils en MCP (agents IA) et en REST (scripts). Spec complète :
[automation.md](automation.md) ; guide utilisateur et exemples : `tools/automation/README.md`.

- `AutomationHost` : `HttpListener` sur `http://localhost:<port>/` (pas d'ASP.NET Core : la release est
  framework-dependent), jeton Bearer, clients locaux uniquement, `Origin` contrôlé. `/mcp` : MCP Streamable HTTP
  sans session (un `StreamableHttpServerTransport` + `McpServer` par requête, SDK `ModelContextProtocol.Core`) ;
  `/api/<outil>` : même outil en JSON.
- `ToolRegistry` : catalogue = méthodes `[McpServerTool]` des classes de `Automation/Tools/` ; MCP utilise les
  `McpServerTool` construits à partir d'elles, REST les appelle par réflexion.
- `AutomationService` (fichiers partiels par domaine) : chaque opération s'exécute sur le thread UI par
  `UiDispatcher` ; les attentes longues (`acquire`, lectures one-shot) se font hors UI entre deux étapes.
- `AutomationManager` / `AutomationSettings` : réglages par utilisateur (`%LocalAppData%\eScope\automation.json`),
  démarrage / arrêt du serveur ; `AutomationForm` (Tools > AI automation) ; indicateur « AI » de la barre d'outils
  (`eScope.Automation.cs`).

---

## Modèle de threading

| Thread | Rôle |
|---|---|
| UI thread | Timer 50 ms, `Process()`, callbacks protocole, rendu ScottPlot, sérialisation JSON |
| `ReadCom` task | Lecture du transport → PipeWriter |
| `FetchFrame` task | Lecture PipeReader → extraction trames → ConcurrentQueue |
| Timer de timeout V3 | Lève un drapeau traité dans `Process()` |
| `gdb-mi` | Lecture de la sortie de GDB, complète les requêtes en attente |
| `gdb-bridge` | Serveur RSP : met les accès mémoire en file, servis par `Bridge.Service()` |
| Tâches de fond | Re-résolution des channels (résultats appliqués dans `Process()`) |
| Pool (HttpListener) | Requêtes de l'API d'automation : chaque opération est envoyée au thread UI (`UiDispatcher`) |

Les listes `configTxList` / `configRxList` de V3 ne sont manipulées que sur le thread UI
(`ReadMem`, `ReadMemRegister`, `UpdateConfig`, `Process`) ; `FetchFrame` ne fait qu'alimenter la queue.
