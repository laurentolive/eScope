# API — Contrats d'interface

## IProtocol (Protocol/IProtocol.cs)

```csharp
public interface IProtocol
{
    void Process();          // Appelé toutes les 50 ms — dépile la queue, déclenche callbacks / événements
    void Start();            // Lance l'acquisition (bRun = true) : liaison ouverte, lectures périodiques envoyées
    void Stop();             // Stoppe l'acquisition
    void UpdateConfig();     // Compile et envoie les commandes en attente au device
    void Dispose();          // Ferme le port, annule les tâches background

    // Lecture/écriture one-shot (apériodique) — V3 uniquement
    void ReadMem(UInt32 address, int size, Action<UInt64> onReadComplete, Action? onReadFailed = null);
    void WriteMem(UInt32 address, int size, UInt64 data);
    bool SupportsReadMem => false;   // true en V3
    bool IsConnected { get; }        // liaison ouverte (état capturing), avec ou sans acquisition

    // Lecture périodique (channel live) : valeurs livrées par FrameComplete, indexées par token
    uint ReadMemRegister(UInt32 address, int size);
    void ReadMemUnregister(uint token);

    // une fois par trame périodique reçue : nombre de trames perdues juste avant, token → valeur
    // (un token absent d'une trame tronquée n'y figure pas)
    event Action<int, IReadOnlyDictionary<uint, UInt64>>? FrameComplete;
    // aucune trame reçue depuis 1 s alors que des lectures périodiques sont configurées
    event Action? Timeout;

    // Fabrique
    static IProtocol? New(string protocol, string com);
    // protocol : "V1" | "V2" | "V3"
}
```

`size` est en **bits** (8, 16, 32, 64) ; le fil transporte des octets (`size / 8`).

**Contrats** :
- Callbacks et événements appelés sur le thread qui exécute `Process()` (thread UI)
- `onReadComplete` : valeur brute 64 bits, little-endian (l'appelant masque selon la taille / le shift)
- `ReadMem` / `WriteMem` hors acquisition (V3) : la liaison est ouverte à la demande, puis refermée après 3 s
  sans accès one-shot ; seules les commandes one-shot sont envoyées, `FrameComplete` n'est pas levé
- `onReadFailed` : échec de connexion (immédiat si la précédente a échoué il y a moins de 1 s), pas de
  réponse sous 1 s, ou reconfiguration sur timeout trame
- Ne doivent pas bloquer

---

## ICom (Com/ICom.cs)

```csharp
public interface ICom
{
    bool Open();
    void Close();
    void Dispose();
    void Flush();                                        // Vide le buffer d'entrée

    byte[] Read(int byteNb);                             // Lecture bloquante jusqu'à N bytes disponibles
    byte[] ReadAll();                                    // Lecture non-bloquante de tout ce qui est disponible
    Task<byte[]> ReadAsync(CancellationToken token);     // Attend l'arrivée de données
    void Write(byte[] data);

    static ICom? Get(string com);
}
```

| Préfixe | Classe | Format |
|---|---|---|
| `Serial` | `ComSerial` | `Serial:{port},{baudrate},{bits},{parity N/E/O},{stopbits},{timeout}` — ex. `Serial:COM3,115200,8,N,1,500` |
| `TCP` | `ComTcp` | `TCP:{host},{port}` — ex. `TCP:192.168.1.10,5000` |
| `RTT` | `ComRtt` | `RTT:{target}:{probeUID}:{host}:{port}:{cbAddr}` — voir [rtt-transport.md](rtt-transport.md) |

---

## ISymbol (Symbol/ISymbol.cs)

```csharp
public interface ISymbol
{
    List<string> List(string name);   // Complétion : symboles contenant name ; GDB : champs après '.' / '->'
    SymbolData?  Get(string name);    // Résolution nom ou expression → adresse/taille ; null si inconnu
    bool         isValid(string name);

    static ISymbol? New(string type, string device, string symbolFilePath,
                        string gdbPath, string mapRegEx);
    // type : "map" | "gdb"
}

public class SymbolData
{
    public string Device   { get; set; }   // Nom du device (ex. "Device1")
    public string Name     { get; set; }   // Nom de la variable / expression
    public string Address  { get; set; }   // Hexa sans préfixe : "20000100" (vide si non résolue)
    public int    Size     { get; set; }   // Bits : 8, 16, 32 ou 64 (GDB : sizeof plafonné à 8 octets)
    public int    Shift    { get; set; }   // Décalage bit (champ de bits)
    public string Type     { get; set; }   // Type C (GDB : sortie de whatis)
    public bool   Resolved { get; set; }   // false : expression valide dont l'adresse passe par un
                                           // pointeur non lisible (cible non connectée)
}
```

`SymbolGDB` ajoute (hors interface) :
- `Task<ExprValue> EvaluateAsync(string expr)` — Watch : adresse d'une lvalue ≤ 8 octets, sinon valeur texte calculée par GDB
- `GdbRemoteBridge Bridge` — `Service(IProtocol)` à appeler sur le thread de `Process()`
- `SetWaitPump(Action, int threadId)` — pompe exécutée pendant une requête synchrone sur ce thread

---

## Format filaire Protocol V3

### PC → Device (configuration)

6 octets par commande :
```
[CMD][SIZE][ADDR_0][ADDR_1][ADDR_2][ADDR_3]
```
Pour `CMD_WRITE_APERIODIC`, SIZE octets de données suivent immédiatement.
`CMD_CLEAR` est envoyé seul (1 octet) ; ce qui le suit dans le même envoi est ignoré.

Adresse en little-endian 32 bits.

### Device → PC (trame de données)

```
55  flowId  dataSize  cmd  [payload…]  AA
^                                      ^
sync                                  footer
```

Payload : données des lectures dans l'ordre de configuration. Trame d'erreur : `dataSize = 0`,
`cmd` = code d'erreur, répétée jusqu'au prochain `CMD_CLEAR`.

**Exemple** — lecture 2 octets @ 0x20000100 (valeur 0x1234) :
```
Config envoyée :  43 02 00 01 00 20
Trame reçue   :  55 01 02 43 34 12 AA
```

### Lectures one-shot (`CMD_READ_APERIODIC`)

Une lecture one-shot est ajoutée en fin de config ; sa donnée arrive dans la trame
suivante, après celles des lectures déjà configurées, puis la commande est retirée.

Firmwares publiés avant la correction : les commandes reçues après une lecture one-shot
dans le même envoi sont perdues (une 2e lecture one-shot, un canal ajouté, une écriture),
de même qu'un envoi reçu avant qu'elle soit servie. Règles côté PC (`ProtocolV3.UpdateConfig`),
compatibles avec tous les firmwares :
- au plus une lecture one-shot par envoi, en dernière position ;
- pas de nouvel envoi tant que sa réponse n'est pas reçue (timeout 1 s → reconfiguration).

Firmware corrigé (`eScopeProtocolV3.c`, `V3Id.c`, `arduino/DebugScope.cpp`) : toutes les
commandes reçues sont traitées dans le même tick, les commandes one-shot servies sont retirées
en décalant les suivantes (ordre des lectures conservé), une commande incomplète est attendue
puis abandonnée après `ESCOPE_PROTOCOL_RX_TIMEOUT` ticks. Format filaire inchangé.

### Codes de commande

```
CMD_NOP             0x00   (ou commande inconnue : octets reçus abandonnés)
CMD_DEINIT          0x01
CMD_CLEAR           0x02
CMD_READ_PERIODIC   0x43   READ_FLAG(0x40) | 0x03
CMD_READ_APERIODIC  0x44   READ_FLAG       | 0x04
CMD_WRITE_APERIODIC 0x25   WRITE_FLAG(0x20)| 0x05
ERR_TXOVF           0x86   ERR_FLAG(0x80)  | 0x06   trame TX trop petite
ERR_CFGOVF          0x87   ERR_FLAG        | 0x07   buffer de config plein
ERR_BWOVF           0x89   ERR_FLAG        | 0x09   bande passante dépassée (Arduino uniquement)
```

---

## API côté cible (arduino/DebugScope.h)

```cpp
class DebugScope {
public:
    explicit DebugScope(HardwareSerial &stream, uint32_t baudrate = 115200);
    void begin();       // Appeler dans setup()
    void update();      // Appeler à période fixe dans loop() (= période d'échantillonnage)
    void end();
    bool isEnded() const;    // CMD_DEINIT reçu
    void rxByte(uint8_t b);  // Pour serialEvent() / ISR
};

// Constantes
#define DEBUG_SCOPE_CHAN_MAX        8
#define DEBUG_SCOPE_TX_SIZE        128
#define DEBUG_SCOPE_RX_SILENCE_MS  2     // silence = fin de réception
#define DEBUG_SCOPE_RX_TIMEOUT_MS  20    // commande incomplète abandonnée
#define DEBUG_SCOPE_TS             0.001 // 1 ms intervalle d'échantillonnage
```

API C générique (`eScopeProtocol/eScopeProtocol.h`) : `eScopeProtocol_Init()`,
`eScopeProtocol_Process()` (à période fixe), `eScopeProtocol_isEnd()`, `eScopeProtocol_DeInit()`.
