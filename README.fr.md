# Scrybe

[English](README.md)

> **scry** : lire un écran distant ; **scribe** : écrire en simulant la frappe.

Scrybe est une petite application Windows dans la zone de notification, destinée
aux consoles où le copier-coller est peu fiable ou indisponible.

Capturez le texte affiché à l'écran, puis saisissez du texte dans la cible lorsque
le collage est bloqué. Tout reste local : aucun OCR dans le cloud, aucune
télémétrie et aucun compte à créer.

## Pourquoi Scrybe ?

Les consoles distantes ou verrouillées compliquent le transfert d'une commande,
d'un mot de passe, d'une ligne de journal ou d'un jeton. Le texte est visible mais
impossible à copier, ou disponible localement mais impossible à coller.

- **Capturer** : sélectionner une région, reconnaître le texte localement,
  le nettoyer et le copier dans le presse-papiers local.
- **Injecter** : envoyer le texte sous forme de frappes simulées, y compris les
  commandes longues, snippets et secrets protégés localement.
- **Rester hors ligne** : conserver réglages, snippets, secrets et historique
  sur la machine, dans le profil Windows courant.

## Fonctionnalités actuelles

- Application Windows WPF dans la zone de notification.
- Capture du moniteur sous le curseur, avec gestion du DPI par moniteur.
- Nettoyage OCR adapté au texte brut, au code et aux journaux.
- Vérification facultative du texte OCR à côté de la capture avant copie.
- Injection Unicode ou scancode, avec profils de cadence par processus cible.
- Bibliothèques de snippets et de secrets.
- Import et export versionnés des snippets avec traitement explicite des conflits.
- Secrets et texte de l'historique protégés par Windows DPAPI.
- Conservation de 20 versions locales par magasin avec aperçu de restauration
  et contrôle de la révision actuelle.
- Centre de contrôle, raccourcis configurables, diagnostics et interface EN/FR.
- Distribution Windows autonome avec moteur OCR à côté de l'exécutable.

La sélection de capture fonctionne au clavier : **K** crée une région centrale,
les **flèches** la déplacent, **Maj+flèches** la redimensionnent et **Ctrl** active
des pas de dix pixels. **Entrée** valide, **Échap** annule. Les coordonnées sont
en pixels physiques. Les modes brut et code conservent les espaces aux limites,
l'indentation et les retours à la ligne finaux.

L'injection revérifie la fenêtre confirmée, son processus et sa disposition
clavier avant chaque groupe de frappes. Un changement observé arrête l'opération.
Le scancode utilise la disposition de la cible. Windows `SendInput` ne permet pas
de lier atomiquement les frappes à une fenêtre : les contrôles réduisent le risque
de changement de focus sans supprimer la course entre contrôle et envoi.
L'injection du presse-papiers n'efface que la version initialement lue, afin de
conserver le contenu copié pendant la frappe.

## Données locales

Les fichiers sont conservés sous :

```text
%LOCALAPPDATA%\Scrybe
```

- Les secrets et le texte de l'historique sont chiffrés avec DPAPI.
- Les magasins JSON mal formés sont déplacés dans `*.corrupt.<timestamp>.json`.
- Les diagnostics indiquent les chemins, emplacements et composants OCR requis.
- L'onglet À propos donne accès aux journaux et rapports de diagnostic.

Un magasin refuse les écritures après échec de lecture ou modification du fichier
depuis son chargement. Un fichier voisin `.lock` coordonne les processus Scrybe ;
sa présence seule n'indique pas un verrou actif. Après conflit, utilisez
**Recharger et garder le brouillon**, examinez les données, puis enregistrez
explicitement. Il n'y a pas de fusion automatique. Les éléments invalides sont
mis en quarantaine avec leurs octets d'origine ; si cette opération échoue,
les écritures restent bloquées.

Une fenêtre de progression sans prise de focus affiche l'avancement, l'arrêt et
les motifs d'interruption. Consultez [Fiabilité et validation](docs/fr/validation.md)
et [Vérification OCR, profils et échange](docs/fr/features.md) pour les parcours,
la conservation des versions et les limites de l'import.

## Prérequis

- Windows 10 version 19041 ou ultérieure.
- SDK .NET 10, version `10.0.103` fixée dans `global.json`.
- Modèle OCR fourni dans `tessdata/eng.traineddata`.
- Pour la distribution publiée, conserver le dossier `x64/` à côté de
  `Scrybe.exe`.

## Compiler et exécuter

Après clonage :

```powershell
dotnet restore Scrybe.slnx
dotnet build Scrybe.slnx --configuration Debug
dotnet test Scrybe.slnx --configuration Release
dotnet run --project src\Scrybe.App\Scrybe.App.csproj
```

- `Run.bat` lance l'application depuis les sources en Debug.
- `Build.bat` compile en Debug.
- `Test.bat` exécute les tests.
- `Release.bat` lance la préparation locale d'une version.

Le processus vérifie le formatage, exécute les tests Release, compile, publie
localement, contrôle le contenu du paquet, produit le SBOM SPDX et les empreintes
SHA256, puis crée une archive dans `Dist/`.

```powershell
dotnet format Scrybe.slnx --verify-no-changes
.\Build.ps1 -Mode Release -DryRun
```

La signature Authenticode est facultative. Une distribution non signée peut
déclencher un avertissement SmartScreen ; accompagnez l'archive des fichiers
`.sha256` et `.spdx.json`. Pour signer, fournissez
`SCRYBE_SIGNING_CERT_THUMBPRINT` pour un certificat du magasin Windows, ou
`SCRYBE_SIGNING_CERT_PATH` et `SCRYBE_SIGNING_CERT_PASSWORD` pour un PFX :

```powershell
.\Build.ps1 -Mode Release -Publish -Sign
```

CodeQL fonctionne automatiquement pour les dépôts publics. Pour un dépôt privé,
activez l'analyse de code GitHub et la variable `SCRYBE_ENABLE_CODEQL=true`.
Sinon, ce contrôle est ignoré afin de ne pas bloquer la compilation ou la
publication sur une configuration externe.

## État du projet

Scrybe est utilisé en conditions réelles pour sa validation. Les parcours OCR,
injection, snippets, secrets DPAPI, historique protégé, diagnostics, raccourcis,
packaging et centre de contrôle sont implémentés. Les travaux restants portent
sur les cas limites, la clarté des erreurs et les interactions des consoles
distantes.

## Organisation du dépôt

```text
Scrybe/
|-- src/       # Application WPF, bibliothèque métier, moteur OCR
|-- tests/     # Tests xUnit
|-- locales/   # Textes EN/FR
|-- tessdata/  # Modèle OCR
|-- docs/      # Documentation anglaise et miroir fr/
|-- CHANGELOG.md
|-- CHANGELOG.fr.md
|-- README.md
`-- README.fr.md
```

Consultez l'[architecture](docs/fr/architecture.md),
l'[historique des versions](CHANGELOG.fr.md) et les
[notes de version](docs/fr/release-notes.md).
