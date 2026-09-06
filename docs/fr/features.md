# Vérification OCR, profils et échange de données

[English](../features.md)

## Vérifier le résultat OCR avant de copier

Activez **Vérifier le résultat OCR avant de copier** dans les réglages, puis
enregistrez. Capturez une région normalement. La capture originale apparaît à
côté du texte reconnu et nettoyé. Corrigez le texte, puis choisissez **Valider et
copier**. Le texte accepté devient la dernière capture et rejoint l'historique
protégé lorsque celui-ci est activé.

Annuler ou fermer la fenêtre conserve le presse-papiers, la dernière capture et
l'historique précédents. Aucune image de capture n'est enregistrée en cas
d'annulation. La vérification est désactivée par défaut. Les espaces, tabulations
et retours à la ligne du texte corrigé sont conservés. Entrée et Tab insèrent des
caractères ; Ctrl+Tab quitte l'éditeur. Échap annule. Le brouillon et l'image
restent en mémoire.

## Profils d'injection par application

Dans les réglages, ajoutez un profil, sélectionnez-le et indiquez le nom du
processus cible sans `.exe`, par exemple `mstsc`. Choisissez Unicode ou scancode,
le délai entre touches de 5 à 200 ms et le délai supplémentaire après Entrée de
0 à 1000 ms, puis enregistrez.

La correspondance du nom est exacte, sans tenir compte de la casse. Les noms
vides, doublons, chemins d'exécutables, caractères génériques et délais invalides
sont refusés. Une cible sans profil utilise les réglages globaux. Le profil
identifie le processus local : les applications distantes partageant un même
client RDP/Citrix partagent donc son profil.

La confirmation affiche le mode et les délais retenus. Ces valeurs sont figées
pour la demande, y compris pour un secret. Un profil ne contourne jamais la
confirmation, les contrôles du premier plan et de disposition clavier, ni
l'annulation. Il ne change pas la disposition clavier Windows.

## Versions locales et restauration

Avant de remplacer un fichier JSON existant, Scrybe sauvegarde ses octets
précédents dans `<fichier>.versions`, à côté des réglages, snippets, secrets ou
captures. Chaque enveloppe contient une version de format, une date, le nom du
magasin, un SHA-256 et les données. Chaque magasin conserve au plus 20 versions.
La première création n'a pas de version précédente. Enregistrer des octets
identiques ne consomme pas de version et ne remplace pas une version utile.

Ouvrez **Versions et restauration** dans les réglages. Sélectionnez une version,
puis continuez. Vérifiez le nom, la date, le nombre d'éléments et la taille avant
de choisir **Restaurer cette version**. Les aperçus périmés, versions corrompues et
données incompatibles sont refusés. La version actuelle est sauvegardée avant le
remplacement atomique. L'échec de la sauvegarde préalable bloque aussi un
enregistrement normal du magasin.

Les bibliothèques sont rechargées après restauration. Rechargez les éditeurs
ouverts avant de sauvegarder leurs brouillons. Redémarrez Scrybe pour appliquer
les réglages restaurés ; le formulaire actif n'est pas remplacé silencieusement.
Les brouillons restent uniquement en mémoire et ne constituent pas une sauvegarde.

Les secrets et captures conservent leur chiffrement DPAPI. Avant restauration,
Scrybe vérifie leur déchiffrement sous le compte Windows actuel et efface les
tampons temporaires. Ces versions sont destinées au même compte et à la même
machine, pas au transfert portable des secrets. Les réglages, modèles de snippets
et métadonnées restent lisibles ; l'encodage base64 de l'enveloppe n'est pas un
chiffrement. Les secrets et captures supprimés peuvent subsister dans les versions
conservées jusqu'à leur expiration. La quarantaine reste un mécanisme distinct.

## Import et export des snippets

Le gestionnaire exporte la bibliothèque enregistrée, sans brouillons, magasin de
secrets ni historique. Choisissez un **nouveau** fichier JSON. Les fichiers
existants ne sont pas remplacés.

L'import accepte l'enveloppe `Scrybe.Snippets` version 1, dans la limite de 4 Mio
et 1 000 snippets. Les champs ou versions inconnus, propriétés JSON et
identifiants en doublon, éléments nuls et paramètres mal formés sont refusés.
Enregistrez ou abandonnez le brouillon ouvert avant d'importer.

L'aperçu liste tous les snippets entrants. Sélectionnez-en un pour examiner son
modèle et les valeurs par défaut de ses paramètres. Choisissez une règle globale :

- **Conserver les snippets existants** : ignorer les éléments entrants en conflit.
- **Remplacer les snippets en conflit** : remplacer l'unique correspondance en
  conservant son identifiant stable. Les correspondances ambiguës sont refusées.
- **Importer les conflits comme copies distinctes** : conserver les éléments
  existants et attribuer de nouveaux identifiants aux copies.

Un conflit correspond à un identifiant identique ou au même nom et à la même
catégorie, sans tenir compte de la casse pour les libellés. La règle s'applique
aussi entre éléments entrants, traités dans l'ordre. La collection finale est
validée et enregistrée en une seule fois. Si la bibliothèque examinée ou le
fichier a changé, l'import est refusé sans résultat partiel publié.

Le format sert aux modèles non secrets. Du texte sensible peut néanmoins avoir
été saisi manuellement dans un modèle ou ses paramètres : vérifiez le contenu
avant de partager le fichier.

## Documentation et notes de version

L'anglais utilise les chemins habituels. Les miroirs français utilisent
`README.fr.md`, `CHANGELOG.fr.md` et `docs/fr/`, avec des liens réciproques.
Les notes de version suivent la même séparation. Les mots-clés du dépôt
décrivent les capacités implémentées sans certifier les transports distants.
