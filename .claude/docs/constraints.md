# Constraints — Ce qu'on ne fait PAS

## Plateforme

**Windows uniquement sur `master`.** Cible `net8.0-windows7.0`.
- WinForms n'est pas cross-platform
- `LicenseInfo` lit `HKEY_LOCAL_MACHINE` (registre Windows)
- `EnvironmentSignals` appelle des API Windows (domain-join, Azure AD)

Un port Linux (WinForms → Avalonia, avec un `eScope.Core` sans dépendance UI) est en cours sur la
branche `worktree-linux-port-core`. Tant qu'il n'est pas fusionné, ne pas introduire de nouvelle
dépendance Windows dans le code modèle / protocole / symboles.

---

## Protocoles dépréciés

### V1 — ne pas modifier
- Streaming brut 16 bits, fenêtres de 1024 samples, 4 canaux max hardcodés
- Pas de sync, pas d'écriture, pas de lecture one-shot
- Code partiellement commenté — ne sert que de référence historique

### V2 — maintenance uniquement
- Sync `[0x55, 0xAA, 0x33]`, compteur de trames, canaux variables
- Pas de lecture one-shot : Watch et pointeurs GDB indisponibles
- Maintenu pour rétro-compatibilité avec anciens firmwares
- **Ne pas y ajouter de fonctionnalités.** Tout développement protocol → V3.

### V3 — compatibilité firmwares publiés
- Le format filaire ne change pas : les firmwares V3 déjà publiés doivent continuer à fonctionner
- Côté PC, respecter les règles d'envoi des lectures one-shot (voir [api.md](api.md#lectures-one-shot-cmd_read_aperiodic)) :
  les anciens firmwares perdent les commandes qui suivent une lecture one-shot

---

## Communication

- **Baudrate** : fixé à la compilation côté firmware (`ESCOPE_PROTOCOL_BAUDRATE`, Arduino `DEBUG_SCOPE_BAUDRATE`).
  Le côté PC est configurable mais le device ne suit pas si on change.
- **Pas de contrôle de flux** : Handshake = None (pas de RTS/CTS)
- **Pas de CRC/checksum** : une payload corrompue n'est pas détectée (seuls sync, footer et `flowId` sont contrôlés)
- **Pas de retry** : une lecture one-shot sans réponse est signalée en échec (`onReadFailed`), pas rejouée
- **Timeout dur** : 1000 ms sans trame (avec des lectures périodiques configurées) → reconfiguration.
  Les devices lents ou lignes bruyantes peuvent déclencher de faux timeouts.
- **Lectures one-shot** : une seule en cours à la fois → environ une par tick de 50 ms. Chaque niveau de
  pointeur résolu par GDB coûte une lecture.
- **TCP firmware** : `eScopeTransportTcp.c` est un stub (pas d'implémentation socket côté cible)

---

## Symboles GDB

- Le pont GDB passe par les lectures one-shot : pointeurs lisibles uniquement en V3, device connecté
- Les canaux à pointeur sont résolus à la connexion et au rechargement des symboles, pas en continu
  (un pointeur qui change ensuite n'est pas suivi sur le plot ; il l'est dans la Watch)
- Pointeur NULL : GDB renvoie `0 + offset` sans erreur, eScope échantillonne alors près de l'adresse 0
- Lectures et écritures limitées à 8 octets (`UInt64`) ; au-delà la Watch affiche la valeur texte de GDB
- Adresses 32 bits sur le fil

---

## Scripts de canaux calculés (Plot/Channel.cs)

- C# compilé avec Roslyn (`Microsoft.CodeAnalysis.CSharp.Scripting`), corps d'une fonction `(int i) => double`
- `Y` = données du canal, `data["nom"]` = autres canaux ; imports `System` et `System.Collections.Generic` seulement
- Lecture **par index** : `data["x"][i]` est l'échantillon `i` de `x`, pas l'instant `i × période`.
  Un canal sans device a sa propre période (`Channel.SamplingTime`, 2 ms par défaut, réglée dans
  l'éditeur) : la régler sur celle des canaux lus, sinon la courbe calculée est décalée dans le temps
- Un échantillon pas encore reçu vaut 0 et la donnée est marquée en attente
- Pas d'I/O, pas de multi-threading dans les scripts

---

## Ce qui n'est pas implémenté

### Acquisition
- Pas d'enregistrement ring-buffer sur disque — tout est en RAM
- Pas de synchronisation multi-devices (les devices tournent indépendamment)

### Analyse
- Analyse limitée à la sélection courante (statistiques, fréquence / rapport cyclique, FFT)
- Pas d'histogramme, pas de décodage de bus ni de déclenchement (trigger)

### Protocole
- Pas d'ACK/NACK — le `flowId` détecte les pertes, mais rien n'est renvoyé
- Une trame plus courte que la config est acceptée : les valeurs manquantes sont absentes de `FrameComplete`

### UI
- Une seule instance d'eScope par machine (mono-document, un seul `eScope.json`)
- Pas de plugin système — les parseurs protocole/symboles sont hardcodés
- Pas de documentation intégrée

### Export
- Pas d'export binaire, MATLAB `.mat`, HDF5, ni format oscilloscope standard
- Export disponible : clipboard (tabulé) via CopyData, fichier CSV (File > Export Trace, historique complet, culture invariante) et image PNG de l'onglet tel qu'affiché
- Une trace se sauvegarde/ouvre seule au format `.etrace` (JSON gzip du modèle `Trace`) ; les devices restent dans le workspace, référencés par nom

---

## Licence — état actuel

Aucune fonctionnalité n'est aujourd'hui bridée par le type de licence. Le système est en place (LicenseManager, LicenceInfo, clé Pro validée par Lemon Squeezy) mais le gating est informatif seulement : une licence expirée ou non validée affiche un rappel, rien n'est bloqué. Ne pas supposer qu'une feature sera cachée derrière une licence sans modifier explicitement le code de gating.
