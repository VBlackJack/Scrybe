# Scrybe v2026.090601

[English](../release-notes.md)

## Nouveautés

- Vérifier et corriger le texte OCR à côté de la capture avant copie.
- Utiliser des profils de mode et cadence par application, affichés à la
  confirmation de cible et figés pour chaque demande.
- Restaurer une version locale après aperçu des métadonnées et confirmation.
  La version précédente est sauvegardée ; 20 versions sont conservées.
- Importer et exporter des bibliothèques versionnées de snippets, examiner les
  modèles et choisir de conserver, remplacer ou copier les conflits.
- Récupérer les brouillons après conflit de stockage, refuser les écritures
  périmées et exclure les modifications refusées des bibliothèques actives.
- Afficher progression et annulation, revérifier cible et disposition clavier
  et conserver le contenu récent du presse-papiers.
- Sélectionner les régions au clavier et conserver les espaces aux limites.
- Mesurer le corpus OCR, contrôler le rendu des interfaces, exécuter l'auto-test
  OCR du paquet et produire une configuration Windows Sandbox.
- Fournir la documentation anglaise et française dans des fichiers séparés.

## Données et compatibilité

La vérification OCR est facultative et désactivée par défaut. Les profils
identifient le processus local. Les sauvegardes conservent la protection DPAPI
et exigent le même compte Windows et la même machine pour les données protégées.
Les éléments supprimés peuvent subsister dans les versions conservées.
Redémarrez après restauration des réglages. L'échange de snippets est limité à
4 Mio et 1 000 éléments, sans magasin de secrets ni historique.

Conservez les composants x64 et tessdata avec l'exécutable. La réception native,
RDP/Citrix, la synthèse vocale et Windows vierge nécessitent leurs contrôles
interactifs respectifs. Les tests locaux automatiques ne certifient pas ces
environnements.
