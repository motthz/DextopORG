# DextopORG

Un piccolo gestore automatico delle finestre per Windows 10/11. Lavora nell'area di notifica, senza finestra principale e senza dipendenze da scaricare. Scritto in C# con WinForms e API Win32, usa .NET Framework 4.8 incluso nelle versioni supportate di Windows.

## Avvio

Scarica `DextopORG.exe` dalla sezione **Releases** ed eseguilo. La prima disposizione avviene entro un secondo. Il programma non è firmato: Windows potrebbe mostrare SmartScreen. Puoi anche compilare personalmente il sorgente.

| Combinazione | Azione |
| --- | --- |
| Ctrl+Alt+O | Riordina subito |
| Ctrl+Alt+P | Attiva/disattiva l'automatismo |
| Ctrl+Alt+R | Ripristina le posizioni salvate e mette in pausa |
| Ctrl+Alt+Z | Seleziona una zona della finestra sotto il mouse e apre lo zoom live |

Clic destro sull'icona per gli stessi comandi, l'avvio con Windows (facoltativo) e **Esci**. Doppio clic per attivare o mettere in pausa. Uscire lascia le finestre nella posizione corrente; per tornare alla disposizione precedente usa prima il ripristino. Le posizioni originali sono conservate solo durante la sessione.

## Zoom su una zona

Porta il mouse sopra la finestra interessata, premi **Ctrl+Alt+Z** e trascina un rettangolo sulla parte da tenere in evidenza. **Esc** o clic destro annullano la selezione.

Si apre una vista live sempre in primo piano: la zona selezionata rimane centrata e viene ingrandita al massimo nello spazio disponibile, mantenendo le proporzioni. La vista entra nel riordino sul proprio monitor al posto della finestra originale. Puoi spostarla su un altro monitor o ridimensionarla. Sono supportate più viste, una per finestra originale.

**Esc** nella vista oppure la sua **X** chiudono lo zoom. Dal menu puoi chiudere tutti gli zoom; anche il comando di ripristino li chiude. La finestra originale resta aperta e non viene più ridimensionata dal riordino finché esiste la vista. Per interagire con il contenuto usa la finestra originale: lo zoom è una vista di sola lettura, non cambia lo zoom interno dell'applicazione.

Il riquadro segue coordinate fisse all'interno della finestra, anche se questa viene spostata: non riconosce oggetti o testo e non li segue dopo uno scorrimento o un cambio di impaginazione. Non ridurre a icona la sorgente: in quel caso la vista si sospende fino al ripristino. Contenuti protetti o alcune applicazioni possono non essere riprodotti. La selezione è valida per la sessione corrente.

La riproduzione usa le [thumbnail live di Windows DWM](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ns-dwmapi-dwm_thumbnail_properties), con ritaglio della sorgente e adattamento proporzionale della destinazione.

## Come dispone le finestre

Considera le finestre visibili e ridimensionabili del desktop attuale, escluse finestre ridotte a icona, pannelli strumenti e finestre di dialogo. Ogni finestra resta sul proprio monitor. Per ciascun monitor confronta disposizioni a righe bilanciate e sceglie quella con proporzioni complessivamente più vicine a 16:10, occupando tutta l'area disponibile senza coprire la barra delle applicazioni. Non è una ricerca di tutte le partizioni geometriche possibili: è un criterio prevedibile di buon equilibrio fra spazio e leggibilità.

Controlla ogni 800 ms se sono cambiate le finestre o gli schermi. Riordina quando apri, chiudi, riduci, ripristini una finestra o la sposti su un altro monitor. Aspetta il rilascio del mouse per evitare interferenze durante il trascinamento. Uno spostamento manuale sullo stesso monitor resta fino al successivo riordino.

Con molte finestre, le dimensioni minime imposte da alcune applicazioni possono impedire di mostrarle tutte senza sovrapposizioni. Le finestre con privilegi amministrativi possono rifiutare il ridimensionamento da un processo normale. Monitor con fattori di scala differenti richiedono verifica sul proprio dispositivo. Non viene garantita una disposizione ideale per ogni app o contenuto. Non sposta file o icone sul desktop.

## Compilazione e test

Da PowerShell nella cartella del progetto:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Test
.\bin\DextopORG.exe
```

Il compilatore incluso in Windows produce `bin/DextopORG.exe`. I test verificano 200 disposizioni e 12 casi di adattamento dello zoom: limiti, centratura e proporzioni, oltre agli input vuoti. La CI ripete compilazione e test su Windows. Selezione, ritaglio live, ingrandimento dopo ridimensionamento e chiusura con Esc sono verificabili su Windows con la finestra sintetica `tests/FocusPreview.cs`; resta necessaria la verifica con le proprie applicazioni e configurazioni multi-monitor.

## Privacy e rimozione

Nessuna connessione di rete o telemetria. Lo zoom riproduce localmente la zona selezionata, senza salvarne immagini o leggere il testo. L'unica impostazione persistente è l'eventuale voce `DextopORG` in `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. Per rimuovere l'app, disattiva prima **Avvia con Windows**, scegli **Esci** ed elimina l'eseguibile.

Licenza MIT.
