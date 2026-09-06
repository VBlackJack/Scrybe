# Historique des versions

[English](CHANGELOG.md)

## v2026.090601

Version du 6 septembre 2026. Changements depuis `v2026.061601`.

### Fonctionnalités

- Correction OCR facultative à côté de la capture, sans publication en cas d'annulation.
- Profils confirmés par processus pour le mode et la cadence d'injection.
- Conservation de 20 versions locales, avec aperçu, contrôle des révisions et validation DPAPI avant restauration.
- Import/export strict et versionné des snippets, aperçu du modèle et règles explicites de conflit.
- README, documentation et notes de version en anglais et français séparés.

### Fiabilité et accessibilité

- Vérification continue de la cible et du clavier ; libération de Maj avant les touches spéciales.
- Conservation du contenu récent du presse-papiers et des espaces aux limites du texte OCR.
- Refus des écritures après échec de lecture ou révision périmée, quarantaine et mutations sérialisées.
- Conservation des brouillons au rechargement ; publication des modifications après sauvegarde réussie.
- Progression, annulation visible et motifs d'échec précis.
- Sélection au clavier, annonce des coordonnées et interface bornée à DPI élevé.

### Validation et livraison

- Corpus OCR mesuré, récepteurs natif/distant et 22 rendus EN/FR.
- Auto-test OCR du paquet et générateur Windows Sandbox.
- Configuration unifiée des projets et verrous des références internes actualisés.
- Notes de version EN/FR séparées avec empreintes ; contrôle de la version du commit en CI.

## v2026.061601

Renforcements précédemment documentés depuis `v2026.060901`.

### Sécurité et persistance

- Raccourcis d'injection de diagnostic désactivés par défaut.
- Propagation du résultat d'injection du presse-papiers, sans effacement après échec.
- Fin d'attente de capture à la fermeture de la fenêtre.
- Entropie DPAPI propre à Scrybe, format versionné et migration.
- Confirmation native de cible rendue testable et contrôle du premier plan pour les snippets.
- Échecs de sauvegarde JSON signalés et écritures atomiques par fichier temporaire voisin.

### Outils et interface

- SDK, verrous NuGet, versions de langage et analyseurs fixés.
- Contrôle du formatage et exécution des tests dans la configuration demandée.
- Workflows CodeQL et Dependency Review ; CodeQL activable pour les dépôts privés.
- Prérequis documentés, raccourcis supplémentaires dans les réglages.
- Libellés accessibles, virtualisation des listes et simplification des onglets.


## v2026.060901

### Interface

- Amélioration du centre de contrôle et des formulaires.
- Icônes de style Fluent sur les onglets et actions principales.
- Disposition compacte des actions et statuts de l'accueil.

## v2026.060802

### Fonctionnalités

- Historique chiffré avec palette.
- Modification des entrées de capture.
- Injection du texte du presse-papiers.

### Interface

- Regroupement des réglages, diagnostics et gestionnaires dans les onglets.
- Thème des fenêtres, barres de défilement et confirmations.

### Compilation

- Renforcement du processus de publication.

## v2026.060801

### Fonctionnalités

- Versions CalVer.
- Fenêtre À propos avec métadonnées de compilation.

## Initial

- Base initiale de Scrybe.
