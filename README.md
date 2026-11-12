# DextopORG

Gestore automatico delle finestre per Windows 10/11. Lavora nell'area di notifica e dispone le finestre indipendentemente su ciascun monitor. C# / WinForms / API Win32, senza pacchetti esterni, con .NET Framework 4.8 presente nelle versioni supportate di Windows.

## Avvio e comandi

Scarica `DextopORG.exe` dall'ultima **Release**. Chiudi la versione precedente dal suo menu **Esci**, poi avvia quella nuova. Il file non è firmato e Windows potrebbe mostrare SmartScreen. Puoi anche compilare il sorgente.

| Combinazione | Azione |
| --- | --- |
| Ctrl+Alt+O | Riordina subito |
| Ctrl+Alt+P | Pausa/riprendi l'automatismo |
| Ctrl+Alt+R | Rimuovi tutti i ritagli, ripristina le posizioni iniziali e metti in pausa |
| Ctrl+Alt+Z | Con il mouse sopra una finestra, seleziona una zona; ripeti per rimuovere il ritaglio |

Clic destro sull'icona per i comandi, le istruzioni del ritaglio, la rimozione di tutte le selezioni, l'avvio facoltativo con Windows e **Esci**. Doppio clic per pausa/ripresa.

## Selezione sulla finestra originale

Porta il mouse sulla finestra, premi **Ctrl+Alt+Z** e trascina un rettangolo sulla zona che vuoi mantenere visibile. **Esc** o clic destro annullano la selezione. L'overlay esiste solo durante la selezione: non viene creata una seconda finestra per il contenuto e non viene mostrata una copia live.

Il ritaglio viene applicato direttamente alla finestra originale, che conserva i suoi controlli cliccabili e la tastiera. La zona selezionata viene centrata nel riquadro assegnato sul relativo monitor. La finestra non viene ridimensionata mentre ha una selezione, per evitare di cambiare l'impaginazione del contenuto. Se la zona è più grande del riquadro, viene tagliata simmetricamente ai bordi. Se è più piccola, resta spazio libero intorno.

**Il ritaglio conserva la scala originale dei pixel: non è uno zoom del contenuto.** Windows non offre un modo universale per ingrandire testo e controlli dentro qualsiasi finestra mantenendola interattiva, senza una vista separata. Usa lo zoom dell'applicazione quando disponibile e seleziona nuovamente la zona dopo averlo cambiato. Il riquadro segue coordinate fisse, non un oggetto dopo uno scorrimento.

Per tornare alla finestra intera, premi nuovamente **Ctrl+Alt+Z** con il mouse sulla sua parte visibile. **Ctrl+Alt+R** ripristina tutto, anche se la selezione ha nascosto barra del titolo o pulsanti. Pausa e uscita rimuovono i ritagli; le finestre selezionate tornano alla posizione iniziale, le altre restano nella posizione assegnata. Le selezioni e le posizioni iniziali sono conservate solo durante la sessione.

## Disposizione senza sovrapposizioni

Ogni monitor ha il proprio insieme di finestre e usa la propria area di lavoro, escludendo la barra delle applicazioni. L'app confronta disposizioni a righe bilanciate, preferendo proporzioni vicine a 16:10. Le finestre non vengono trasferite automaticamente tra monitor.

Dopo aver richiesto posizione e dimensioni, l'app legge le dimensioni effettive e limita la regione visibile di ogni finestra al suo riquadro. Quindi, anche se un'app impone una dimensione minima più grande del riquadro, la parte eccedente viene tagliata e non copre le altre finestre. Questa scelta può nascondere contenuti o comandi: usa **Ctrl+Alt+R** per recuperarli. I ritagli espliciti seguono lo stesso vincolo.

Il controllo avviene ogni 800 ms: apertura, chiusura, riduzione, ripristino, spostamento e ridimensionamento delle finestre provocano un riordino. Durante il trascinamento l'app aspetta il rilascio del mouse. Il vincolo viene riapplicato dopo uno spostamento manuale; durante il trascinamento o prima del controllo successivo può esserci una sovrapposizione transitoria.

Sono gestite le finestre principali visibili, incluse quelle di dimensione fissa. Finestre ridotte a icona, desktop virtuali non attivi, pannelli strumenti, menu e finestre di dialogo dipendenti restano esclusi. Un'app con privilegi superiori o che rifiuta le API di posizione/ritaglio può impedire il controllo: viene mostrato un avviso e su quella finestra non si garantisce il vincolo. Le configurazioni con DPI misti richiedono verifica sul dispositivo.

Il ritaglio usa [SetWindowRgn](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowrgn). La regione originale viene salvata e ripristinata alla rimozione del ritaglio e all'uscita normale. Usa **Esci** invece di terminare forzatamente il processo, per consentire il ripristino.

## Compilazione e test

Da PowerShell nella cartella del progetto:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Test
.\bin\DextopORG.exe
```

Il compilatore incluso in Windows produce `bin/DextopORG.exe`. La CI esegue gli stessi test su Windows:

- 200 disposizioni: numero, limiti, assenza di sovrapposizioni e copertura completa, con monitor verticali, coordinate negative e risoluzioni dispari.
- 12 casi di centratura e ritaglio della selezione, anche quando supera il riquadro.
- 40 disposizioni con finestre che eccedono lo spazio assegnato.
- Test Win32 su finestre di prova: confinamento nonostante la dimensione minima, conservazione dello stesso HWND, nessuna finestra aggiunta, centratura e ripristino delle regioni originali.

`tests/FocusPreview.cs` è una fixture manuale che non avvia il riordino delle altre applicazioni. Consente di verificare la selezione e il clic su un controllo originale dopo il ritaglio. **Esc** nella fixture rimuove il ritaglio di prova. Il comportamento richiede comunque verifica con le proprie applicazioni e configurazioni multi-monitor.

## Privacy e rimozione

Nessuna connessione di rete, telemetria o lettura dei contenuti delle finestre. Non vengono salvate immagini. L'unica impostazione persistente è la voce opzionale `DextopORG` in `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. Per rimuovere l'app, disattiva **Avvia con Windows**, scegli **Esci** ed elimina l'eseguibile. L'app non organizza file o icone del desktop.

Licenza MIT.
