# Scrybe

> **scry** (lire l'écran à distance) + **scribe** (écrire / taper au clavier)

Utilitaire Windows **standalone**, léger et **local-first** pour développeurs, SysAdmins et DevOps travaillant sur consoles distantes où le presse-papier ne fonctionne pas (RDP, vSphere/ESXi, IPMI/iDRAC/iLO, VNC/noVNC web, SSH verrouillé).

## Le problème

Sur ces consoles, le copier-coller est cassé dans les **deux sens**. Scrybe rétablit le pont :

- **Extraction** (écran → local) : capture d'une zone d'écran → OCR → texte propre, *code-aware*, directement dans le presse-papier local.
- **Injection** (local → distant) : saisie d'une chaîne dans la console « comme tapée au clavier » - commande PowerShell longue, ou auto-saisie d'un mot de passe stocké - puisque le paste est bloqué.

## Différenciation

Ni Capture2Text ni Textify ne font l'injection. Aucun concurrent ne couvre les deux directions.

## Contraintes projet

- **Licence** : Apache 2.0
- **Code / specs techniques** : anglais
- **Documentation user-facing** : français
- **Local-first** : hors-ligne, zéro télémétrie, zéro OCR cloud
- **Windows-native** : HiDPI / multi-écrans / per-monitor DPI aware
- **Thème** : Dracula

## État

🟢 **v1.0 - Dogfooding.** Les deux piliers sont en place : extraction OCR vers presse-papier local, injection clavier locale/distante, vault secrets DPAPI, réglages, hotkeys, packaging et Control Hub. Le backlog restant est volontairement limité aux besoins révélés par l'usage réel.

## Structure

```
Scrybe/
├── src/            # application WPF, Core et moteur OCR
├── tests/          # suite xUnit
├── locales/        # chaînes localisées EN/FR
├── tessdata/       # modèle OCR embarqué
├── docs/adr/       # décisions d'architecture
└── README.md
```
