# MarketMogul

Plugin [Dalamud](https://github.com/goatcorp/Dalamud) pour Final Fantasy XIV qui recommande quoi **acheter sur les autres mondes de ton data center** pour le **revendre sur ton monde d'origine**.

Le plugin est **consultatif** : il calcule et conseille, c'est toi qui achètes et mets en vente. Aucune automatisation d'achat ou de vente.

## Fonctionnement

1. Le plugin détecte seul ton monde d'origine, ton monde actuel et ton data center.
2. Un scan interroge l'API [Universalis](https://universalis.app) pour tous les objets vendables : annonces sur tout le data center, historique des ventes sur ton monde (~330 requêtes, environ 1 minute).
3. Pour chaque objet (NQ et HQ), le moteur calcule :
   - un **prix de revente réaliste** : médiane des ventes des 14 derniers jours, ou juste sous l'annonce la moins chère si la concurrence affiche moins cher ;
   - la **vitesse de vente** sur ton monde, et donc la quantité que le marché peut absorber dans l'horizon de ton style de trading ;
   - les **annonces à acheter**, de la moins chère à la plus chère, tant qu'elles restent rentables après taxes (une annonce s'achète en entier) ;
   - une **fiabilité** (0–100 %) : fraîcheur des données, nombre de ventes observées, stabilité du prix.
4. Les opportunités sont triées par **profit par jour × fiabilité**.
5. En visite sur un autre monde, la fenêtre passe en **mode achat** et ne montre que ce qu'il y a à acheter là, avec un rappel dans le chat à l'arrivée.

Commande : `/mogul`.

## Structure

```
src/MarketMogul.Core/    Moteur de calcul, sans dépendance à Dalamud (réutilisable et testable)
  Universalis/           Client HTTP de l'API Universalis (paquets de 100 objets, limitation de débit)
  Analysis/              Statistiques de marché, carnet d'ordres, scoring, scanner
src/MarketMogul/         Plugin Dalamud : détection du monde, catalogue des objets, fenêtres ImGui
tests/                   Tests unitaires du moteur (xUnit)
```

## Développement

Prérequis : [.NET 10 SDK](https://dotnet.microsoft.com/download) et XIVLauncher avec Dalamud installé (les librairies sont lues dans `%AppData%\XIVLauncher\addon\Hooks\dev`, ou dans le dossier indiqué par la variable `DALAMUD_HOME`).

```bash
dotnet build
dotnet test
```

Pour charger le plugin en jeu :

1. `/xlsettings` → **Experimental** → **Dev Plugin Locations** : ajoute le chemin complet vers `src/MarketMogul/bin/x64/Debug/MarketMogul.dll`.
2. `/xlplugins` → **Dev Tools** → **Installed Dev Plugins** → active MarketMogul.

La CI GitHub compile, lance les tests et publie le plugin en artefact à chaque push sur `main`.

## Hypothèses à vérifier en jeu

- **Frais à la revente** : 5 % par défaut, hypothèse prudente. Réglable dans les réglages.
- Les données Universalis sont envoyées par les joueurs : une annonce peut avoir disparu. La fiabilité reflète ce risque.

## Feuille de route

- [ ] Surligner les annonces rentables directement dans la fenêtre du marketboard.
- [ ] Optimiseur de portefeuille : répartir ton budget (gils lus en jeu) et tes emplacements de vente.
- [ ] Prix conseillé et alertes de sous-cotation chez les servants.
- [ ] Journal P&L : achats et ventes réels, capital immobilisé, objets qui ne se vendent pas.
- [ ] Facteur de concurrence : stock déjà en vente et fréquence des sous-cotations.
- [ ] Stratégie marchand PNJ → marketboard.
- [ ] Scan incrémental et cache local.
