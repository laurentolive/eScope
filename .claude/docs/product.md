# Product — Vision & Objectifs

## Ce que fait eScope

eScope est un oscilloscope logiciel pour développeurs embarqués. Il se connecte à un microcontrôleur (Arduino, STM32, Renesas RA, ESP32, SAMD…) par liaison série / Bluetooth, TCP ou SEGGER RTT, lit des variables directement en RAM pendant l'exécution, et les affiche en courbes temps-réel.

**Problème résolu** : inspecter l'état de variables en live sans interrompre l'exécution ni sortir un oscilloscope matériel. La question centrale est "quelle est la valeur de ma variable *maintenant* ?"

## Flux d'utilisation typique

1. Compiler le firmware avec le module eScope et récupérer le fichier symboles (`.elf` lu par GDB, ou `.map`)
2. Déclarer un device dans eScope : transport, protocole, fichier symboles
3. Créer des Channels en tapant le nom (ou une expression C) des variables, avec complétion
4. Cliquer Record → les variables remontent en temps réel
5. Analyser avec marqueurs, deltas, zoom/pan et l'analyseur
6. Consulter la Watch window pour les valeurs live, et modifier des variables
7. Sauvegarder le workspace (`.escope`) ou une trace seule (`.etrace`) ; les rouvrir par *Files > Open...* ou glisser-déposer

## Fonctionnalités principales

### Acquisition
- Plusieurs devices simultanés, chacun avec son transport et son fichier symboles
- Transports : série / Bluetooth série, TCP, SEGGER RTT par sonde SWD (pyOCD ou J-Link)
- Pertes de trames et silences du device détectés et marqués sur la courbe
- Rechargement à chaud du fichier symboles après recompilation

### Symboles
- Fichier `.map` : format détecté (map GNU ld avec tailles, `*_Scope.map` du postbuild, lignes adresse / nom) ou regex configurable
- ELF via GDB (GDB embarqué) : toute expression C (`ctx.cfg.gain`, `tab[3].x`, `uart->Instance->BRR`, casts),
  pointeurs lus sur la cible ; complétion des variables et des champs de structures

### Système de traces (onglets)
- Plusieurs onglets indépendants, chacun est un workspace de plot
- Channels colorés par trace, chacun avec son axe Y, gain / offset, canaux liés
- Channels calculés par script C# à partir des autres
- Position et zoom (XPos / XWidth) persistés dans le workspace
- Couper / copier / coller de données (y compris depuis Excel), undo / redo
- Sauvegarde / ouverture d'une trace (`.etrace`), export CSV et PNG

### Enregistrement & navigation
- Record (Ctrl+Space) : bascule l'acquisition
- Play/Pause : relecture des données enregistrées
- Navigation : Début/Fin/Avant/Arrière, zoom out / fit / sélection, raccourcis clavier (B/E/F/O/Z…)

### Annotations sur le plot
- **Marker** : ligne verticale sur le plot
- **Delta** : span horizontal pour mesurer un intervalle de temps
- **Annotation** : texte ancré à des coordonnées
- **Watch line** : marqueur vertical suivant le curseur, valeurs affichées dans la légende

### Analyseur (touche A)
- Sur la sélection, par canal : min, max, crête à crête, moyenne, RMS, écart-type
- Fréquence, période, rapport cyclique (fronts avec hystérésis)
- Spectre FFT (fenêtre de Hann, linéaire ou dB), copie du tableau

### Watch window (touche W)
- Grille flottante affichant les valeurs live
- Colonnes : Expression (`Device.expr`, préfixe facultatif avec un seul device) / Address / Display format / Value
- Édition en place (double-clic, Entrée ou F2) : expression, format (combo), valeur (écriture sur la cible) ;
  double-clic sur la ligne « add watch » pour ajouter ; glisser-déposer pour réordonner, Suppr pour supprimer
- Complétion des symboles pendant la saisie de l'expression (liste sous la cellule, Ctrl+Espace pour l'ouvrir,
  ↑/↓ puis Entrée/Tab ou clic pour choisir ; avec GDB, membres proposés après `.` / `->`)
- Rafraîchissement à 1 Hz (lectures apériodiques)
- Avec les symboles GDB : l'expression est une expression C quelconque (`ctx->cfg->gain`, `tab[3].x`,
  `*(uint16_t*)&reg`, `a * 2 + b`), réévaluée à chaque rafraîchissement (pointeurs suivis en direct) ;
  une expression sans adresse ou de plus de 8 octets est affichée telle que calculée par GDB (lecture seule)
- Différent du plot : pas d'historique, juste la valeur courante

### Différences Watch vs Plot

| | Watch window | Plot |
|---|---|---|
| But | Valeur courante | Historique temporel |
| Taux | 1 Hz | Période d'échantillonnage du firmware ; affichage rafraîchi toutes les 50 ms |
| Lecture protocole | `CMD_READ_APERIODIC` | `CMD_READ_PERIODIC` |
| Affichage | DataGridView | ScottPlot 5.0 |

### Pilotage par IA / scripts (Tools > AI automation)
- Serveur local désactivé par défaut : un agent IA (Claude Code, Cursor, VS Code… par MCP) ou un script (REST)
  pilote la fenêtre eScope ouverte ; l'utilisateur voit ce qu'il fait (indicateur « AI » dans la barre d'outils)
- 46 outils : workspace et traces, symboles, channels (dont calculés par script), acquisition
  (`acquire` : enregistre N ms puis arrête), données, analyse (statistiques, fréquence, FFT, fronts), markers /
  deltas / annotations, vue, capture d'écran (l'IA voit le plot), exports CSV / PNG / `.etrace`, lecture et écriture
  de variables sur la cible (pendant un enregistrement)
- Modifications annulables par l'utilisateur (`AI: ...` dans Undo) ; écriture sur la cible soumise à une option
- Toutes les éditions ; jeton d'accès, clients locaux uniquement
- Guide : `tools/automation/README.md` (client Python, simulateur de device V3, exemple de bout en bout) ;
  spec : [automation.md](automation.md)

## Éditions & licences

Trois types (License/) — aucun n'est bridé fonctionnellement aujourd'hui, la licence est informative :

| Édition | Durée | Restriction |
|---|---|---|
| Démonstration | 1 mois | Expiration automatique |
| Personnelle | Perpétuelle | Non-commercial ; bloquée si machine domain-joined ou Azure AD |
| Professionnelle | Abonnement annuel (Lemon Squeezy) | Aucune |

La vérification domain-join se fait via `EnvironmentSignals.IsDomainJoined` / `IsAzureAdJoined` : sur machine d'entreprise, choisir la Personnelle affiche un avertissement (OK continue, Annuler revient au choix), puis un rappel non bloquant à chaque démarrage tant que la licence est Personnelle.

### Licence Professionnelle — Lemon Squeezy

Pas de serveur maison : Lemon Squeezy (Merchant of Record) gère l'achat, la TVA, le renouvellement de l'abonnement et génère la clé. eScope appelle la License API publique (`License/LemonSqueezy.cs`, sans clé d'API) :

- **activate** (`license_key`, `instance_name` = nom machine) depuis `ProfessionalActivationForm` ; la clé est refusée si `meta.store_id` / `meta.product_id` ne sont pas ceux d'eScope (constantes `StoreId` / `ProductId`) — l'API est commune à toutes les boutiques Lemon Squeezy, sans ce contrôle la clé d'un produit gratuit d'une autre boutique activerait la licence Pro ;
- **validate** (`license_key`, `instance_id`) à chaque démarrage, avant `EnsureLicense` (un abonnement renouvelé ne déclenche pas le rappel) et via About > License > Check now ;
- **deactivate** depuis About > License pour libérer l'activation et réutiliser la clé sur un autre poste.

Le dernier résultat est mis en cache dans `licenseProfessional.dat` (DPAPI). Hors ligne, la licence reste valide `LicenseInfo.OfflineGraceDays` (30 j) après la dernière validation ; au-delà elle compte comme expirée et un rappel « connectez-vous à Internet » s'affiche à chaque démarrage (sans dialogue de choix, pour ne pas consommer une nouvelle activation). Statut `expired` / `disabled` / `invalid` (clé ou instance inconnue) → rappel + dialogue de choix de licence au démarrage.

Objectif : la licence Pro doit être payée (contrôle produit, revalidation, délai de grâce), mais rien n'est fait contre la falsification du cache local : qui veut tricher prend la licence Personnelle.
