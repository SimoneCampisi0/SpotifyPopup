# Istruzioni per il progetto

- Lavora esclusivamente nella cartella di questo progetto. Non modificare Spotify, i suoi file o file esterni al progetto.
- Mantieni dipendenze, cache, file temporanei e output di compilazione nel progetto. Usa `build.ps1` per compilare con percorsi locali.
- Se manca una dipendenza importante, fermati e informa l'utente; procedi con il download locale solo quando autorizzato.
- Il progetto è un'app Windows C# / WPF che interagisce con la sessione multimediale di Spotify tramite le API Windows. Non dare per garantiti dati o comandi non effettivamente esposti dalla sessione.
- Preserva il comportamento del popup sempre in primo piano, il blocco con clic passanti e una modalità di sblocco accessibile.
- Verifica la compilazione e, quando possibile, il comportamento con Spotify realmente aperto. Riporta esplicitamente ciò che non è stato verificato.
