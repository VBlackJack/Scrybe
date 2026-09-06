# Fiabilité et validation

[English](../validation.md)

## Contrôles des fonctionnalités

Voir [Vérification OCR, profils et échange](features.md) pour les parcours.
Les tests couvrent l'annulation OCR, les espaces du texte corrigé, le mode et la
cadence par profil, les brouillons invalides, la conservation des versions, les
aperçus périmés, l'intégrité des sauvegardes, la récupération DPAPI et l'import
atomique. Le contrôle d'interface rend aussi les dialogues OCR et import en
EN/FR à 96/192 DPI, avec contrôle des liaisons et limites des boutons accessibles.
Le focus réel et la synthèse vocale demandent un bureau interactif.

## Récupérer les modifications non enregistrées

Les quatre gestionnaires proposent **Recharger et garder le brouillon**.
La révision actuelle est lue sans sauvegarde automatique de l'éditeur.
En cas d'échec, la liste précédente reste disponible et les écritures restent
bloquées. Examinez liste et brouillon, puis enregistrez explicitement.
Une nouvelle modification du fichier après rechargement est à nouveau refusée.

- Snippets et secrets conservent les champs et sélectionnent par identifiant
  stable. Un identifiant supprimé ne sélectionne pas un autre élément.
  Un nouveau secret exige toujours un mot de passe.
- L'historique conserve le texte révélé. Si l'entrée a été supprimée, la
  sauvegarde est désactivée et le texte n'est pas appliqué ailleurs.
- Les réglages conservent le formulaire et rafraîchissent la révision du fichier.
  Le statut avertit que l'enregistrement remplacera le fichier par le formulaire.
  Il n'y a pas de fusion automatique.
- Les brouillons restent en mémoire et disparaissent à la fermeture. Un secret
  en clair n'est jamais exporté en fichier de récupération ; son éditeur suit
  toujours l'effacement au déchargement.

Chargements et mutations sont sérialisés dans chaque processus, y compris
l'historique en arrière-plan. Les verrous interprocessus et empreintes restent
actifs. Les modifications des bibliothèques ne deviennent visibles qu'après
persistance réussie : un brouillon refusé ne rejoint ni palette ni sauvegarde
ultérieure d'une autre action.

## Retour sur l'injection

Une fenêtre sans prise de focus affiche le processus confirmé, son handle,
le nombre de frappes traitées, le statut et le bouton Arrêter.
La fermer annule aussi l'injection. Aucun texte injecté n'est affiché.
Les mises à jour sont limitées en fréquence ; les statuts distinguent changement
de cible ou disposition, annulation, caractère non représentable, erreur native
et blocage par élévation.

Il n'y a pas de reprise automatique. Toute nouvelle demande repasse par la
confirmation. Les compteurs mesurent le travail de l'émetteur et ne prouvent pas
la réception distante. La course finale entre contrôle du premier plan et
`SendInput` subsiste.

## Contrôles automatiques

Utilisez le SDK fixé dans `global.json`. Chaque exécution exige un **nouveau
dossier de sortie**. Codes : 0 réussite, 1 échec de validation, 2 arguments
invalides ou dossier existant. Un échec après création produit `failure.json`
et des journaux.

```powershell
dotnet build Scrybe.slnx -c Release
dotnet test Scrybe.slnx -c Release --no-build
dotnet run --project tools/Scrybe.Validation -c Release --no-build -- ocr artifacts/ocr-check
dotnet run --project tools/Scrybe.Validation -c Release --no-build -- overlay artifacts/overlay-check
```

Le corpus `tools/Scrybe.Validation/corpus.json` définit des exemples de console
et code, fonds clairs/sombres, polices 12/16 DIP et DPI 96/144/192.
Le programme produit les PNG, emploie Tesseract réel et mesure les quatre modes
de nettoyage. `ocr.json` contient les durées d'initialisation, reconnaissance
et nettoyage, ainsi que le taux d'erreur de caractères exact et normalisé.
La mesure exacte révèle les pertes d'indentation ou de retours à la ligne que
la normalisation peut masquer. Le seuil normalisé par exemple est actuellement
0,05 ; tout dépassement échoue. Les durées n'ont pas de seuil dépendant du
matériel. Un troisième argument facultatif sélectionne un autre corpus.
Les exemples synthétiques ne couvrent pas toutes les consoles distantes.

Le contrôle de sélection rend les vues WPF sans les afficher, active le même
gestionnaire de touches et vérifie les noms accessibles, régions dynamiques,
coordonnées, limites du texte et contraste réel d'au moins 4,5.
Il couvre EN/FR, trois échelles et des métadonnées de coordonnées négatives.
Cela ne prouve ni la parole d'un lecteur d'écran ni le placement sur des
moniteurs physiques. Vérifiez interactivement K, flèches, Maj+flèches,
Ctrl+flèches, Entrée/Échap et annonce des coordonnées sur chaque moniteur.

## Injection native et distante

Sur un bureau contrôlé, le programme affiche son propre récepteur, envoie du
texte synthétique avec Tab/Entrée et relève la réception. Il n'installe aucune
disposition : celle demandée doit exister. La disposition du thread est
restaurée en sortie. Ces contrôles sont exclus de la CI non interactive.

```powershell
dotnet run --project tools/Scrybe.Validation -c Release --no-build -- native artifacts/native-us --allow-input --layout=00000409 --scenario=complete
dotnet run --project tools/Scrybe.Validation -c Release --no-build -- native artifacts/native-fr --allow-input --layout=0000040c --scenario=complete
dotnet run --project tools/Scrybe.Validation -c Release --no-build -- native artifacts/native-focus --allow-input --scenario=focus
dotnet run --project tools/Scrybe.Validation -c Release --no-build -- native artifacts/native-cancel --allow-input --scenario=cancel
```

Chaque scénario utilise Unicode et scancode. `native.json` contient le texte
synthétique reçu, résultat, disposition, taux d'erreur et événements clavier.
Un scénario complet exige l'égalité exacte ; une interruption exige une séquence
incomplète et annulée. L'absence de premier plan interdit tout envoi.

Pour RDP/Citrix, exécutez le récepteur passif **dans la session distante**, puis
injectez depuis Scrybe local à travers le client distant :

```powershell
dotnet run --project tools/Scrybe.Validation -c Release --no-build -- receiver artifacts/remote-receiver
```

La fenêtre présente une référence et un champ vide. Injectez puis comparez.
Les rapports contiennent seulement compteurs, taux d'erreur et résultat, sans
texte reçu ni journal clavier. Répétez avec chaque mode, disposition installée,
cadence faible/élevée, perte de focus et arrêt d'urgence. Notez les versions
client/serveur, le DPI et la cadence. Un test natif local ne valide pas le
transport distant.

## Paquet publié et Windows vierge

`Build.ps1` exécute l'auto-test du paquet avant signature et archivage.
Il charge les DLL et le modèle OCR réels, reconnaît une image synthétique
et vérifie unicité et parité des langues. Il s'arrête avant données utilisateur,
raccourcis, presse-papiers et interface normale.

```powershell
dotnet publish src/Scrybe.App/Scrybe.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/publish-check
& ./artifacts/publish-check/Scrybe.exe --self-test ./artifacts/package-result.json
./tools/New-PackageSandbox.ps1 -PublishDirectory ./artifacts/publish-check -OutputDirectory ./artifacts/sandbox-check
```

Le rapport doit être nouveau. L'automatisation doit attendre la fin du WinExe et
vérifier son code de sortie ainsi que `passed`. Sur un hôte équipé de Windows
Sandbox, ouvrez le WSB produit. Le paquet est monté en lecture seule, les preuves
en écriture ; réseau et redirection du presse-papiers sont désactivés.
Le même test s'exécute sans SDK installé. Exigez `sandbox-receipt.json` :
la génération du WSB ne constitue pas une réussite sur Windows vierge.
Le générateur prend en charge WhatIf et n'installe aucun composant Windows.
Sa syntaxe suit la [documentation Microsoft](https://learn.microsoft.com/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-configure-using-wsb-file).

La CI exécute corpus, interfaces et auto-test du paquet.
Les preuves GitHub CI, réception distante et Windows vierge restent distinctes
des contrôles locaux.
