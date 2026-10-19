# Simulation firmware sur PC — Design

> **Statut : phases 1 à 3 implémentées** (adresses 64 bits, hooks cible et codes d'erreur, map MSVC) ;
> phases 4 (exemple `targetExamples/pcsim/` + host C#) et 5 (tutoriels) à faire.
> Issue [#1](https://github.com/laurentolive/eScope/issues/1).
>
> Écarts avec le design ci-dessous :
> - une adresse qui ne tient pas sur 4 octets n'est pas refusée par `ProtocolV1/V2/V3` : seuls les 32 bits
>   de poids faible sont envoyés (comportement antérieur, AVR `0x80xxxx`) ;
> - `ERR_ADDR` sur une lecture périodique : le canal est retiré de la config (pas de nouvel essai), son
>   absence apparaît en échantillons perdus et dans le log ; pas encore d'état « adresse invalide » dans le
>   panneau latéral ;
> - `ERR_ADDRSIZE` : réarmé aussi à la réouverture de la liaison (firmware corrigé pris en compte sans
>   éditer le device).
>
> Vérifié : `sim_v3.py --addr-size 4|8` (4 combinaisons PC / cible, `ERR_ADDR`, `ERR_ADDRSIZE`), eScope de
> bout en bout via l'API d'automation (lecture / écriture / acquisition à 0x1_8002_00xx, lecture refusée en
> ~130 ms, map MSVC), compilation du C V3 / V3Id en 4 et 8 octets (clang, `-Wall -Wextra`). Non vérifié :
> exécution du C sur PC (pas de toolchain C complète sur la machine de dev), cibles réelles NUCLEO / RA4E1.

## Objectif

Visualiser dans eScope les variables d'un **firmware exécuté sur PC** comme sur une cible réelle : même
workspace, mêmes traces, mêmes symboles, sans hardware.

Contexte : un simulateur C# charge le firmware compilé en **DLL**, le schedule et simule le hardware
(UART, GPIO, interruptions…). La DLL peut être compilée avec **GCC (MinGW)** ou **MSVC** : les deux
chaînes ne diffèrent que par les symboles et la base de link ; le transport et le protocole sont communs.

Principe directeur : **eScope ne sait pas qu'il parle à une simulation.** Pour lui c'est une cible V3 sur
TCP, avec une taille d'adresse donnée. Tout ce qui est propre au PC (relocation de la DLL, validité des
adresses) est traité côté cible, dans eScopeProtocol, par des hooks de `eScopeCfg.h`.

---

## Architecture

```
eScope (PC)                         simulateur C# (process séparé)
───────────                         ───────────────────────────────────────────────
Device  Protocol V3, AddressSize 8  ┌ relais TCP ⇄ UART simulée (TcpListener :47700)
  │                                 │     │ octets reçus injectés dans le contexte du scheduler
ComTcp  "TCP:127.0.0.1,47700" ──────┘     ▼   (dans le driver UART simulé)
                                    UART simulée ⇄ firmware.dll (GCC ou MSVC)
                                                   eScopeTransportUart (inchangé)
                                                   eScopeProtocolV3 + hooks PC :
                                                     ESCOPE_ADDR_TO_PTR   (relocation)
                                                     ESCOPE_ADDR_IS_VALID (sanity check)
                                                   eScopeProtocol_Process() appelé là où
                                                   l'utilisateur veut échantillonner
```

Ce qui ne change pas : `ComTcp`, la détection de trames, le flow counter, la base de temps
(`Device.SamplingTime`), les traces, le Watch, l'automation.

---

## 1. Transport : TCP sous l'UART simulée

Le firmware garde son transport UART (`ESCOPE_TRANSPORT_UART`). Le relais vers un socket TCP serveur est
fait **dans la couche driver UART du simulateur** : l'UART simulée sur laquelle tourne eScopeProtocol a
pour « fil » un socket au lieu d'un modèle de périphérique. eScope s'y connecte avec `ComTcp`
(`TCP:127.0.0.1,<port>`), comme sur un pont Wi-Fi. Ni le firmware ni eScope ne voient la différence.

Spécification du relais (driver UART du simulateur C#) :

| Point | Choix |
|---|---|
| Socket | `TcpListener` sur `127.0.0.1:<port>` (47700 par défaut, comme `sim_v3.py`), un seul client ; un nouveau client remplace l'ancien |
| PC → firmware | octets reçus mis en file, **injectés dans l'UART simulée par le scheduler** (même contexte que les autres interruptions simulées) : la simulation reste déterministe, `CRITICAL_SECTION_*` peut rester vide |
| Firmware → PC | octets émis par l'UART simulée envoyés au client (bufferisés, envoyés par le thread socket) ; sans client : jetés, comme une ligne série débranchée |
| Débit | option : débit illimité, ou limité au baudrate simulé pour être représentatif de la cible |
| Déconnexion | aucun effet sur le firmware (ligne au repos) ; eScope se reconnecte via `ComOpener` |

`eScopeTransportTcp.c` (stub, mode client) n'est **pas** nécessaire ; un mode serveur ne servirait qu'à un
firmware PC autonome sans simulateur (hors périmètre, cf. Évolutions).

---

## 2. Adressage : taille d'adresse selon l'archi cible

On ne passe pas 64 bits sur le fil pour une cible 32 bits ou moins. La taille d'adresse filaire est
**celle de la cible** : 4 octets (défaut, toutes les cibles actuelles), 8 octets pour une DLL x64. Une DLL
compilée en x86 garde 4 octets.

### Format filaire V3 (étendu, rétrocompatible)

```
[CMD][SIZE][ADDR_0 … ADDR_{n-1}]        n = 4 ou 8, little-endian
CMD_SIZE = 2 + n                        (6 aujourd'hui, 10 en 64 bits)
```

Les commandes à adresse 64 bits portent le flag **`ADDR64_FLAG = 0x10`** dans l'octet commande :

| Commande | 32 bits | 64 bits |
|---|---|---|
| `CMD_READ_PERIODIC` | `0x43` | `0x53` |
| `CMD_READ_APERIODIC` | `0x44` | `0x54` |
| `CMD_WRITE_APERIODIC` | `0x25` | `0x35` |

Rien d'autre ne change (trames, en-tête `0x43` des trames de données, ordre des lectures). En 32 bits le
format est identique octet pour octet : firmwares et workspaces existants inchangés.

### Incohérence de taille : erreur, pas d'identification

Pas de commande d'identification : la taille est configurée des deux côtés, et c'est le flag qui rend une
incohérence **détectable dès la première commande**, sans désaligner le parse. Une cible qui reçoit une
commande lecture / écriture dont la taille d'adresse (flag) n'est pas la sienne répond l'erreur
**`ERR_ADDRSIZE`** (§4), avec sa propre taille en payload. Sans le flag, des adresses 64 bits envoyées à une
cible 32 bits seraient lues comme des commandes (l'octet `0x01` d'une adresse `0x1_8000_5A40` serait pris
pour `CMD_DEINIT`).

Firmwares publiés avant cette évolution : `0x53` / `0x54` / `0x35` sont des commandes inconnues, ignorées →
pas de trame, `Timeout` côté eScope (pas de message explicite, mais pas de comportement dangereux).

### Côté cible (`eScopeProtocolV3.c`, `eScopeProtocolV3Id.c`)

- `ESCOPE_PROTOCOL_ADDRESS_SIZE` (déjà obligatoire dans `eScopeCfg.h`, aujourd'hui utilisé seulement pour
  la taille du buffer de config) pilote réellement le format : `CMD_SIZE` et `CMD_DATA_IDX` en dérivent,
  l'adresse est décodée par une boucle sur `ESCOPE_PROTOCOL_ADDRESS_SIZE` octets dans un `uint64_t`
  (`uint32_t` si 4).
- Au parse : `ADDR64_FLAG` présent ⇔ `ESCOPE_PROTOCOL_ADDRESS_SIZE == 8`, sinon `ERR_ADDRSIZE`.
- Le cast `(uint8_t*)((uint32_t)…)` (V3.c l. 163 et 221) devient `ESCOPE_ADDR_TO_PTR(addr)` (§3), dont la
  forme par défaut passe par `uintptr_t` : plus de troncature sur x64.
- Valeurs acceptées : 4 ou 8 (`#error` sinon).
- `arduino/DebugScope.cpp` : reste en 4 octets (cibles AVR / ARM), non concerné.

### Côté eScope

| Élément | Changement |
|---|---|
| `Device` (json) | nouvelle propriété `AddressSize` (octets, 4 par défaut ; 8 possible en V3) ; workspaces existants : 4 |
| `DeviceEdit` | choix « Adresses : 32 bits / 64 bits », actif en V3 seulement |
| `IProtocol` | adresses en `UInt64` (`ReadMem`, `WriteMem`, `ReadMemRegister`) |
| `ProtocolV3` | `AddressSize` passé au constructeur (`IProtocol.New`) ; `CMD_SIZE = 2 + AddressSize` ; `ADDR64_FLAG` si 8 ; sérialisation sur n octets ; une adresse qui ne tient pas sur n octets est refusée (log, `onReadFailed`) ; codes d'erreur traités (§4) |
| `ProtocolV1` / `V2` | gardent 32 bits ; adresse > `0xFFFFFFFF` refusée (log) |
| `Channel.Address`, `Watch.Data.Address` | `UInt64` (lecture json compatible : un nombre 32 bits se relit en 64) |
| `utils.strToAddress` / `AddressToStr`, `ChannelEdit` (validation), `AutomationService.Target` | `UInt64` |
| `GdbRemoteBridge` | `MemRequest.Address` en `UInt64`, plus de troncature dans `TryParseRange` (la troncature AVR 0x80xxxx est faite par `ProtocolV3` selon `AddressSize`) |
| `SymbolMap` | `ParseHex` en `long` pour les adresses (aujourd'hui `int` : une adresse > 31 bits vaut 0 et fausse l'appariement section / symbole) |

---

## 3. Relocation : traduite par la cible

eScope envoie l'adresse **telle que donnée par les symboles** (adresse de link). La DLL, chargée par le
simulateur, peut être relogée (ASLR, conflit de base) : c'est le **code eScopeProtocol côté cible** qui
traduit l'adresse filaire en pointeur, via un hook de `eScopeCfg.h` utilisé pour toutes les lectures
(périodiques, one-shot) et écritures :

```c
/* eScopeProtocolV3.h — défaut : identité (cibles embarquées) */
#ifndef ESCOPE_ADDR_TO_PTR
    #define ESCOPE_ADDR_TO_PTR(addr)  ((uint8_t*)(uintptr_t)(addr))
#endif
```

### Simulation Windows (MSVC et MinGW)

```c
/* eScopeCfg.h du firmware simulé */
#include <stdint.h>
extern char __ImageBase;                          /* base de chargement réelle (MSVC et MinGW) */
#define ESCOPE_LINK_BASE  0x180000000ULL          /* base de link, lue dans le .map */
#define ESCOPE_ADDR_TO_PTR(addr) \
    ((uint8_t*)((uintptr_t)&__ImageBase + (uintptr_t)((addr) - ESCOPE_LINK_BASE)))
```

`ESCOPE_LINK_BASE` est la base préférée du link, qu'on trouve dans le map :

| Chaîne | Où dans le map | Défaut usuel |
|---|---|---|
| MSVC | ligne `Preferred load address is 0000000180000000` | DLL x64 `0x180000000`, DLL x86 `0x10000000` ; fixable par `/BASE:` |
| MinGW (ld) | symbole `__image_base__` | variable selon la version de binutils (auto-image-base) ; fixable par `-Wl,--image-base=` |

Recommandation : fixer la base au link (`/BASE:` ou `--image-base=`) et la reprendre dans `eScopeCfg.h`,
pour qu'elle ne dérive pas d'un build à l'autre. Avec `/DYNAMICBASE:NO`, la DLL est le plus souvent chargée
à sa base préférée (décalage nul), mais rien ne le garantit : le hook reste nécessaire.

L'addition est faite à chaque accès (une addition par lecture, négligeable). Le buffer de config garde
l'adresse filaire : il reste valide après un rechargement de la DLL à une autre base.

---

## 4. Sanity check des adresses

En simulation, une adresse invalide fait planter le process du simulateur (pas de bus fault « inoffensif »
comme via SWD). Hook optionnel de `eScopeCfg.h` :

```c
/* défaut : pas de contrôle */
#ifndef ESCOPE_ADDR_IS_VALID
    #define ESCOPE_ADDR_IS_VALID(ptr, size)  (true)
#endif
```

Implémentation PC proposée (dans l'exemple) : l'accès doit tomber dans l'image de la DLL — plage
`[__ImageBase, __ImageBase + SizeOfImage[` lue dans l'en-tête PE — ou, plus large, `VirtualQuery` (page
commise, lisible / inscriptible). Sur cible réelle le même hook peut contrôler les plages RAM / flash
(symboles du linker script).

### Quand

Une seule fois, **au parse** de la commande (lecture périodique, lecture one-shot, écriture), pas à
chaque échantillon : coût nul en régime établi.

### Comportement : code d'erreur

Une commande (lecture périodique / one-shot, écriture) dont l'adresse est refusée n'est pas exécutée : la
cible passe en erreur par le mécanisme existant (`eScopeProtocol_Error`, comme `ERR_TXOVF` /
`ERR_CFGOVF`) : réception en cours abandonnée, trame d'erreur répétée à chaque tick jusqu'au prochain
`CMD_CLEAR`.

### Codes d'erreur ajoutés

```
ERR_ADDR      0x88   ERR_FLAG | 0x08   adresse refusée par ESCOPE_ADDR_IS_VALID
ERR_ADDRSIZE  0x8A   ERR_FLAG | 0x0A   taille d'adresse de la commande ≠ celle de la cible (§2)
                                       (0x89 = ERR_BWOVF, Arduino)
```

Les trames d'erreur actuelles ont `dataSize = 0`. Ces deux codes ajoutent un payload pour qu'eScope sache
quoi corriger (le format de trame le permet déjà, `TryReadFrame` lit `dataSize` octets quel que soit le
code) :

```
ERR_ADDR      55  flowId  n+1  88  [ADDR_0 … ADDR_{n-1}]  [SIZE]  AA    commande fautive
ERR_ADDRSIZE  55  flowId  1    8A  [ESCOPE_PROTOCOL_ADDRESS_SIZE]  AA   taille attendue par la cible
```

### Côté eScope

Aujourd'hui une trame d'erreur est seulement loguée : les trames de données s'arrêtent, le `Timeout` (1 s)
reconfigure à l'identique, et l'erreur revient en boucle. `ProtocolV3` traite désormais les codes d'erreur :

- **Base de temps** : chaque trame d'erreur occupe un tick de la cible → comptée comme **un échantillon
  perdu** (`FrameComplete` avec un dictionnaire vide → `AddLost(1)`) : l'axe de temps reste aligné, le
  trou est visible sur les canaux.
- **`ERR_ADDR`** : la commande fautive (adresse + taille du payload) est retirée de la config ; lecture
  one-shot → `onReadFailed` (Watch / GDB / automation voient un échec, pas une valeur) ; écriture →
  abandonnée, loguée ; lecture périodique → canal désenregistré et marqué « adresse invalide » dans le
  panneau latéral (comme un symbole non résolu). Puis `Reconfigure` (`CMD_CLEAR` + le reste de la config) :
  les autres canaux repartent.
- **`ERR_ADDRSIZE`** : erreur de configuration du device → message à l'utilisateur (« la cible attend des
  adresses de n octets »), plus de reconfiguration automatique (sinon boucle) jusqu'à modification du
  device ou nouveau démarrage de l'acquisition.
- **`ERR_TXOVF` / `ERR_CFGOVF`** : même remontée à l'utilisateur (aujourd'hui seulement dans le log) ;
  hors périmètre de cette évolution au-delà du message.
- Événement `IProtocol.Error(code, payload)` levé sur le thread de `Process()`, comme `Timeout`.

Compatibilité : un firmware sans le hook ne produit jamais `ERR_ADDR`. Un eScope actuel recevant ces codes
les logue comme les autres erreurs (comportement actuel).

---

## 5. Base de temps : pas d'horodatage

Comme sur cible, c'est la **cadence** du flux qui fait office de base de temps : une trame par appel de
`eScopeProtocol_Process()`, `Device.SamplingTime` = période d'appel en temps simulé. L'axe X d'eScope est
donc en temps simulé, quelle que soit la vitesse de la simulation.

- Simulation plus rapide que le temps réel : plus de trames par seconde, absorbées par la file de trames de
  `ProtocolV3` (limite : débit du socket et du rendu) ; option « baudrate simulé » du relais pour rester
  représentatif.
- Simulation en pause (breakpoint Visual Studio) : plus de trames → `Timeout` au bout de 1 s, signalé sur
  les canaux, reconfiguration à la reprise. C'est le comportement voulu : une perte de com est signalée.

## 6. Point d'échantillonnage

Choisi par l'utilisateur, comme sur cible : il appelle `eScopeProtocol_Process()` où il veut dans son code
(tick du scheduler, interruption timer simulée, fin de boucle de régulation…). Les callbacks du transport
UART simulé arrivent dans le contexte du scheduler (§1).

---

## 7. Symboles

### GCC (MinGW)

- **Map** : `-Wl,-Map=firmware.map`. Le map ld d'une cible PE a la même structure (« Linker script and
  memory map ») : `SymbolMap.ParseGnu` s'applique, une fois les adresses lues en 64 bits (§2). En x86 les
  symboles C ont un `_` en tête (à retirer comme pour `FormatGNU`) ; pas en x64.
- **GDB** : `-g` (DWARF) ; le `gdb-multiarch` fourni lit un PE x86 / x64 avec DWARF (`file firmware.dll`).
  Les adresses données par GDB sont les adresses de link : la relocation (§3) s'applique. Les pointeurs
  sont lus via `GdbRemoteBridge` (adresses 64 bits, §2). Le `g` court du bridge reste valable (amd64 :
  le reste est demandé par `p`).

### MSVC

Pas de DWARF : `SymbolGDB` inutilisable avec `cl`.

1. **Map MSVC** (`/MAP`), phase 1 : nouveau format détecté dans `SymbolMap` (ligne
   `Preferred load address is`). Sections « Publics by Value » et « Static symbols » :
   ```
    0003:00000a40       gMotorSpeed                0000000180005a40     motor.obj
   ```
   nom = 2e colonne, adresse = colonne `Rva+Base`. Noms C++ décorés (`?…`) ignorés ; `_` de tête retiré en
   x86. Pas de taille dans le map : `Size = 0`, l'utilisateur la garde (comme le format générique).
2. **PDB** (`SymbolPdb`, phase 2, optionnel) : `dbghelp` hors process (`SymLoadModuleEx` sur la DLL à sa
   base préférée), `SymFromName` → adresse et taille, `SymGetTypeInfo` → types et membres de structures
   (complétion `a.b`, comme GDB, sans déréférencement de pointeurs). Windows seulement : isolé derrière
   `ISymbol`, absent du portage Linux.
3. Alternative sans code : compiler la DLL avec `clang-cl -gdwarf` → chaîne GDB.

---

## Plan d'implémentation

| Phase | Contenu | Validation |
|---|---|---|
| 1 | eScope : adresses `UInt64`, `Device.AddressSize`, format V3 sur n octets + `ADDR64_FLAG`, `SymbolMap` 64 bits | `sim_v3.py --addr-size 8` (nouvelle option : base RAM au-delà de 4 Go) ; non-régression `--addr-size 4` et cibles réelles |
| 2 | Cible : `ESCOPE_PROTOCOL_ADDRESS_SIZE` effectif, `ADDR64_FLAG`, `ESCOPE_ADDR_TO_PTR`, `ESCOPE_ADDR_IS_VALID`, `ERR_ADDR` / `ERR_ADDRSIZE` (V3 et V3Id) ; eScope : traitement des codes d'erreur | `sim_v3.py` émet `ERR_ADDR` hors RAM et `ERR_ADDRSIZE` sur taille incohérente ; NUCLEO / RA4E1 inchangés |
| 3 | Symboles : map MSVC | maps MSVC x86 / x64 de l'exemple |
| 4 | Exemple `targetExamples/pcsim/` : firmware C (build MSVC et MinGW, DLL x86 et x64), `eScopeCfg.h` PC, petit host C# (scheduler 1 ms + driver UART simulé relayé en TCP), workspace | les 4 combinaisons chaîne × archi : lecture, écriture, Watch, adresse invalide, relocation forcée (`/DYNAMICBASE` + base occupée) |
| 5 | Docs : `api.md` (format filaire, `ERR_ADDR`, `IProtocol`), `architecture.md`, tutoriel FR / EN | — |
| option | `SymbolPdb` (dbghelp) ; `eScopeTransportTcp.c` serveur pour un firmware PC autonome | — |

Le portage Linux (`worktree-linux-port-core`) touche les mêmes fichiers (`IProtocol`, `ProtocolV3`,
`Device`) : la phase 1 est à reporter sur cette branche.

## Décisions

- Adresse refusée → **code d'erreur** `ERR_ADDR` (mécanisme d'erreur existant, collant jusqu'au
  `CMD_CLEAR`), payload = commande fautive.
- **Pas de commande d'identification** : incohérence de taille d'adresse détectée par `ADDR64_FLAG`,
  signalée par `ERR_ADDRSIZE`.
- Relais UART ⇄ TCP **dans la couche driver UART du simulateur**.
