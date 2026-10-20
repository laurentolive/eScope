# eScope

[English](README.md) | **Français**

Un oscilloscope logiciel pour les développeurs embarqués : eScope lit les variables directement dans
la RAM d'un microcontrôleur pendant qu'il tourne et les trace en temps réel, sans arrêter le programme
ni sortir un oscilloscope.

Application Windows (WinForms, .NET 8). Cibles testées : Arduino (AVR), STM32, Renesas RA et, plus
généralement, tout MCU capable d'embarquer le petit module firmware fourni.

## Principe

```
 Fichier de symboles (.elf / .map)       Microcontrôleur
          │                                    ▲
          ▼                                    │ adresses + tailles
   eScope : nom de variable ──► adresse ──► protocole eScope (série, TCP, RTT)
          ▲                                    │
          └──────── valeurs brutes ◄───────────┘
```

Le firmware est passif : il ne sait pas quelles variables sont observées. eScope traduit les noms en
adresses grâce au fichier de symboles et envoie la liste au MCU, qui renvoie périodiquement le contenu
brut de ces zones mémoire.

## Fonctionnalités

### Acquisition
- **Plusieurs devices** à la fois, chacun avec sa liaison et son fichier de symboles.
- **Liaisons** : série / série Bluetooth, TCP, SEGGER RTT par une sonde SWD
  (bridge `bridge_TcpRtt.py`, pyOCD ou J-Link).
- **Détection des pertes** : les trames manquantes et les silences du device sont marqués sur le tracé.
- **Rechargement à chaud** du fichier de symboles après une recompilation.

### Symboles
- **Fichier `.map`** : format détecté (map GNU ld avec tailles, lignes adresse / nom), ou expression régulière personnalisée.
- **ELF par GDB** (`gdb-multiarch` livré dans `tools/gdb`) : toute expression C, par exemple
  `ctx.cfg.gain`, `tab[3].x`, `*(uint16_t*)&reg` ou `uart->Instance->BRR`.
  - Les pointeurs sont lus **sur la cible** au moment de la résolution : GDB est relié à un
    gdbserver intégré qui passe par le protocole eScope (V3).
  - Complétion des noms de variables et des champs de structures.

### Tracé (onglets de traces)
- Courbes en couleur, un axe Y par channel, gain / offset, channels liés qui se déplacent ensemble.
- Zoom, déplacement, zoom sur la sélection, zoom global ; légende redimensionnable.
- **Marqueurs**, **deltas** (mesure d'intervalle), **annotations** texte, watch line.
- **Channels calculés** définis par un script C# sur les autres channels.
- Couper / copier / coller des données (y compris depuis Excel), annuler / rétablir.
- Sauvegarde des traces (compressée), export **CSV** et **PNG**.

### Analyse (touche A)
Sur la sélection, pour chaque channel : min, max, crête à crête, moyenne, RMS, écart-type,
fréquence, période, rapport cyclique (détection de fronts avec hystérésis) et spectre **FFT**
(linéaire ou dB).

### Watch (touche W)
Une fenêtre de valeurs courantes rafraîchie à 1 Hz, avec un format d'affichage (décimal, hexa,
float…) et l'**écriture** de variables sur la cible. Avec les symboles GDB, chaque ligne est une
expression C réévaluée à chaque rafraîchissement : les pointeurs sont suivis en direct, et une
expression sans adresse (`a * 2 + b`, une structure, un champ de bits) est calculée par GDB.

### Pilotage par IA (Tools > AI automation)
Un serveur local, désactivé par défaut, permet à un agent IA (Claude Code, Cursor, VS Code… par **MCP**)
ou à un script (**REST**) de piloter la fenêtre eScope ouverte : ajouter des channels, acquérir, analyser
(statistiques, fréquence, FFT, fronts), annoter, faire des captures, exporter, lire et écrire des variables
de la cible. Les modifications sont annulables (Ctrl+Z), l'écriture sur la cible demande une option
explicite, et les requêtes demandent un jeton. Guide, client Python, simulateur de device protocole V3 et
exemple complet : [tools/automation](tools/automation/README.fr.md).

## Protocoles

| Version | Usage |
|---|---|
| V1 | Historique : nombre de channels et taille des données fixes |
| V2 | Adresse et taille par channel (maintenance uniquement) |
| **V3** | Recommandé : lectures périodiques, lectures et écritures ponctuelles, effacement |

Le format filaire est décrit dans [.claude/docs/api.md](.claude/docs/api.md).

## Intégrer eScope dans un firmware

Le code de référence est dans [targetExamples/](targetExamples/) :

- `eScopeProtocol/` : protocole (V1, V2, V3, V3Id) ;
- `eScopeTransport/` : transport (UART, TCP, RTT).

Les deux se choisissent à la compilation dans un `eScopeCfg.h` propre au projet :

```c
#define ESCOPE_PROTOCOL_V3
#define ESCOPE_TRANSPORT_UART
#define ESCOPE_PROTOCOL_BAUDRATE      115200
#define ESCOPE_PROTOCOL_CHAN_MAX      (8U)
#define ESCOPE_PROTOCOL_ADDRESS_SIZE  (4U)
#define ESCOPE_PROTOCOL_DATASIZE_SIZE (4U)
#define ESCOPE_PROTOCOL_TX_SIZE       (128U)
```

Appeler ensuite `eScopeProtocol_Init()` au démarrage et `eScopeProtocol_Process()` à période fixe
(cette période est la période d'échantillonnage). Exemples complets :

- `arduino/` : bibliothèque `DebugScope` prête à l'emploi ;
- `stm32/` : projet STM32CubeIDE (NUCLEO-C051C8), UART (port COM virtuel du ST-LINK ou pont USART1) ou RTT ;
- `ra/` : Renesas RA4E1, RTT ou UART (SCI9) ;
- `pcsim/` : firmware simulé sur PC, compilé en DLL avec GCC et exécuté par un simulateur dont l'UART
  est relayée en TCP (adresses 64 bits).

Les liaisons sans fil passent par un pont série ESP32 : [`BluetoothSerialBridge/`](BluetoothSerialBridge/)
(port série Bluetooth) et [`WifiTcpBridge/`](WifiTcpBridge/) (Wi-Fi TCP).

Tutoriels pas à pas, avec schémas de câblage et captures d'écran, pour chaque carte et chaque liaison :
[docs/tutoriels](docs/tutoriels/README.md).

## Compiler et lancer

```powershell
dotnet build eScope.sln
dotnet run --project eScope/eScope.csproj
```

Le GDB livré se télécharge avec `powershell -ExecutionPolicy Bypass -File tools\fetch-gdb.ps1`
(build MSYS2 portable, copiée à côté d'`eScope.exe`).

## Démarrage rapide

1. Compiler le firmware avec le module eScope et récupérer le fichier `.elf` (ou `.map`).
2. Dans eScope, déclarer un **device** : liaison, protocole, fichier de symboles.
3. Ajouter des **channels** en tapant des noms de variables (ou des expressions).
4. **Enregistrer** (Ctrl+Espace) : les courbes défilent en temps réel.
5. Mesurer avec les marqueurs, les deltas et l'analyseur ; suivre les valeurs dans la fenêtre Watch.
6. L'espace de travail est sauvegardé dans un fichier `.escope` (à rouvrir par *Files > Open...* ou en le déposant sur la fenêtre).

## Licence

L'application PC eScope est à source disponible sous la [PolyForm Noncommercial License 1.0.0](LICENSE.md) :

- **Gratuite pour un usage non commercial** : projets personnels, loisirs, études, recherche, et usage par des établissements d'enseignement, de recherche publique, caritatifs ou publics.
- **Un usage commercial demande une licence Professional** (travail, entreprise, projets clients) : elle s'achète depuis le menu *About > License* d'eScope.

Le côté firmware est du **code d'exemple sous [licence MIT](targetExamples/LICENSE.md)** : `targetExamples/` (eScopeProtocol, eScopeTransport, bibliothèque Arduino, code d'intégration des projets d'exemple) et [`BluetoothSerialBridge/`](BluetoothSerialBridge/LICENSE.md). Copiez-le dans votre firmware et livrez-le dans vos produits, commerciaux compris, sans licence eScope.

Les fichiers tiers gardent leur propre licence : les sources SEGGER RTT et les drivers ST / Renesas de `targetExamples/`, et GDB dans `eScope/tools/gdb` (GPL, voir `SOURCES.txt`).

Seul le texte anglais de [LICENSE.md](LICENSE.md) fait foi ; ce résumé est donné à titre indicatif.

## Documentation

- [Tutoriels](docs/tutoriels/README.md) : déployer et utiliser eScope sur Arduino Nano, STM32 NUCLEO-C051C8 et
  Renesas FPB-RA4E1 en série, RTT, Bluetooth et Wi-Fi / TCP, et firmware simulé sur PC
- [Pilotage par IA / script](tools/automation/README.fr.md) : API d'automatisation (MCP + REST)

Documents de conception (en français) :

- [Produit](.claude/docs/product.md) : vision, fonctionnalités, éditions
- [Architecture](.claude/docs/architecture.md) : threading, flux de données, symboles
- [API](.claude/docs/api.md) : interfaces internes et format filaire
- [Contraintes](.claude/docs/constraints.md) : limites et choix assumés
- [Automatisation](.claude/docs/automation.md) : spécification complète de l'API MCP + REST
